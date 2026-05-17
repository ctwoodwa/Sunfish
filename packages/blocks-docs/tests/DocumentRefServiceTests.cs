using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class DocumentRefServiceTests
{
    private static TenantId Tenant(string v = "acme") => new(v);

    private static async Task<Attachment> SeedAttachmentAsync(
        IAttachmentRepository repo, TenantId tenantId)
    {
        var a = Attachment.Create(
            tenantId: tenantId,
            storageRef: StorageRef.ForFoundationBlob("cid-test"),
            contentHash: Guid.NewGuid().ToString(),
            mimeType: "application/pdf",
            sizeBytes: 100,
            originalFilename: "x.pdf",
            createdBy: "u");
        await repo.UpsertAsync(a);
        return a;
    }

    private static (DocumentRefService service, IAttachmentRepository attachments, IDocumentRefRepository refs)
        BuildService()
    {
        var attachments = new InMemoryAttachmentRepository();
        var refs = new InMemoryDocumentRefRepository();
        var service = new DocumentRefService(refs, attachments);
        return (service, attachments, refs);
    }

    [Fact]
    public async Task Link_NewParent_CreatesLink()
    {
        var (svc, attachments, _) = BuildService();
        var a = await SeedAttachmentAsync(attachments, Tenant());

        var link = await svc.LinkAsync(Tenant(), a.Id,
            clusterCode: "blocks-financial-ar",
            parentEntityType: "invoice",
            parentEntityId: "INV-1",
            actor: "u",
            attachmentRole: "primary-attachment");

        Assert.Equal(a.Id, link.AttachmentId);
        Assert.Equal("primary-attachment", link.AttachmentRole);
        Assert.Equal(1, link.Version);
    }

    [Fact]
    public async Task Link_SameParentTwice_IsIdempotent()
    {
        var (svc, attachments, _) = BuildService();
        var a = await SeedAttachmentAsync(attachments, Tenant());

        var first = await svc.LinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u");
        var second = await svc.LinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, second.Version); // no version bump when role unchanged
    }

    [Fact]
    public async Task Link_SameParent_DifferentRole_UpdatesRoleInPlace()
    {
        var (svc, attachments, _) = BuildService();
        var a = await SeedAttachmentAsync(attachments, Tenant());

        var first = await svc.LinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u",
            attachmentRole: "supporting-doc");
        var second = await svc.LinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u",
            attachmentRole: "primary-attachment");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("primary-attachment", second.AttachmentRole);
        Assert.Equal(2, second.Version);
    }

    [Fact]
    public async Task Link_CrossTenant_Throws()
    {
        var (svc, attachments, _) = BuildService();
        var a = await SeedAttachmentAsync(attachments, Tenant("owner"));

        // Caller is a different tenant. Link must be rejected.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.LinkAsync(Tenant("attacker"), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u"));
    }

    [Fact]
    public async Task Link_MissingAttachment_Throws()
    {
        var (svc, _, _) = BuildService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.LinkAsync(Tenant(), AttachmentId.NewId(), "blocks-financial-ar", "invoice", "INV-1", "u"));
    }

    [Fact]
    public async Task Link_TombstonedAttachment_Throws()
    {
        var (svc, attachments, _) = BuildService();
        var a = await SeedAttachmentAsync(attachments, Tenant());
        await attachments.SoftDeleteAsync(a.Id, "u", "test");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.LinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u"));
    }

    [Fact]
    public async Task Unlink_ExistingLink_ReturnsTrue_AndTombstones()
    {
        var (svc, attachments, refs) = BuildService();
        var a = await SeedAttachmentAsync(attachments, Tenant());
        var link = await svc.LinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u");

        Assert.True(await svc.UnlinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u",
            reason: "invoice voided"));

        // Live count drops to 0.
        Assert.Equal(0, await svc.CountLiveLinksToAttachmentAsync(Tenant(), a.Id));
        Assert.Null(await refs.GetAsync(link.Id));
    }

    [Fact]
    public async Task Unlink_NoMatch_ReturnsFalse()
    {
        var (svc, attachments, _) = BuildService();
        var a = await SeedAttachmentAsync(attachments, Tenant());
        // No link created.
        Assert.False(await svc.UnlinkAsync(Tenant(), a.Id, "blocks-leases", "lease", "L-1", "u"));
    }

    [Fact]
    public async Task CountLiveLinks_SumsAcrossClusters_ExcludesTombstoned()
    {
        var (svc, attachments, _) = BuildService();
        var a = await SeedAttachmentAsync(attachments, Tenant());

        await svc.LinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u");
        await svc.LinkAsync(Tenant(), a.Id, "blocks-leases", "lease", "L-100", "u");
        await svc.LinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-2", "u");

        Assert.Equal(3, await svc.CountLiveLinksToAttachmentAsync(Tenant(), a.Id));

        await svc.UnlinkAsync(Tenant(), a.Id, "blocks-leases", "lease", "L-100", "u");
        Assert.Equal(2, await svc.CountLiveLinksToAttachmentAsync(Tenant(), a.Id));
    }

    [Fact]
    public async Task CountLiveLinks_OtherTenant_DoesNotLeak()
    {
        var (svc, attachments, _) = BuildService();
        var a = await SeedAttachmentAsync(attachments, Tenant("a"));
        await svc.LinkAsync(Tenant("a"), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u");

        // Tenant b never linked anything to a's attachment — count must be 0
        // even if `b` somehow had the AttachmentId.
        Assert.Equal(0, await svc.CountLiveLinksToAttachmentAsync(Tenant("b"), a.Id));
    }
}
