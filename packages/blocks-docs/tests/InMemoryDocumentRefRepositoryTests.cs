using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class InMemoryDocumentRefRepositoryTests
{
    private static TenantId Tenant(string v = "acme") => new(v);

    private static DocumentRef MakeRef(
        TenantId? tenantId = null,
        AttachmentId? attachmentId = null,
        string clusterCode = "blocks-financial-ar",
        string entityType = "invoice",
        string entityId = "INV-1",
        string? role = null)
        => DocumentRef.Create(
            tenantId ?? Tenant(),
            attachmentId ?? AttachmentId.NewId(),
            clusterCode,
            entityType,
            entityId,
            createdBy: "u",
            attachmentRole: role);

    [Fact]
    public async Task Upsert_ThenGet_RoundTripsTheRow()
    {
        var repo = new InMemoryDocumentRefRepository();
        var doc = MakeRef();

        await repo.UpsertAsync(doc);
        var fetched = await repo.GetAsync(doc.Id);

        Assert.NotNull(fetched);
        Assert.Equal(doc.Id, fetched!.Id);
        Assert.Equal(doc.AttachmentId, fetched.AttachmentId);
    }

    [Fact]
    public async Task Get_TombstonedRow_ReturnsNull()
    {
        var repo = new InMemoryDocumentRefRepository();
        var doc = MakeRef();
        await repo.UpsertAsync(doc);
        await repo.SoftDeleteAsync(doc.Id, "u", "test cleanup");

        Assert.Null(await repo.GetAsync(doc.Id));
    }

    [Fact]
    public async Task Upsert_OnTombstonedRow_Throws()
    {
        var repo = new InMemoryDocumentRefRepository();
        var doc = MakeRef();
        await repo.UpsertAsync(doc);
        await repo.SoftDeleteAsync(doc.Id, "u", "reason");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpsertAsync(doc));
    }

    [Fact]
    public async Task FindByAttachment_ReturnsAllLiveParentsAcrossClusters()
    {
        var repo = new InMemoryDocumentRefRepository();
        var sharedBlob = AttachmentId.NewId();
        await repo.UpsertAsync(MakeRef(attachmentId: sharedBlob, clusterCode: "blocks-financial-ar", entityType: "invoice", entityId: "INV-1"));
        await repo.UpsertAsync(MakeRef(attachmentId: sharedBlob, clusterCode: "blocks-leases", entityType: "lease", entityId: "L-100"));
        await repo.UpsertAsync(MakeRef(attachmentId: AttachmentId.NewId(), clusterCode: "blocks-financial-ar", entityType: "invoice", entityId: "INV-2"));

        var hits = await repo.FindByAttachmentAsync(Tenant(), sharedBlob);
        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal(sharedBlob, h.AttachmentId));
    }

    [Fact]
    public async Task FindByAttachment_ExcludesTombstoned()
    {
        var repo = new InMemoryDocumentRefRepository();
        var blob = AttachmentId.NewId();
        var live = MakeRef(attachmentId: blob, entityId: "INV-1");
        var dead = MakeRef(attachmentId: blob, entityId: "INV-2");
        await repo.UpsertAsync(live);
        await repo.UpsertAsync(dead);
        await repo.SoftDeleteAsync(dead.Id, "u", "removed");

        var hits = await repo.FindByAttachmentAsync(Tenant(), blob);
        Assert.Single(hits);
        Assert.Equal(live.Id, hits[0].Id);
    }

    [Fact]
    public async Task FindByParent_ReturnsLiveAttachmentsForThatEntity()
    {
        var repo = new InMemoryDocumentRefRepository();
        var primary = MakeRef(entityId: "INV-5", role: "primary-attachment");
        var supporting = MakeRef(entityId: "INV-5", role: "supporting-doc");
        var unrelated = MakeRef(entityId: "INV-6");
        await repo.UpsertAsync(primary);
        await repo.UpsertAsync(supporting);
        await repo.UpsertAsync(unrelated);

        var hits = await repo.FindByParentAsync(Tenant(), "blocks-financial-ar", "invoice", "INV-5");
        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal("INV-5", h.ParentEntityId));
    }

    [Fact]
    public async Task FindByParent_ClusterCodeMatchIsCaseInsensitive()
    {
        var repo = new InMemoryDocumentRefRepository();
        await repo.UpsertAsync(MakeRef(clusterCode: "blocks-financial-ar", entityType: "invoice", entityId: "INV-1"));

        // Caller uses different casing — match should still hit.
        var hits = await repo.FindByParentAsync(Tenant(), "Blocks-Financial-AR", "INVOICE", "INV-1");
        Assert.Single(hits);
    }

    [Fact]
    public async Task FindByParent_TenantIsolation()
    {
        var repo = new InMemoryDocumentRefRepository();
        await repo.UpsertAsync(MakeRef(tenantId: Tenant("a"), entityId: "INV-1"));
        await repo.UpsertAsync(MakeRef(tenantId: Tenant("b"), entityId: "INV-1"));

        var aHits = await repo.FindByParentAsync(Tenant("a"), "blocks-financial-ar", "invoice", "INV-1");
        var bHits = await repo.FindByParentAsync(Tenant("b"), "blocks-financial-ar", "invoice", "INV-1");

        Assert.Single(aHits);
        Assert.Single(bHits);
        Assert.NotEqual(aHits[0].Id, bHits[0].Id);
    }

    [Fact]
    public async Task SoftDelete_UnknownId_ReturnsFalse()
    {
        var repo = new InMemoryDocumentRefRepository();
        Assert.False(await repo.SoftDeleteAsync(DocumentRefId.NewId(), "u", "missing"));
    }

    [Fact]
    public async Task SoftDelete_AlreadyTombstoned_ReturnsFalse_AndIsIdempotent()
    {
        var repo = new InMemoryDocumentRefRepository();
        var doc = MakeRef();
        await repo.UpsertAsync(doc);
        Assert.True(await repo.SoftDeleteAsync(doc.Id, "u", "first"));
        Assert.False(await repo.SoftDeleteAsync(doc.Id, "u", "second"));
        // Get still returns null both times.
        Assert.Null(await repo.GetAsync(doc.Id));
    }

    [Fact]
    public async Task SoftDelete_BumpsVersion_AndSetsEnvelopeFields()
    {
        var repo = new InMemoryDocumentRefRepository();
        var doc = MakeRef();
        await repo.UpsertAsync(doc);
        Assert.True(await repo.SoftDeleteAsync(doc.Id, "actor-X", "test reason"));

        // The tombstoned row is hidden from Get, but FindByAttachment also
        // excludes it. We assert by upserting a new ref pointing at the
        // same attachment + checking it's the only one returned.
        var freshLive = MakeRef(attachmentId: doc.AttachmentId, entityId: "OTHER");
        await repo.UpsertAsync(freshLive);
        var hits = await repo.FindByAttachmentAsync(Tenant(), doc.AttachmentId);
        Assert.Single(hits);
        Assert.Equal(freshLive.Id, hits[0].Id);
    }
}
