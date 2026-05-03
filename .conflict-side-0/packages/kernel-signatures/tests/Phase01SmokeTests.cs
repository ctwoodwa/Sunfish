using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Taxonomy.Models;
using Sunfish.Kernel.Signatures.DependencyInjection;
using Sunfish.Kernel.Signatures.Models;
using Sunfish.Kernel.Signatures.Services;
using Xunit;

namespace Sunfish.Kernel.Signatures.Tests;

/// <summary>
/// W#21 Phase 0+1 smoke tests: Phase 0 envelope round-trip + Phase 1
/// substrate end-to-end (consent → capture → revocation).
/// </summary>
public sealed class Phase01SmokeTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly ActorId TestSigner = new("alice@example.com");

    private static readonly TaxonomyClassification LeaseExecutionScope = new()
    {
        Definition = new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"),
        Code = "lease-execution",
        Version = TaxonomyVersion.V1_0_0,
    };

    // ─────────── Phase 0 — SignatureEnvelope stub ───────────

    [Fact]
    public void SignatureEnvelope_RoundTrips_ThroughEquality()
    {
        var headers = new Dictionary<string, string> { ["kid"] = "key-1" };
        var envelope1 = new SignatureEnvelope("ed25519", new byte[64], headers);
        var envelope2 = new SignatureEnvelope("ed25519", envelope1.Signature, headers);

        Assert.Equal(envelope1.Algorithm, envelope2.Algorithm);
        Assert.Equal(envelope1.Headers, envelope2.Headers);
    }

    [Fact]
    public void SignatureEnvelope_AcceptsAnyAlgorithmString_InPhase0()
    {
        // Phase 0 is intentionally permissive — algorithm validation is deferred to ADR 0004 Stage 06.
        var unknownAlgo = new SignatureEnvelope("future-pqc-algorithm", new byte[128], new Dictionary<string, string>());
        Assert.Equal("future-pqc-algorithm", unknownAlgo.Algorithm);
    }

    // ─────────── ContentHash ───────────

    [Fact]
    public void ContentHash_ComputeFromUtf8Nfc_Deterministic()
    {
        var h1 = ContentHash.ComputeFromUtf8Nfc("hello world");
        var h2 = ContentHash.ComputeFromUtf8Nfc("hello world");
        Assert.True(h1.ConstantTimeEquals(h2));
        Assert.Equal(32, h1.Bytes.Length);
    }

    [Fact]
    public void ContentHash_NfcNormalizesEquivalentForms()
    {
        // Both forms render the same character "é" via different code points.
        var precomposed = "café";    // U+00E9 = é
        var decomposed = "café";   // e + U+0301 combining acute
        var h1 = ContentHash.ComputeFromUtf8Nfc(precomposed);
        var h2 = ContentHash.ComputeFromUtf8Nfc(decomposed);
        Assert.True(h1.ConstantTimeEquals(h2));
    }

    [Fact]
    public void ContentHash_ToString_RendersSha256Prefix()
    {
        var h = ContentHash.ComputeFromUtf8Nfc("test");
        Assert.StartsWith("sha256:", h.ToString());
    }

    // ─────────── ConsentRegistry ───────────

    [Fact]
    public async Task ConsentRegistry_RecordsAndReadsBack_CurrentConsent()
    {
        var registry = new InMemoryConsentRegistry();
        var consent = MakeConsent();

        await registry.RecordAsync(consent, default);
        var current = await registry.GetCurrentAsync(TestTenant, TestSigner, consent.GivenAt.AddMinutes(1), default);

        Assert.NotNull(current);
        Assert.Equal(consent.Id, current!.Id);
    }

    [Fact]
    public async Task ConsentRegistry_GetCurrent_ReturnsNull_WhenRevoked()
    {
        var registry = new InMemoryConsentRegistry();
        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);

        await registry.RevokeAsync(consent.Id, consent.GivenAt.AddMinutes(5), default);
        var current = await registry.GetCurrentAsync(TestTenant, TestSigner, consent.GivenAt.AddMinutes(10), default);

        Assert.Null(current);
    }

    [Fact]
    public async Task ConsentRegistry_GetCurrent_ReturnsNull_AfterExpiry()
    {
        var registry = new InMemoryConsentRegistry();
        var givenAt = DateTimeOffset.UtcNow;
        var consent = MakeConsent() with { GivenAt = givenAt, ExpiresAt = givenAt.AddDays(30) };
        await registry.RecordAsync(consent, default);

        var current = await registry.GetCurrentAsync(TestTenant, TestSigner, givenAt.AddDays(31), default);
        Assert.Null(current);
    }

    // ─────────── SignatureCapture ───────────

    [Fact]
    public async Task Capture_SucceedsWith_CurrentConsent()
    {
        var registry = new InMemoryConsentRegistry();
        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);

        var capture = new InMemorySignatureCapture(registry);
        var ev = await capture.CaptureAsync(MakeRequest(consent.Id), default);

        Assert.NotEqual(default, ev.Id);
        Assert.Equal(TestSigner, ev.Signer);
        Assert.Equal(consent.Id, ev.Consent);
    }

    [Fact]
    public async Task Capture_RefusesWithout_CurrentConsent()
    {
        var registry = new InMemoryConsentRegistry();   // no consent recorded
        var capture = new InMemorySignatureCapture(registry);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.CaptureAsync(MakeRequest(new ConsentRecordId(Guid.NewGuid())), default));
    }

    [Fact]
    public async Task Capture_RefusesIf_RequestConsentMismatch()
    {
        var registry = new InMemoryConsentRegistry();
        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);
        var capture = new InMemorySignatureCapture(registry);

        // Pass a stale consent id — current registry consent has a different id.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.CaptureAsync(MakeRequest(new ConsentRecordId(Guid.NewGuid())), default));
    }

    [Fact]
    public async Task Capture_RefusesEmptyScope()
    {
        var registry = new InMemoryConsentRegistry();
        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);
        var capture = new InMemorySignatureCapture(registry);

        var bad = MakeRequest(consent.Id) with { Scope = Array.Empty<TaxonomyClassification>() };
        await Assert.ThrowsAsync<ArgumentException>(() => capture.CaptureAsync(bad, default));
    }

    [Fact]
    public async Task Capture_RoundTrips_ViaGetAsync()
    {
        var registry = new InMemoryConsentRegistry();
        var consent = MakeConsent();
        await registry.RecordAsync(consent, default);
        var capture = new InMemorySignatureCapture(registry);

        var ev = await capture.CaptureAsync(MakeRequest(consent.Id), default);
        var fetched = await capture.GetAsync(ev.Id, default);

        Assert.NotNull(fetched);
        Assert.Equal(ev, fetched);
    }

    // ─────────── SignatureRevocationLog ───────────

    [Fact]
    public async Task RevocationLog_FreshSignature_IsValid()
    {
        var log = new InMemorySignatureRevocationLog();
        var status = await log.GetCurrentValidityAsync(new SignatureEventId(Guid.NewGuid()), default);
        Assert.True(status.IsValid);
    }

    [Fact]
    public async Task RevocationLog_AppendThenQuery_MarksInvalid()
    {
        var log = new InMemorySignatureRevocationLog();
        var sigId = new SignatureEventId(Guid.NewGuid());
        var revocation = MakeRevocation(sigId);

        await log.AppendAsync(revocation, default);
        var status = await log.GetCurrentValidityAsync(sigId, default);

        Assert.False(status.IsValid);
        Assert.Equal(revocation.Id, status.RevokedBy!.Id);
    }

    [Fact]
    public async Task RevocationLog_AppendIsIdempotent()
    {
        var log = new InMemorySignatureRevocationLog();
        var sigId = new SignatureEventId(Guid.NewGuid());
        var revocation = MakeRevocation(sigId);

        await log.AppendAsync(revocation, default);
        await log.AppendAsync(revocation, default); // same Id → no-op

        var entries = new List<SignatureRevocation>();
        await foreach (var r in log.ListRevocationsAsync(sigId, default))
        {
            entries.Add(r);
        }
        Assert.Single(entries);
    }

    // ─────────── DI ───────────

    [Fact]
    public void DI_RegistersThreeServices()
    {
        var services = new ServiceCollection();
        services.AddInMemoryKernelSignatures();
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IConsentRegistry>());
        Assert.NotNull(sp.GetRequiredService<ISignatureCapture>());
        Assert.NotNull(sp.GetRequiredService<ISignatureRevocationLog>());
    }

    // ─────────── Helpers ───────────

    private static ConsentRecord MakeConsent() => new()
    {
        Id = new ConsentRecordId(Guid.NewGuid()),
        Principal = TestSigner,
        Tenant = TestTenant,
        GivenAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        AffirmationText = "I agree to electronic signatures.",
        GivenFromIp = "198.51.100.42",
    };

    private static SignatureCaptureRequest MakeRequest(ConsentRecordId consentId) => new()
    {
        Tenant = TestTenant,
        Signer = TestSigner,
        Consent = consentId,
        DocumentHash = ContentHash.ComputeFromUtf8Nfc("contract body"),
        Scope = new[] { LeaseExecutionScope },
        Envelope = new SignatureEnvelope("ed25519", new byte[64], new Dictionary<string, string>()),
        Quality = new CaptureQuality
        {
            StrokeFidelity = PenStrokeFidelity.LowResolution,
            ClockSource = ClockSource.ServerSide,
            DeviceTouchAvailable = true,
            DocumentReviewedBeforeSign = true,
        },
    };

    private static SignatureRevocation MakeRevocation(SignatureEventId sigId) => new()
    {
        Id = new RevocationEventId(Guid.NewGuid()),
        SignatureEvent = sigId,
        RevokedAt = DateTimeOffset.UtcNow,
        RevokedBy = new ActorId("operator"),
        Reason = RevocationReason.SignerRequest,
    };
}
