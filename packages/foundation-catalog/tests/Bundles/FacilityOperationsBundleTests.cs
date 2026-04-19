using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Catalog.Tests.Bundles;

public class FacilityOperationsBundleTests
{
    private const string ResourceName = "Bundles/facility-operations.bundle.json";

    [Fact]
    public void Manifest_loads_from_embedded_resource()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Equal("sunfish.bundles.facility-operations", manifest.Key);
        Assert.Equal("Facility Operations", manifest.Name);
        Assert.Equal(BundleCategory.Operations, manifest.Category);
    }

    [Fact]
    public void Manifest_requires_maintenance_inspections_scheduling_assets()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Contains("sunfish.blocks.maintenance", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.inspections", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.scheduling", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.assets", manifest.RequiredModules);
    }

    [Fact]
    public void Optional_modules_include_reservations_for_bookable_spaces()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Contains("sunfish.blocks.reservations", manifest.OptionalModules);
        Assert.Contains("sunfish.blocks.vendors", manifest.OptionalModules);
        Assert.Contains("sunfish.blocks.procurement", manifest.OptionalModules);
    }

    [Fact]
    public void Supports_all_three_deployment_modes()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Contains(DeploymentMode.Lite, manifest.DeploymentModesSupported);
        Assert.Contains(DeploymentMode.SelfHosted, manifest.DeploymentModesSupported);
        Assert.Contains(DeploymentMode.HostedSaaS, manifest.DeploymentModesSupported);
    }
}
