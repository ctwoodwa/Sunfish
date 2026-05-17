using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class AttachmentOrphanReconcilerTests
{
    private static TenantId Tenant() => new("acme");

    /// <summary>
    /// Build the full graph + a reconciler with a zero grace period so
    /// every test-created attachment is immediately reap-eligible.
    /// </summary>
    private static (
        AttachmentOrphanReconciler reconciler,
        IAttachmentRepository attachments,
        IDocumentRefService docs)
        BuildGraph(TimeSpan? grace = null)
    {
        var attachments = new InMemoryAttachmentRepository();
        var refs = new InMemoryDocumentRefRepository();
        var docs = new DocumentRefService(refs, attachments);
        var reconciler = new AttachmentOrphanReconciler(attachments, docs, grace ?? TimeSpan.Zero);
        return (reconciler, attachments, docs);
    }

    private static async Task<Attachment> SeedActiveAttachmentAsync(
        IAttachmentRepository repo,
        TenantId? tenantId = null,
        Instant? createdAtUtc = null)
    {
        var a = Attachment.Create(
            tenantId: tenantId ?? Tenant(),
            storageRef: StorageRef.ForFoundationBlob($"cid-{Guid.NewGuid()}"),
            contentHash: Guid.NewGuid().ToString(),
            mimeType: "application/pdf",
            sizeBytes: 100,
            originalFilename: "x.pdf",
            createdBy: "u",
            createdAtUtc: createdAtUtc);
        await repo.UpsertAsync(a);
        return a;
    }

    [Fact]
    public async Task Reconcile_NoAttachments_ReturnsZero()
    {
        var (reconciler, _, _) = BuildGraph();
        Assert.Equal(0, await reconciler.ReconcileTenantAsync(Tenant(), "system"));
    }

    [Fact]
    public async Task Reconcile_ActiveAttachmentWithLiveLink_NotTombstoned()
    {
        var (reconciler, attachments, docs) = BuildGraph();
        var a = await SeedActiveAttachmentAsync(attachments);
        await docs.LinkAsync(Tenant(), a.Id, "blocks-financial-ar", "invoice", "INV-1", "u");

        var n = await reconciler.ReconcileTenantAsync(Tenant(), "system");
        Assert.Equal(0, n);
        // Attachment is still Active.
        var still = await attachments.GetAsync(a.Id);
        Assert.NotNull(still);
        Assert.Equal(AttachmentStatus.Active, still!.Status);
    }

    [Fact]
    public async Task Reconcile_OrphanActiveAttachment_Tombstoned()
    {
        var (reconciler, attachments, _) = BuildGraph();
        // Upload but never link.
        var a = await SeedActiveAttachmentAsync(attachments);

        var n = await reconciler.ReconcileTenantAsync(Tenant(), "system", reason: "test gc");
        Assert.Equal(1, n);
        // After tombstone, GetAsync returns null (hides Tombstoned rows).
        Assert.Null(await attachments.GetAsync(a.Id));
    }

    [Fact]
    public async Task Reconcile_AfterAllLinksTombstoned_OrphanReaped()
    {
        var (reconciler, attachments, docs) = BuildGraph();
        var a = await SeedActiveAttachmentAsync(attachments);
        await docs.LinkAsync(Tenant(), a.Id, "blocks-leases", "lease", "L-1", "u");

        // While linked, no reap.
        Assert.Equal(0, await reconciler.ReconcileTenantAsync(Tenant(), "system"));

        // Tombstone the link.
        await docs.UnlinkAsync(Tenant(), a.Id, "blocks-leases", "lease", "L-1", "u");

        // Now reaped.
        Assert.Equal(1, await reconciler.ReconcileTenantAsync(Tenant(), "system"));
        Assert.Null(await attachments.GetAsync(a.Id));
    }

    [Fact]
    public async Task Reconcile_GracePeriod_ProtectsRecentUploads()
    {
        // 1-hour grace; attachment created 30 minutes ago is too young.
        var (reconciler, attachments, _) = BuildGraph(grace: TimeSpan.FromHours(1));
        var thirtyMinAgo = new Instant(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(30));
        var young = await SeedActiveAttachmentAsync(attachments, createdAtUtc: thirtyMinAgo);

        Assert.Equal(0, await reconciler.ReconcileTenantAsync(Tenant(), "system"));
        // Attachment survives.
        Assert.NotNull(await attachments.GetAsync(young.Id));
    }

    [Fact]
    public async Task Reconcile_GracePeriod_ReapPastTheCutoff()
    {
        // 1-hour grace; attachment created 2 hours ago is past cutoff.
        var (reconciler, attachments, _) = BuildGraph(grace: TimeSpan.FromHours(1));
        var twoHoursAgo = new Instant(DateTimeOffset.UtcNow - TimeSpan.FromHours(2));
        var old = await SeedActiveAttachmentAsync(attachments, createdAtUtc: twoHoursAgo);

        Assert.Equal(1, await reconciler.ReconcileTenantAsync(Tenant(), "system"));
        Assert.Null(await attachments.GetAsync(old.Id));
    }

    [Fact]
    public async Task Reconcile_Idempotent()
    {
        var (reconciler, attachments, _) = BuildGraph();
        await SeedActiveAttachmentAsync(attachments);
        await SeedActiveAttachmentAsync(attachments);

        Assert.Equal(2, await reconciler.ReconcileTenantAsync(Tenant(), "system"));
        // Second pass: nothing left to reap.
        Assert.Equal(0, await reconciler.ReconcileTenantAsync(Tenant(), "system"));
    }

    [Fact]
    public async Task Reconcile_DoesNotTouchTombstonedOrSuperseded()
    {
        var (reconciler, attachments, _) = BuildGraph();
        // Manually create + immediately tombstone — should not appear in scan,
        // and the reconciler must not double-tombstone.
        var a = await SeedActiveAttachmentAsync(attachments);
        await attachments.SoftDeleteAsync(a.Id, "u", "manual");

        Assert.Equal(0, await reconciler.ReconcileTenantAsync(Tenant(), "system"));
    }

    [Fact]
    public async Task Reconcile_TenantScoped_DoesNotAffectOtherTenants()
    {
        var (reconciler, attachments, _) = BuildGraph();
        var aA = await SeedActiveAttachmentAsync(attachments, new TenantId("a"));
        var aB = await SeedActiveAttachmentAsync(attachments, new TenantId("b"));

        // Reconcile tenant A only. Only A's orphan should be reaped.
        Assert.Equal(1, await reconciler.ReconcileTenantAsync(new TenantId("a"), "system"));
        Assert.Null(await attachments.GetAsync(aA.Id));
        Assert.NotNull(await attachments.GetAsync(aB.Id));
    }

    [Fact]
    public void GracePeriod_DefaultsTo15Minutes()
    {
        var attachments = new InMemoryAttachmentRepository();
        var refs = new InMemoryDocumentRefRepository();
        var docs = new DocumentRefService(refs, attachments);
        var reconciler = new AttachmentOrphanReconciler(attachments, docs);
        Assert.Equal(TimeSpan.FromMinutes(15), reconciler.GracePeriod);
    }
}
