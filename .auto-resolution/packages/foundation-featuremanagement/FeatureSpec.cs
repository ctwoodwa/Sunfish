namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Catalog entry describing a known feature. Evaluating a feature not
/// registered in <see cref="IFeatureCatalog"/> is a hard error.
/// </summary>
public sealed record FeatureSpec
{
    /// <summary>Feature identifier.</summary>
    public required FeatureKey Key { get; init; }

    /// <summary>Declared value-type.</summary>
    public required FeatureValueKind Kind { get; init; }

    /// <summary>Raw default value, used when no provider or entitlement resolves the feature.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional module or bundle key that owns this feature declaration.</summary>
    public string? OwnerKey { get; init; }
}
