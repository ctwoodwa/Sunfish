using Json.Schema;
using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Catalog.Tests.Bundles;

/// <summary>
/// Validates every embedded bundle manifest seed against the shipped
/// meta-schema. Catches manifest shape regressions before they reach the
/// loader, which is more forgiving.
/// </summary>
public class BundleManifestMetaSchemaTests
{
    private const string MetaSchemaResource = "Schemas/bundle-manifest.schema.json";

    public static IEnumerable<object[]> AllBundleResourceNames()
    {
        foreach (var name in BundleManifestLoader.ListEmbeddedBundleResourceNames())
        {
            yield return new object[] { name };
        }
    }

    [Theory]
    [MemberData(nameof(AllBundleResourceNames))]
    public void Every_shipped_bundle_manifest_validates_against_the_meta_schema(string resourceName)
    {
        var manifestJson = BundleManifestLoader.LoadEmbeddedText(resourceName);

        Assert.True(ValidateAgainstMetaSchema(manifestJson, out var _),
            $"Manifest '{resourceName}' failed meta-schema validation.");
    }

    [Fact]
    public void Meta_schema_rejects_manifest_missing_required_fields()
    {
        const string invalid = """{"name": "Missing key and version"}""";

        Assert.False(ValidateAgainstMetaSchema(invalid, out _));
    }

    [Fact]
    public void Meta_schema_rejects_manifest_with_invalid_key_pattern()
    {
        const string invalid = """{"key": "Not-A-Valid-Key", "name": "Bad", "version": "0.1.0"}""";

        Assert.False(ValidateAgainstMetaSchema(invalid, out _));
    }

    [Fact]
    public void Meta_schema_rejects_manifest_with_unknown_deployment_mode()
    {
        const string invalid = """
        {
          "key": "sunfish.bundles.test",
          "name": "Test",
          "version": "0.1.0",
          "deploymentModesSupported": ["Mainframe"]
        }
        """;

        Assert.False(ValidateAgainstMetaSchema(invalid, out _));
    }

    [Fact]
    public void Meta_schema_rejects_manifest_with_invalid_semver()
    {
        const string invalid = """{"key": "sunfish.bundles.test", "name": "Test", "version": "latest"}""";

        Assert.False(ValidateAgainstMetaSchema(invalid, out _));
    }

    /// <summary>
    /// Parses the meta-schema and evaluates the supplied document. Strips
    /// <c>$id</c> from the meta-schema before building to avoid the
    /// <c>JsonSchema.Net</c> global-registry collision across tests.
    /// </summary>
    private static bool ValidateAgainstMetaSchema(string documentJson, out string? error)
    {
        var metaText = BundleManifestLoader.LoadEmbeddedText(MetaSchemaResource);
        var metaObject = JsonNode.Parse(metaText)!.AsObject();
        metaObject.Remove("$id");
        var schema = JsonSchema.FromText(metaObject.ToJsonString());

        using var doc = JsonDocument.Parse(documentJson);
        var results = schema.Evaluate(doc.RootElement);

        error = results.IsValid ? null : "schema validation failed";
        return results.IsValid;
    }
}
