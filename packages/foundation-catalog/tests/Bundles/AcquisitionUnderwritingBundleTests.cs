using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Catalog.Tests.Bundles;

public class AcquisitionUnderwritingBundleTests
{
    private const string ResourceName = "Bundles/acquisition-underwriting.bundle.json";

    [Fact]
    public void Manifest_loads_as_diligence_category()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Equal("sunfish.bundles.acquisition-underwriting", manifest.Key);
        Assert.Equal(BundleCategory.Diligence, manifest.Category);
    }

    [Fact]
    public void Optional_modules_include_crm_diligence_documents()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Contains("sunfish.blocks.crm", manifest.OptionalModules);
        Assert.Contains("sunfish.blocks.diligence", manifest.OptionalModules);
        Assert.Contains("sunfish.blocks.documents", manifest.OptionalModules);
    }

    [Fact]
    public void Lite_mode_is_intentionally_unsupported()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.DoesNotContain(DeploymentMode.Lite, manifest.DeploymentModesSupported);
        Assert.Contains(DeploymentMode.SelfHosted, manifest.DeploymentModesSupported);
        Assert.Contains(DeploymentMode.HostedSaaS, manifest.DeploymentModesSupported);
    }

    [Fact]
    public void Providers_include_storage_and_identity_for_data_room()
    {
        var manifest = BundleManifestLoader.LoadEmbedded(ResourceName);

        Assert.Contains(manifest.ProviderRequirements, r => r.Category == Sunfish.Foundation.Catalog.Bundles.ProviderCategory.Storage);
        Assert.Contains(manifest.ProviderRequirements, r => r.Category == Sunfish.Foundation.Catalog.Bundles.ProviderCategory.IdentityProvider);
    }
}
