using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>
/// Default in-memory implementation of <see cref="IBundleCatalog"/>. Safe
/// for concurrent reads after startup registration.
/// </summary>
public sealed class BundleCatalog : IBundleCatalog
{
    private readonly ConcurrentDictionary<string, BusinessCaseBundleManifest> _byKey
        = new(StringComparer.Ordinal);

    private readonly List<string> _registrationOrder = new();
    private readonly object _orderLock = new();

    /// <inheritdoc />
    public void Register(BusinessCaseBundleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (!_byKey.TryAdd(manifest.Key, manifest))
        {
            throw new InvalidOperationException(
                $"Bundle '{manifest.Key}' is already registered.");
        }

        lock (_orderLock)
        {
            _registrationOrder.Add(manifest.Key);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<BusinessCaseBundleManifest> GetBundles()
    {
        lock (_orderLock)
        {
            return _registrationOrder.Select(k => _byKey[k]).ToArray();
        }
    }

    /// <inheritdoc />
    public bool TryGet(string key, [NotNullWhen(true)] out BusinessCaseBundleManifest? manifest)
    {
        return _byKey.TryGetValue(key, out manifest);
    }
}
