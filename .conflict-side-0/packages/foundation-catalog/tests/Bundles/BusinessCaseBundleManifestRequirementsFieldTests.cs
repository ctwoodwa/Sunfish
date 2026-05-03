using System.Text.Json;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Catalog.Tests.Bundles;

public sealed class BusinessCaseBundleManifestRequirementsFieldTests
{
    private static BusinessCaseBundleManifest NewManifest(MinimumSpec? requirements = null) => new()
    {
        Key = "sunfish.bundles.test",
        Name = "Test bundle",
        Version = "1.0.0",
        Requirements = requirements,
    };

    [Fact]
    public void Requirements_NullByDefault_PreservesBackwardCompat()
    {
        // A pre-A1 manifest construction (no Requirements set) leaves
        // the field null — the backward-compat path the addendum pins.
        var manifest = new BusinessCaseBundleManifest
        {
            Key = "k",
            Name = "n",
            Version = "1.0.0",
        };
        Assert.Null(manifest.Requirements);
    }

    [Fact]
    public void Requirements_NotNull_RoundTripsViaCanonicalJson()
    {
        var manifest = NewManifest(new MinimumSpec { Policy = SpecPolicy.Required });
        var bytes = CanonicalJson.Serialize(manifest);
        var roundtripped = JsonSerializer.Deserialize<BusinessCaseBundleManifest>(System.Text.Encoding.UTF8.GetString(bytes));
        Assert.NotNull(roundtripped);
        Assert.NotNull(roundtripped!.Requirements);
        Assert.Equal(SpecPolicy.Required, roundtripped.Requirements.Policy);
    }

    [Fact]
    public void Requirements_FieldNameInJson_IsCamelCase()
    {
        var manifest = NewManifest(new MinimumSpec { Policy = SpecPolicy.Recommended });
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(manifest));
        Assert.Contains("\"requirements\"", json);
        Assert.DoesNotContain("\"Requirements\"", json);
    }

    [Fact]
    public void Requirements_SpecPolicy_RoundTripsAsLiteralString()
    {
        var manifest = NewManifest(new MinimumSpec { Policy = SpecPolicy.Informational });
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(manifest));
        Assert.Contains("\"Informational\"", json);
        Assert.Contains("\"policy\"", json);
    }

    [Fact]
    public void Requirements_NullSerialization_OmitsField()
    {
        // JsonIgnoreCondition.WhenWritingNull means a null Requirements
        // value doesn't even appear in the serialized JSON.
        var manifest = NewManifest(requirements: null);
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(manifest));
        Assert.DoesNotContain("\"requirements\"", json);
    }

    [Fact]
    public void Validation_RequirementsAbsent_RoundTripsCleanly()
    {
        // Pre-A1 manifests deserialize correctly with Requirements=null.
        // The existing BusinessCaseBundleManifest fields don't carry
        // [JsonPropertyName] attrs, so the wire form is PascalCase.
        var preA1Json = """
        {
            "Key": "old-bundle",
            "Name": "Old bundle",
            "Version": "1.0.0"
        }
        """;
        var manifest = JsonSerializer.Deserialize<BusinessCaseBundleManifest>(preA1Json);
        Assert.NotNull(manifest);
        Assert.Null(manifest!.Requirements);
    }

    [Fact]
    public void Validation_RequirementsPresentWithValidPolicy_DeserializesAllThreeValues()
    {
        // SpecPolicy is constrained to 3 values per the stub; all three
        // must round-trip.
        foreach (var expected in new[] { SpecPolicy.Required, SpecPolicy.Recommended, SpecPolicy.Informational })
        {
            var manifest = NewManifest(new MinimumSpec { Policy = expected });
            var bytes = CanonicalJson.Serialize(manifest);
            var roundtripped = JsonSerializer.Deserialize<BusinessCaseBundleManifest>(System.Text.Encoding.UTF8.GetString(bytes));
            Assert.NotNull(roundtripped);
            Assert.Equal(expected, roundtripped!.Requirements!.Policy);
        }
    }

    [Fact]
    public void ForwardCompat_PreA1ManifestSerializedByPostA1Receiver_RoundTripsCleanly()
    {
        // Forward-compat: a pre-A1 manifest deserializes + reserializes
        // cleanly even though the post-A1 receiver knows about the new
        // Requirements field. The pre-existing BusinessCaseBundleManifest
        // properties use PascalCase wire form (no JsonPropertyName attr).
        var preA1Json = """
        {
            "Key": "k",
            "Name": "n",
            "Version": "1.0.0"
        }
        """;
        var manifest = JsonSerializer.Deserialize<BusinessCaseBundleManifest>(preA1Json);
        Assert.NotNull(manifest);
        var bytes = CanonicalJson.Serialize(manifest!);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        // The newly-introduced field is omitted (null + JsonIgnore.WhenWritingNull).
        Assert.DoesNotContain("\"requirements\"", json);
        // And re-deserialization round-trips with the same shape.
        var roundtripped = JsonSerializer.Deserialize<BusinessCaseBundleManifest>(json);
        Assert.NotNull(roundtripped);
        Assert.Null(roundtripped!.Requirements);
    }
}
