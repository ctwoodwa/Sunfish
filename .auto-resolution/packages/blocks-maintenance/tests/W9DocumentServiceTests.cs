using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Foundation.Recovery.TenantKey;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

public sealed class W9DocumentServiceTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly DateTimeOffset Future = Now.AddHours(1);
    private static readonly DateTimeOffset Past = Now.AddHours(-1);
    private static readonly W9MailingAddress Address = new("123 Main St", null, "Springfield", "OR", "97477");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static FixedDecryptCapability ValidCap(TenantId tenant) =>
        new("cap-1", new ActorId("operator-1"), tenant, Future);

    private sealed record Bundle(InMemoryW9DocumentService Service, ITenantKeyProvider Keys);

    private static Bundle BuildAuditDisabled()
    {
        var keys = new InMemoryTenantKeyProvider();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var dec = new TenantKeyProviderFieldDecryptor(keys);
        return new Bundle(new InMemoryW9DocumentService(enc, dec), keys);
    }

    private static (InMemoryW9DocumentService Service, IAuditTrail Trail) BuildAuditEnabled()
    {
        var keys = new InMemoryTenantKeyProvider();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var dec = new TenantKeyProviderFieldDecryptor(keys, trail, signer);
        return (new InMemoryW9DocumentService(enc, dec), trail);
    }

    private static CreateW9DocumentRequest SampleRequest(TenantId tenant, byte[]? tin = null) =>
        new(
            Vendor: new VendorId(Guid.NewGuid().ToString()),
            LegalName: "Acme Plumbing LLC",
            DbaName: null,
            TaxClassification: W9TaxClassification.LLC,
            PlaintextTin: tin ?? Encoding.UTF8.GetBytes("12-3456789"),
            Address: Address,
            Tenant: tenant,
            ReceivedAt: Now);

    [Fact]
    public async Task CreateAsync_EncryptsTin_AndDoesNotPersistPlaintext()
    {
        var bundle = BuildAuditDisabled();

        var doc = await bundle.Service.CreateAsync(SampleRequest(TenantA), Ct);

        Assert.NotEqual(default, doc.Id);
        Assert.Equal(1, doc.TinEncrypted.KeyVersion);
        Assert.True(doc.TinEncrypted.Ciphertext.Length > 0);
        Assert.True(doc.TinEncrypted.Nonce.Length > 0);
        // Ciphertext must not contain the plaintext bytes verbatim.
        var plaintext = Encoding.UTF8.GetBytes("12-3456789");
        Assert.False(doc.TinEncrypted.Ciphertext.Span.IndexOf(plaintext) >= 0);
    }

    [Fact]
    public async Task GetWithDecryptedTinAsync_RoundTripsToOriginalPlaintext()
    {
        var bundle = BuildAuditDisabled();
        var plaintext = Encoding.UTF8.GetBytes("12-3456789");
        var doc = await bundle.Service.CreateAsync(SampleRequest(TenantA, plaintext), Ct);

        var view = await bundle.Service.GetWithDecryptedTinAsync(doc.Id, ValidCap(TenantA), TenantA, Ct);

        Assert.True(plaintext.AsSpan().SequenceEqual(view.Tin.Span));
        Assert.Equal(doc.Id, view.Id);
        Assert.Equal("Acme Plumbing LLC", view.LegalName);
        Assert.Equal(W9TaxClassification.LLC, view.TaxClassification);
    }

    [Fact]
    public async Task GetWithDecryptedTinAsync_RejectsExpiredCapability()
    {
        var bundle = BuildAuditDisabled();
        var doc = await bundle.Service.CreateAsync(SampleRequest(TenantA), Ct);
        var expired = new FixedDecryptCapability("cap-x", new ActorId("op"), TenantA, Past);

        await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => bundle.Service.GetWithDecryptedTinAsync(doc.Id, expired, TenantA, Ct));
    }

    [Fact]
    public async Task GetWithDecryptedTinAsync_RejectsWrongTenantCapability()
    {
        var bundle = BuildAuditDisabled();
        var doc = await bundle.Service.CreateAsync(SampleRequest(TenantA), Ct);
        var wrongTenant = new FixedDecryptCapability("cap-x", new ActorId("op"), TenantB, Future);

        await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => bundle.Service.GetWithDecryptedTinAsync(doc.Id, wrongTenant, TenantA, Ct));
    }

    [Fact]
    public async Task GetAsync_AcrossDifferentTenants_ReturnsNull()
    {
        var bundle = BuildAuditDisabled();
        var doc = await bundle.Service.CreateAsync(SampleRequest(TenantA), Ct);

        var crossTenant = await bundle.Service.GetAsync(doc.Id, TenantB, Ct);

        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task GetAsync_WithoutCapability_DoesNotDecrypt()
    {
        var bundle = BuildAuditDisabled();
        var doc = await bundle.Service.CreateAsync(SampleRequest(TenantA), Ct);

        var fetched = await bundle.Service.GetAsync(doc.Id, TenantA, Ct);

        Assert.NotNull(fetched);
        // The retrieved record carries only ciphertext, never plaintext.
        Assert.Equal(doc.TinEncrypted.KeyVersion, fetched!.TinEncrypted.KeyVersion);
    }

    [Fact]
    public async Task DifferentTenants_ProduceDifferentCiphertextForSameTin()
    {
        var bundle = BuildAuditDisabled();
        var tin = Encoding.UTF8.GetBytes("12-3456789");

        var docA = await bundle.Service.CreateAsync(SampleRequest(TenantA, tin), Ct);
        var docB = await bundle.Service.CreateAsync(SampleRequest(TenantB, tin), Ct);

        Assert.False(docA.TinEncrypted.Ciphertext.Span.SequenceEqual(docB.TinEncrypted.Ciphertext.Span));
    }

    [Fact]
    public async Task VerifyAsync_StampsVerifiedAtAndVerifiedBy()
    {
        var bundle = BuildAuditDisabled();
        var doc = await bundle.Service.CreateAsync(SampleRequest(TenantA), Ct);

        var verified = await bundle.Service.VerifyAsync(doc.Id, new ActorId("operator-7"), TenantA, Ct);

        Assert.NotNull(verified.VerifiedAt);
        Assert.Equal("operator-7", verified.VerifiedBy?.Value);
    }

    [Fact]
    public async Task VerifyAsync_IsIdempotent()
    {
        var bundle = BuildAuditDisabled();
        var doc = await bundle.Service.CreateAsync(SampleRequest(TenantA), Ct);

        var v1 = await bundle.Service.VerifyAsync(doc.Id, new ActorId("op-a"), TenantA, Ct);
        var v2 = await bundle.Service.VerifyAsync(doc.Id, new ActorId("op-b"), TenantA, Ct);

        Assert.Equal(v1.VerifiedAt, v2.VerifiedAt);
        Assert.Equal(v1.VerifiedBy, v2.VerifiedBy);
    }

    [Fact]
    public async Task VerifyAsync_UnknownDocument_Throws()
    {
        var bundle = BuildAuditDisabled();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bundle.Service.VerifyAsync(new W9DocumentId(Guid.NewGuid()), new ActorId("op"), TenantA, Ct));
    }

    [Fact]
    public async Task GetWithDecryptedTinAsync_AuditEnabled_EmitsFieldDecryptedRecord()
    {
        var (service, trail) = BuildAuditEnabled();
        var doc = await service.CreateAsync(SampleRequest(TenantA), Ct);

        await service.GetWithDecryptedTinAsync(doc.Id, ValidCap(TenantA), TenantA, Ct);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FieldDecrypted) && r.TenantId.Equals(TenantA)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetWithDecryptedTinAsync_AuditEnabled_EmitsDeniedRecordOnRejection()
    {
        var (service, trail) = BuildAuditEnabled();
        var doc = await service.CreateAsync(SampleRequest(TenantA), Ct);
        var expired = new FixedDecryptCapability("cap-x", new ActorId("op"), TenantA, Past);

        await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => service.GetWithDecryptedTinAsync(doc.Id, expired, TenantA, Ct));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FieldDecryptionDenied)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void W9Document_SignatureRefIsNullableInPhase4()
    {
        // Phase 4 invariant: SignatureRef is nullable so W-9 records can be
        // captured before W#21 signature substrate is wired.
        var prop = typeof(W9Document).GetProperty(nameof(W9Document.SignatureRef));
        Assert.NotNull(prop);
        Assert.True(Nullable.GetUnderlyingType(prop!.PropertyType) is not null
                    || prop.PropertyType.IsClass);
    }
}
