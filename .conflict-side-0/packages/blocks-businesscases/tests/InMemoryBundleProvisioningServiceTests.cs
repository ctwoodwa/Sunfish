using Sunfish.Blocks.BusinessCases.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Catalog.Bundles;
using Xunit;

namespace Sunfish.Blocks.BusinessCases.Tests;

public class InMemoryBundleProvisioningServiceTests
{
    private const string BundleKey = "sunfish.bundles.test";
    private const string Edition = "standard";

    private static (BundleCatalog Catalog, InMemoryBundleActivationStore Store) NewFixture()
    {
        var catalog = new BundleCatalog();
        catalog.Register(new BusinessCaseBundleManifest
        {
            Key = BundleKey,
            Name = "Test Bundle",
            Version = "0.1.0",
            FeatureDefaults = new Dictionary<string, string>
            {
                ["leases.enabled"] = "true",
                ["leases.max-count"] = "50"
            },
            EditionMappings = new Dictionary<string, IReadOnlyList<string>>
            {
                [Edition] = new[] { "sunfish.blocks.leases" },
                ["enterprise"] = new[] { "sunfish.blocks.leases", "sunfish.blocks.maintenance" }
            },
            RequiredModules = new[] { "sunfish.blocks.identity" }
        });
        return (catalog, new InMemoryBundleActivationStore());
    }

    [Fact]
    public async Task ProvisionAsync_HappyPath_RecordsActivation()
    {
        var (catalog, store) = NewFixture();
        var svc = new InMemoryBundleProvisioningService(catalog, store);

        var record = await svc.ProvisionAsync(new TenantId("tenant-a"), BundleKey, Edition);

        Assert.Equal(BundleKey, record.BundleKey);
        Assert.Equal(Edition, record.Edition);
        Assert.Equal("tenant-a", record.TenantId.Value);
        Assert.Null(record.DeactivatedAt);

        Assert.True(store.TryGet(new TenantId("tenant-a"), BundleKey, out var stored));
        Assert.NotNull(stored);
        Assert.Equal(record.Id, stored!.Id);
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenBundleUnknown()
    {
        var (catalog, store) = NewFixture();
        var svc = new InMemoryBundleProvisioningService(catalog, store);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProvisionAsync(new TenantId("tenant-a"), "nope", Edition).AsTask());
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenEditionNotDeclared()
    {
        var (catalog, store) = NewFixture();
        var svc = new InMemoryBundleProvisioningService(catalog, store);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProvisionAsync(new TenantId("tenant-a"), BundleKey, "platinum").AsTask());
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenAlreadyActive()
    {
        var (catalog, store) = NewFixture();
        var svc = new InMemoryBundleProvisioningService(catalog, store);

        await svc.ProvisionAsync(new TenantId("tenant-a"), BundleKey, Edition);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProvisionAsync(new TenantId("tenant-a"), BundleKey, Edition).AsTask());
    }

    [Fact]
    public async Task DeprovisionAsync_RemovesRecord()
    {
        var (catalog, store) = NewFixture();
        var svc = new InMemoryBundleProvisioningService(catalog, store);

        await svc.ProvisionAsync(new TenantId("tenant-a"), BundleKey, Edition);
        await svc.DeprovisionAsync(new TenantId("tenant-a"), BundleKey);

        Assert.False(store.TryGet(new TenantId("tenant-a"), BundleKey, out _));
    }

    [Fact]
    public async Task DeprovisionAsync_IsNoOp_WhenNoRecord()
    {
        var (catalog, store) = NewFixture();
        var svc = new InMemoryBundleProvisioningService(catalog, store);

        // Should not throw.
        await svc.DeprovisionAsync(new TenantId("tenant-a"), BundleKey);
    }
}
