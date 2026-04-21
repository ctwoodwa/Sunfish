using Sunfish.Blocks.BusinessCases.FeatureManagement;
using Sunfish.Blocks.BusinessCases.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.FeatureManagement;
using Xunit;

namespace Sunfish.Blocks.BusinessCases.Tests;

public class BundleEntitlementResolverTests
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

    private static BundleEntitlementResolver NewResolver(BundleCatalog catalog, InMemoryBundleActivationStore store)
    {
        var readSvc = new InMemoryBusinessCaseService(catalog, store);
        return new BundleEntitlementResolver(catalog, readSvc);
    }

    private static FeatureEvaluationContext ContextFor(TenantId? tenantId)
        => new() { TenantId = tenantId };

    [Fact]
    public async Task TryResolveAsync_ReturnsNull_WhenNoActiveBundle()
    {
        var (catalog, store) = NewFixture();
        var resolver = NewResolver(catalog, store);

        var value = await resolver.TryResolveAsync(
            FeatureKey.Of("leases.enabled"),
            ContextFor(new TenantId("tenant-a")));

        Assert.Null(value);
    }

    [Fact]
    public async Task TryResolveAsync_ReturnsNull_WhenNoTenant()
    {
        var (catalog, store) = NewFixture();
        var resolver = NewResolver(catalog, store);

        var value = await resolver.TryResolveAsync(
            FeatureKey.Of("leases.enabled"),
            ContextFor(null));

        Assert.Null(value);
    }

    [Fact]
    public async Task TryResolveAsync_ResolvesFeatureDefault()
    {
        var (catalog, store) = NewFixture();
        var prov = new InMemoryBundleProvisioningService(catalog, store);
        await prov.ProvisionAsync(new TenantId("tenant-a"), BundleKey, Edition);

        var resolver = NewResolver(catalog, store);

        var value = await resolver.TryResolveAsync(
            FeatureKey.Of("leases.enabled"),
            ContextFor(new TenantId("tenant-a")));

        Assert.NotNull(value);
        Assert.Equal("true", value!.Raw);
        Assert.True(value.AsBoolean());
    }

    [Fact]
    public async Task TryResolveAsync_ResolvesModuleTrue_WhenInEditionMapping()
    {
        var (catalog, store) = NewFixture();
        var prov = new InMemoryBundleProvisioningService(catalog, store);
        await prov.ProvisionAsync(new TenantId("tenant-a"), BundleKey, Edition);

        var resolver = NewResolver(catalog, store);

        var value = await resolver.TryResolveAsync(
            FeatureKey.Of("modules.sunfish.blocks.leases.enabled"),
            ContextFor(new TenantId("tenant-a")));

        Assert.NotNull(value);
        Assert.True(value!.AsBoolean());
    }

    [Fact]
    public async Task TryResolveAsync_ResolvesModuleTrue_WhenInRequiredModules()
    {
        var (catalog, store) = NewFixture();
        var prov = new InMemoryBundleProvisioningService(catalog, store);
        await prov.ProvisionAsync(new TenantId("tenant-a"), BundleKey, Edition);

        var resolver = NewResolver(catalog, store);

        var value = await resolver.TryResolveAsync(
            FeatureKey.Of("modules.sunfish.blocks.identity.enabled"),
            ContextFor(new TenantId("tenant-a")));

        Assert.NotNull(value);
        Assert.True(value!.AsBoolean());
    }

    [Fact]
    public async Task TryResolveAsync_ResolvesModuleFalse_WhenNotInEditionMapping()
    {
        var (catalog, store) = NewFixture();
        var prov = new InMemoryBundleProvisioningService(catalog, store);
        await prov.ProvisionAsync(new TenantId("tenant-a"), BundleKey, Edition);

        var resolver = NewResolver(catalog, store);

        var value = await resolver.TryResolveAsync(
            FeatureKey.Of("modules.sunfish.blocks.maintenance.enabled"),
            ContextFor(new TenantId("tenant-a")));

        Assert.NotNull(value);
        Assert.False(value!.AsBoolean());
    }

    [Fact]
    public async Task TryResolveAsync_ReturnsNull_ForUnmatchedFeatureKey()
    {
        var (catalog, store) = NewFixture();
        var prov = new InMemoryBundleProvisioningService(catalog, store);
        await prov.ProvisionAsync(new TenantId("tenant-a"), BundleKey, Edition);

        var resolver = NewResolver(catalog, store);

        var value = await resolver.TryResolveAsync(
            FeatureKey.Of("billing.max-invoices"),
            ContextFor(new TenantId("tenant-a")));

        Assert.Null(value);
    }
}
