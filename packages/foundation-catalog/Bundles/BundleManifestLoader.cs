using System.Text.Json;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>
/// Loads <see cref="BusinessCaseBundleManifest"/> instances from JSON text
/// or from embedded resources shipped with this assembly.
/// </summary>
public static class BundleManifestLoader
{
    /// <summary>Parses a bundle manifest from JSON text.</summary>
    public static BusinessCaseBundleManifest Parse(string json)
    {
        return JsonSerializer.Deserialize<BusinessCaseBundleManifest>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Bundle manifest JSON deserialized to null.");
    }

    /// <summary>
    /// Loads a bundle manifest from an embedded JSON resource inside
    /// <c>Sunfish.Foundation.Catalog</c>. The <paramref name="logicalName"/>
    /// matches the <c>LogicalName</c> declared in the csproj
    /// (e.g. <c>Bundles/property-management.bundle.json</c>).
    /// </summary>
    public static BusinessCaseBundleManifest LoadEmbedded(string logicalName)
        => Parse(LoadEmbeddedText(logicalName));

    /// <summary>
    /// Reads the text of an embedded resource shipped with
    /// <c>Sunfish.Foundation.Catalog</c>. Used for bundle manifests, the
    /// bundle meta-schema, and similar seed content.
    /// </summary>
    public static string LoadEmbeddedText(string logicalName)
    {
        var assembly = typeof(BundleManifestLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new FileNotFoundException(
                $"Embedded resource '{logicalName}' not found in {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Enumerates the logical names of every embedded bundle manifest resource
    /// shipped with <c>Sunfish.Foundation.Catalog</c>.
    /// </summary>
    public static IReadOnlyList<string> ListEmbeddedBundleResourceNames()
    {
        var assembly = typeof(BundleManifestLoader).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("Bundles/", StringComparison.Ordinal)
                     && n.EndsWith(".bundle.json", StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>Serializer options used for manifest deserialization; exposed for tests.</summary>
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
