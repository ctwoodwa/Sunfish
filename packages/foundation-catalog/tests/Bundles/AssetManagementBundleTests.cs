using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Catalog.Tests.Bundles;

/// <summary>
/// Locks in the shape of the asset-management reference bundle.
/// Changes to the seed JSON must update these assertions consciously.
/// </summary>
public class AssetManagementBundleTests
{
    private const string ResourceName = "Bundles/asset-management.bundle.json";

    [Fact]
    public void Manifest_loads_from_embedded_resource()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Equal("sunfish.bundles.asset-management", manifest.Key);
        Assert.Equal("Asset Management", manifest.Name);
        Assert.Equal(BundleCategory.Operations, manifest.Category);
        Assert.Equal(BundleStatus.Draft, manifest.Status);
    }

    [Fact]
    public void Manifest_requires_existing_asset_tracking_modules()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Contains("sunfish.blocks.assets", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.maintenance", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.inspections", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.scheduling", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.tasks", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.workflow", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.forms", manifest.RequiredModules);
    }

    [Fact]
    public void Manifest_supports_all_three_deployment_modes()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Contains(DeploymentMode.Lite, manifest.DeploymentModesSupported);
        Assert.Contains(DeploymentMode.SelfHosted, manifest.DeploymentModesSupported);
        Assert.Contains(DeploymentMode.HostedSaaS, manifest.DeploymentModesSupported);
    }

    [Fact]
    public void Editions_progress_lite_to_enterprise()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.True(manifest.EditionMappings.ContainsKey("lite"));
        Assert.True(manifest.EditionMappings.ContainsKey("standard"));
        Assert.True(manifest.EditionMappings.ContainsKey("enterprise"));
        Assert.True(manifest.EditionMappings["lite"].Count < manifest.EditionMappings["standard"].Count);
        Assert.True(manifest.EditionMappings["standard"].Count < manifest.EditionMappings["enterprise"].Count);
    }

    [Fact]
    public void Provider_requirements_are_optional_in_draft()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.NotEmpty(manifest.ProviderRequirements);
        Assert.All(manifest.ProviderRequirements, req => Assert.False(req.Required));
    }

    [Fact]
    public void Manifest_registers_into_BundleCatalog()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);
        var catalog = new BundleCatalog();

        catalog.Register(manifest);

        Assert.True(catalog.TryGet("sunfish.bundles.asset-management", out var resolved));
        Assert.Same(manifest, resolved);
    }
}
