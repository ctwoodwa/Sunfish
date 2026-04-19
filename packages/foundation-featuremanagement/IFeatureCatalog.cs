using System.Diagnostics.CodeAnalysis;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Registry of declared features. Evaluating a feature absent from the
/// catalog is an error — prevents silent drift across modules.
/// </summary>
public interface IFeatureCatalog
{
    /// <summary>Registers a feature. Duplicate keys throw.</summary>
    void Register(FeatureSpec spec);

    /// <summary>Returns every registered spec, in registration order.</summary>
    IReadOnlyList<FeatureSpec> GetFeatures();

    /// <summary>Tries to resolve one spec by key.</summary>
    bool TryGetFeature(FeatureKey key, [NotNullWhen(true)] out FeatureSpec? spec);
}
