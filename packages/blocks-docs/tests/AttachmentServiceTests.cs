using System.Text;
using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class AttachmentServiceTests
{
    private static TenantId Tenant() => new("acme");

    private static (AttachmentService Svc, InMemoryAttachmentRepository Repo) NewSut()
    {
        var repo = new InMemoryAttachmentRepository();
        return (new AttachmentService(repo), repo);
    }

    private static ReadOnlyMemory<byte> Bytes(string s) => Encoding.UTF8.GetBytes(s);

    // ── Sha-256 hashing ───────────────────────────────────────────────

    [Fact]
    public void ComputeSha256Hex_KnownVector()
    {
        // "abc" → ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        var hash = AttachmentService.ComputeSha256Hex(Encoding.UTF8.GetBytes("abc"));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public void ComputeSha256Hex_EmptyVector()
    {
        var hash = AttachmentService.ComputeSha256Hex(ReadOnlySpan<byte>.Empty);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }

    [Fact]
    public void ComputeSha256Hex_LowercaseHex()
    {
        var hash = AttachmentService.ComputeSha256Hex(Encoding.UTF8.GetBytes("any input"));
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.Equal(64, hash.Length);
    }

    // ── UploadAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_FreshBytes_CreatesAttachmentWithMatchingHash()
    {
        var (svc, _) = NewSut();
        var a = await svc.UploadAsync(Tenant(), Bytes("hello"), "text/plain", "hello.txt", "user-1");

        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", a.ContentHash);
        Assert.Equal(5L, a.SizeBytes);
        Assert.Equal(AttachmentStatus.Active, a.Status);
        Assert.Equal(StorageRefKind.Inline, a.StorageRef.Kind);
    }

    [Fact]
    public async Task Upload_DuplicateBytesInSameTenant_DedupsToExistingAttachment()
    {
        var (svc, repo) = NewSut();
        var a1 = await svc.UploadAsync(Tenant(), Bytes("same"), "text/plain", "first.txt", "user-1");
        var a2 = await svc.UploadAsync(Tenant(), Bytes("same"), "text/plain", "second.txt", "user-2");

        Assert.Equal(a1.Id, a2.Id);
        // Only one row in the repo.
        Assert.Single(await repo.ListByTenantAsync(Tenant()));
    }

    [Fact]
    public async Task Upload_DuplicateBytesAcrossTenants_DoesNotDedupe()
    {
        // Tenant isolation: identical bytes in two different tenants
        // produce two separate Attachment rows.
        var (svc, repo) = NewSut();
        var tenA = new TenantId("a");
        var tenB = new TenantId("b");
        var a = await svc.UploadAsync(tenA, Bytes("shared"), "text/plain", "f.txt", "u");
        var b = await svc.UploadAsync(tenB, Bytes("shared"), "text/plain", "f.txt", "u");

        Assert.NotEqual(a.Id, b.Id);
        Assert.Single(await repo.ListByTenantAsync(tenA));
        Assert.Single(await repo.ListByTenantAsync(tenB));
    }

    [Fact]
    public async Task Upload_DifferentBytes_CreatesSeparateAttachments()
    {
        var (svc, repo) = NewSut();
        await svc.UploadAsync(Tenant(), Bytes("alpha"), "text/plain", "a.txt", "u");
        await svc.UploadAsync(Tenant(), Bytes("beta"), "text/plain", "b.txt", "u");
        Assert.Equal(2, (await repo.ListByTenantAsync(Tenant())).Count);
    }

    [Fact]
    public async Task Upload_StoresBytesInlineForPR2_SizeMatches()
    {
        var (svc, _) = NewSut();
        var a = await svc.UploadAsync(Tenant(), Bytes("hello"), "text/plain", "hello.txt", "u");
        Assert.Equal(StorageRefKind.Inline, a.StorageRef.Kind);
        Assert.NotNull(a.StorageRef.InlineBytes);
        Assert.Equal(5, a.StorageRef.InlineBytes!.Value.Length);
    }

    [Fact]
    public async Task Upload_PropagatesSensitivity()
    {
        var (svc, _) = NewSut();
        var a = await svc.UploadAsync(Tenant(), Bytes("ssn-stuff"), "text/plain", "ssn.txt", "u", Sensitivity.Pii);
        Assert.Equal(Sensitivity.Pii, a.Sensitivity);
    }

    // ── SupersedeAsync ────────────────────────────────────────────────

    [Fact]
    public async Task Supersede_FreshBytes_NewAttachmentReplacesPrior()
    {
        var (svc, repo) = NewSut();
        var prior = await svc.UploadAsync(Tenant(), Bytes("v1"), "text/plain", "doc.txt", "u");

        var next = await svc.SupersedeAsync(prior.Id, Bytes("v2"), "text/plain", "doc.txt", "u");

        Assert.NotEqual(prior.Id, next.Id);
        Assert.Equal(prior.Id, next.ReplacesAttachmentId);
        Assert.Equal(AttachmentStatus.Active, next.Status);

        var refetchPrior = await repo.GetAsync(prior.Id);
        Assert.NotNull(refetchPrior);
        Assert.Equal(AttachmentStatus.Superseded, refetchPrior!.Status);
        Assert.Equal(next.Id, refetchPrior.ReplacedByAttachmentId);
    }

    [Fact]
    public async Task Supersede_OnTombstonedPrior_Throws()
    {
        var (svc, repo) = NewSut();
        var prior = await svc.UploadAsync(Tenant(), Bytes("v1"), "text/plain", "doc.txt", "u");
        await repo.SoftDeleteAsync(prior.Id, "u", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SupersedeAsync(prior.Id, Bytes("v2"), "text/plain", "doc.txt", "u"));
    }

    [Fact]
    public async Task Supersede_OnAlreadySupersededPrior_Throws()
    {
        var (svc, _) = NewSut();
        var v1 = await svc.UploadAsync(Tenant(), Bytes("v1"), "text/plain", "doc.txt", "u");
        await svc.SupersedeAsync(v1.Id, Bytes("v2"), "text/plain", "doc.txt", "u");
        // v1 is now Superseded.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SupersedeAsync(v1.Id, Bytes("v3"), "text/plain", "doc.txt", "u"));
    }

    [Fact]
    public async Task Supersede_UnknownPrior_Throws()
    {
        var (svc, _) = NewSut();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SupersedeAsync(AttachmentId.NewId(), Bytes("v"), "text/plain", "f.txt", "u"));
    }

    [Fact]
    public async Task Supersede_DefaultsSensitivityToPriorWhenNotSupplied()
    {
        var (svc, _) = NewSut();
        var prior = await svc.UploadAsync(Tenant(), Bytes("v1"), "text/plain", "doc.txt", "u", Sensitivity.Confidential);

        var next = await svc.SupersedeAsync(prior.Id, Bytes("v2"), "text/plain", "doc.txt", "u");
        Assert.Equal(Sensitivity.Confidential, next.Sensitivity);
    }

    [Fact]
    public async Task Supersede_SensitivityOverrideApplies()
    {
        var (svc, _) = NewSut();
        var prior = await svc.UploadAsync(Tenant(), Bytes("v1"), "text/plain", "doc.txt", "u", Sensitivity.Internal);
        var next = await svc.SupersedeAsync(prior.Id, Bytes("v2"), "text/plain", "doc.txt", "u",
            sensitivity: Sensitivity.Pii);
        Assert.Equal(Sensitivity.Pii, next.Sensitivity);
    }
}
