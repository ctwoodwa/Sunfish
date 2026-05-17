using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class DocumentRefReconcilerTests
{
    private static TenantId Tenant() => new("acme");

    private static async Task<Attachment> SeedAttachmentAsync(IAttachmentRepository repo)
    {
        var a = Attachment.Create(
            tenantId: Tenant(),
            storageRef: StorageRef.ForFoundationBlob("cid-r"),
            contentHash: Guid.NewGuid().ToString(),
            mimeType: "application/pdf",
            sizeBytes: 10,
            originalFilename: "x.pdf",
            createdBy: "u");
        await repo.UpsertAsync(a);
        return a;
    }

    [Fact]
    public async Task TombstoneParentLinks_RemovesAllLiveLinksForThatParent()
    {
        var attachments = new InMemoryAttachmentRepository();
        var refs = new InMemoryDocumentRefRepository();
        var svc = new DocumentRefService(refs, attachments);
        var reconciler = new DocumentRefReconciler(refs);

        var a1 = await SeedAttachmentAsync(attachments);
        var a2 = await SeedAttachmentAsync(attachments);
        var a3 = await SeedAttachmentAsync(attachments);

        // Two links to invoice INV-99, one to a different parent.
        await svc.LinkAsync(Tenant(), a1.Id, "blocks-financial-ar", "invoice", "INV-99", "u");
        await svc.LinkAsync(Tenant(), a2.Id, "blocks-financial-ar", "invoice", "INV-99", "u");
        await svc.LinkAsync(Tenant(), a3.Id, "blocks-financial-ar", "invoice", "INV-OTHER", "u");

        var tombstoned = await reconciler.TombstoneParentLinksAsync(
            Tenant(), "blocks-financial-ar", "invoice", "INV-99",
            actor: "system", reason: "invoice hard-deleted");

        Assert.Equal(2, tombstoned);
        Assert.Equal(0, await svc.CountLiveLinksToAttachmentAsync(Tenant(), a1.Id));
        Assert.Equal(0, await svc.CountLiveLinksToAttachmentAsync(Tenant(), a2.Id));
        // The unrelated link survives.
        Assert.Equal(1, await svc.CountLiveLinksToAttachmentAsync(Tenant(), a3.Id));
    }

    [Fact]
    public async Task TombstoneParentLinks_NoLiveLinks_ReturnsZero()
    {
        var refs = new InMemoryDocumentRefRepository();
        var reconciler = new DocumentRefReconciler(refs);

        var n = await reconciler.TombstoneParentLinksAsync(
            Tenant(), "blocks-financial-ar", "invoice", "INV-NEVER-LINKED",
            actor: "system");
        Assert.Equal(0, n);
    }

    [Fact]
    public async Task TombstoneParentLinks_IsIdempotent()
    {
        var attachments = new InMemoryAttachmentRepository();
        var refs = new InMemoryDocumentRefRepository();
        var svc = new DocumentRefService(refs, attachments);
        var reconciler = new DocumentRefReconciler(refs);

        var a = await SeedAttachmentAsync(attachments);
        await svc.LinkAsync(Tenant(), a.Id, "blocks-leases", "lease", "L-1", "u");

        Assert.Equal(1, await reconciler.TombstoneParentLinksAsync(
            Tenant(), "blocks-leases", "lease", "L-1", "system"));
        // Second call — already-tombstoned rows are not re-counted.
        Assert.Equal(0, await reconciler.TombstoneParentLinksAsync(
            Tenant(), "blocks-leases", "lease", "L-1", "system"));
    }

    [Fact]
    public async Task TombstoneParentLinks_OnlyAffectsRequestedTenant()
    {
        var attachments = new InMemoryAttachmentRepository();
        var refs = new InMemoryDocumentRefRepository();
        var svc = new DocumentRefService(refs, attachments);
        var reconciler = new DocumentRefReconciler(refs);

        var aA = Attachment.Create(new TenantId("a"), StorageRef.ForFoundationBlob("a"), Guid.NewGuid().ToString(),
            "application/pdf", 1, "x.pdf", "u");
        var aB = Attachment.Create(new TenantId("b"), StorageRef.ForFoundationBlob("b"), Guid.NewGuid().ToString(),
            "application/pdf", 1, "x.pdf", "u");
        await attachments.UpsertAsync(aA);
        await attachments.UpsertAsync(aB);

        await svc.LinkAsync(new TenantId("a"), aA.Id, "blocks-leases", "lease", "L-1", "u");
        await svc.LinkAsync(new TenantId("b"), aB.Id, "blocks-leases", "lease", "L-1", "u");

        Assert.Equal(1, await reconciler.TombstoneParentLinksAsync(
            new TenantId("a"), "blocks-leases", "lease", "L-1", "system"));
        Assert.Equal(1, await svc.CountLiveLinksToAttachmentAsync(new TenantId("b"), aB.Id));
    }
}
