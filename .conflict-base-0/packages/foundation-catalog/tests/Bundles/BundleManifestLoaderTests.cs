using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Catalog.Tests.Bundles;

public class BundleManifestLoaderTests
{
    [Fact]
    public void Parse_deserializes_minimal_manifest_with_defaults()
    {
        const string json = """
        {
          "key": "sunfish.bundles.sample",
          "name": "Sample",
          "version": "0.1.0"
        }
        """;

        var manifest = BundleManifestLoader.Parse(json);

        Assert.Equal("sunfish.bundles.sample", manifest.Key);
        Assert.Equal("Sample", manifest.Name);
        Assert.Equal("0.1.0", manifest.Version);
        Assert.Equal(BundleCategory.Operations, manifest.Category);
        Assert.Equal(BundleStatus.Draft, manifest.Status);
        Assert.Empty(manifest.RequiredModules);
        Assert.Empty(manifest.OptionalModules);
        Assert.Empty(manifest.FeatureDefaults);
        Assert.Empty(manifest.EditionMappings);
        Assert.Empty(manifest.DeploymentModesSupported);
        Assert.Empty(manifest.ProviderRequirements);
    }

    [Fact]
    public void Parse_deserializes_enums_as_strings()
    {
        const string json = """
        {
          "key": "k", "name": "n", "version": "0.1.0",
          "category": "Finance",
          "status": "Preview",
          "deploymentModesSupported": ["Lite", "HostedSaaS"]
        }
        """;

        var manifest = BundleManifestLoader.Parse(json);

        Assert.Equal(BundleCategory.Finance, manifest.Category);
        Assert.Equal(BundleStatus.Preview, manifest.Status);
        Assert.Equal(new[] { DeploymentMode.Lite, DeploymentMode.HostedSaaS }, manifest.DeploymentModesSupported);
    }

    [Fact]
    public void Parse_deserializes_nested_edition_mappings()
    {
        const string json = """
        {
          "key": "k", "name": "n", "version": "0.1.0",
          "editionMappings": {
            "lite": ["a", "b"],
            "enterprise": ["a", "b", "c"]
          }
        }
        """;

        var manifest = BundleManifestLoader.Parse(json);

        Assert.Equal(2, manifest.EditionMappings.Count);
        Assert.Equal(new[] { "a", "b" }, manifest.EditionMappings["lite"]);
        Assert.Equal(new[] { "a", "b", "c" }, manifest.EditionMappings["enterprise"]);
    }

    [Fact]
    public void Parse_deserializes_provider_requirements()
    {
        const string json = """
        {
          "key": "k", "name": "n", "version": "0.1.0",
          "providerRequirements": [
            { "category": "Payments", "required": true, "purpose": "rent" },
            { "category": "ChannelManager", "required": false }
          ]
        }
        """;

        var manifest = BundleManifestLoader.Parse(json);

        Assert.Equal(2, manifest.ProviderRequirements.Count);
        Assert.Equal(ProviderCategory.Payments, manifest.ProviderRequirements[0].Category);
        Assert.True(manifest.ProviderRequirements[0].Required);
        Assert.Equal("rent", manifest.ProviderRequirements[0].Purpose);
        Assert.Equal(ProviderCategory.ChannelManager, manifest.ProviderRequirements[1].Category);
        Assert.False(manifest.ProviderRequirements[1].Required);
    }

    [Fact]
    public void ListEmbeddedBundleResourceNames_finds_at_least_the_property_management_seed()
    {
        var names = BundleManifestLoader.ListEmbeddedBundleResourceNames();

        Assert.Contains("Bundles/property-management.bundle.json", names);
    }

    [Fact]
    public void LoadEmbedded_throws_FileNotFound_for_unknown_resource()
    {
        Assert.Throws<FileNotFoundException>(
            () => BundleManifestLoader.LoadEmbedded("Bundles/does-not-exist.bundle.json"));
    }
}
