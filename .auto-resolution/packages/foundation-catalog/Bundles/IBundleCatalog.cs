using System.Diagnostics.CodeAnalysis;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>
/// Registry of <see cref="BusinessCaseBundleManifest"/> instances available
/// to the running host. Bridge's provisioning service consumes this catalog
/// to resolve tenant bundle selections into active modules and features.
/// </summary>
public interface IBundleCatalog
{
    /// <summary>Registers a bundle manifest. Duplicate keys throw.</summary>
    void Register(BusinessCaseBundleManifest manifest);

    /// <summary>Returns every registered manifest, in registration order.</summary>
    IReadOnlyList<BusinessCaseBundleManifest> GetBundles();

    /// <summary>Tries to resolve one manifest by its key.</summary>
    bool TryGet(string key, [NotNullWhen(true)] out BusinessCaseBundleManifest? manifest);
}
