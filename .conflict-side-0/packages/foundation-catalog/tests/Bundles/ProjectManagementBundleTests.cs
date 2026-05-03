using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Catalog.Tests.Bundles;

/// <summary>
/// Locks in the shape of the project-management reference bundle.
/// Changes to the seed JSON must update these assertions consciously.
/// </summary>
public class ProjectManagementBundleTests
{
    private const string ResourceName = "Bundles/project-management.bundle.json";

    [Fact]
    public void Manifest_loads_from_embedded_resource()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Equal("sunfish.bundles.project-management", manifest.Key);
        Assert.Equal("Project Management", manifest.Name);
        Assert.Equal(BundleCategory.Operations, manifest.Category);
    }

    [Fact]
    public void Manifest_requires_only_existing_baseline_modules()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        // Required list is intentionally conservative: only modules that exist today.
        // Richer project features live in optionalModules until blocks-projects ships.
        Assert.Contains("sunfish.blocks.workflow", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.forms", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.tasks", manifest.RequiredModules);
        Assert.Contains("sunfish.blocks.scheduling", manifest.RequiredModules);
    }

    [Fact]
    public void Optional_modules_include_future_projects_module()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Contains("sunfish.blocks.projects", manifest.OptionalModules);
        Assert.Contains("sunfish.blocks.crm", manifest.OptionalModules);
        Assert.Contains("sunfish.blocks.reservations", manifest.OptionalModules);
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

        Assert.True(manifest.EditionMappings["lite"].Count < manifest.EditionMappings["standard"].Count);
        Assert.True(manifest.EditionMappings["standard"].Count < manifest.EditionMappings["enterprise"].Count);
    }

    [Fact]
    public void Manifest_registers_alongside_other_bundles()
    {
        var pm = BundleManifestLoader.LoadEmbedded("Bundles/property-management.bundle.json");
        var am = BundleManifestLoader.LoadEmbedded("Bundles/asset-management.bundle.json");
        var pr = BundleManifestLoader.LoadEmbedded("Bundles/project-management.bundle.json");
        var catalog = new BundleCatalog();

        catalog.Register(pm);
        catalog.Register(am);
        catalog.Register(pr);

        var keys = catalog.GetBundles().Select(b => b.Key).ToArray();

        Assert.Equal(3, keys.Length);
        Assert.Contains("sunfish.bundles.property-management", keys);
        Assert.Contains("sunfish.bundles.asset-management", keys);
        Assert.Contains("sunfish.bundles.project-management", keys);
    }
}
