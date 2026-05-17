using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class InMemoryAttachmentRepositoryTests
{
    private static TenantId Tenant() => new("acme");
    private static StorageRef Foundation() => StorageRef.ForFoundationBlob("bafy-test");

    private static Attachment NewAttachment(TenantId? tenantId = null, string hash = "abc123") =>
        Attachment.Create(
            tenantId: tenantId ?? Tenant(),
            storageRef: Foundation(),
            contentHash: hash,
            mimeType: "application/pdf",
            sizeBytes: 100,
            originalFilename: "file.pdf",
            createdBy: "user-1");

    [Fact]
    public async Task Upsert_RoundtripsViaGet()
    {
        var repo = new InMemoryAttachmentRepository();
        var a = NewAttachment();
        await repo.UpsertAsync(a);

        var fetched = await repo.GetAsync(a.Id);
        Assert.NotNull(fetched);
        Assert.Equal(a.Id, fetched!.Id);
    }

    [Fact]
    public async Task Get_OnTombstoned_ReturnsNull()
    {
        var repo = new InMemoryAttachmentRepository();
        var a = NewAttachment();
        await repo.UpsertAsync(a);
        await repo.SoftDeleteAsync(a.Id, "user-1", "cleanup");

        Assert.Null(await repo.GetAsync(a.Id));
    }

    [Fact]
    public async Task FindByContentHash_FiltersByTenantAndExcludesTombstones()
    {
        var repo = new InMemoryAttachmentRepository();
        var tenA = new TenantId("a");
        var tenB = new TenantId("b");
        var a1 = NewAttachment(tenA, "shared-hash");
        var a2 = NewAttachment(tenA, "different-hash");
        var b1 = NewAttachment(tenB, "shared-hash");
        await repo.UpsertAsync(a1);
        await repo.UpsertAsync(a2);
        await repo.UpsertAsync(b1);

        var aResults = await repo.FindByContentHashAsync(tenA, "shared-hash");
        Assert.Single(aResults);
        Assert.Equal(a1.Id, aResults[0].Id);

        await repo.SoftDeleteAsync(a1.Id, "u", null);
        var aResultsAfterDelete = await repo.FindByContentHashAsync(tenA, "shared-hash");
        Assert.Empty(aResultsAfterDelete);
    }

    [Fact]
    public async Task ListByTenant_FiltersAndExcludesTombstones()
    {
        var repo = new InMemoryAttachmentRepository();
        var tenA = new TenantId("a");
        var tenB = new TenantId("b");
        await repo.UpsertAsync(NewAttachment(tenA, "h1"));
        await repo.UpsertAsync(NewAttachment(tenA, "h2"));
        await repo.UpsertAsync(NewAttachment(tenB, "h3"));
        var dead = NewAttachment(tenA, "h-dead");
        await repo.UpsertAsync(dead);
        await repo.SoftDeleteAsync(dead.Id, "u", null);

        var aResults = await repo.ListByTenantAsync(tenA);
        Assert.Equal(2, aResults.Count);
    }

    [Fact]
    public async Task SoftDelete_FlipsStatusToTombstoned_AndIsIdempotent()
    {
        var repo = new InMemoryAttachmentRepository();
        var a = NewAttachment();
        await repo.UpsertAsync(a);

        Assert.True(await repo.SoftDeleteAsync(a.Id, "u", "first"));
        Assert.True(await repo.SoftDeleteAsync(a.Id, "u", "second")); // idempotent

        // The internal record exists but is excluded from Get/List.
        Assert.Null(await repo.GetAsync(a.Id));
    }

    [Fact]
    public async Task SoftDelete_UnknownId_ReturnsFalse()
    {
        var repo = new InMemoryAttachmentRepository();
        Assert.False(await repo.SoftDeleteAsync(AttachmentId.NewId(), "u", null));
    }

    [Fact]
    public async Task Upsert_OnTombstoned_Throws()
    {
        var repo = new InMemoryAttachmentRepository();
        var a = NewAttachment();
        await repo.UpsertAsync(a);
        await repo.SoftDeleteAsync(a.Id, "u", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.UpsertAsync(a with { OriginalFilename = "renamed.pdf" }));
    }
}
