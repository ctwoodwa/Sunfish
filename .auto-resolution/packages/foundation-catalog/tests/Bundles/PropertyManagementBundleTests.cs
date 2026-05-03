using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Catalog.Tests.Bundles;

/// <summary>
/// Locks in the shape of the first reference bundle shipped with
/// <c>Sunfish.Foundation.Catalog</c>. Changes to the seed JSON must
/// update these assertions consciously.
/// </summary>
public class PropertyManagementBundleTests
{
    private const string ResourceName = "Bundles/property-management.bundle.json";

    [Fact]
    public void Manifest_loads_from_embedded_resource()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Equal("sunfish.bundles.property-management", manifest.Key);
        Assert.Equal("Property Management", manifest.Name);
        Assert.Equal("0.1.0", manifest.Version);
        Assert.Equal(BundleCategory.Operations, manifest.Category);
        Assert.Equal(BundleStatus.Draft, manifest.Status);
    }

    [Fact]
    public void Manifest_required_modules_match_existing_blocks_packages()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Contains("sunfish.blocks.leases", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.rent-collection", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.maintenance", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.inspections", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.scheduling", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.tasks", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.workflow", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.forms", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.accounting", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.tax-reporting", manifest.RequiredModules);
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
    public void Manifest_declares_edition_mappings_for_lite_standard_enterprise()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.True(manifest.EditionMappings.ContainsKey("lite"));
        Assert.True(manifest.EditionMappings.ContainsKey("standard"));
        Assert.True(manifest.EditionMappings.ContainsKey("enterprise"));
        Assert.True(manifest.EditionMappings["lite"].Count < manifest.EditionMappings["standard"].Count);
        Assert.True(manifest.EditionMappings["standard"].Count < manifest.EditionMappings["enterprise"].Count);
    }

    [Fact]
    public void Manifest_provider_requirements_are_all_optional_in_draft()
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

        Assert.True(catalog.TryGet("sunfish.bundles.property-management", out var resolved));
        Assert.Same(manifest, resolved);
    }
}
