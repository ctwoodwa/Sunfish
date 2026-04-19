using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>Default in-memory <see cref="IFeatureCatalog"/>. Safe for concurrent reads after startup.</summary>
public sealed class InMemoryFeatureCatalog : IFeatureCatalog
{
    private readonly ConcurrentDictionary<FeatureKey, FeatureSpec> _byKey = new();
    private readonly List<FeatureKey> _registrationOrder = new();
    private readonly object _orderLock = new();

    /// <inheritdoc />
    public void Register(FeatureSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!_byKey.TryAdd(spec.Key, spec))
        {
            throw new InvalidOperationException(
                $"Feature '{spec.Key}' is already registered.");
        }

        lock (_orderLock)
        {
            _registrationOrder.Add(spec.Key);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<FeatureSpec> GetFeatures()
    {
        lock (_orderLock)
        {
            return _registrationOrder.Select(k => _byKey[k]).ToArray();
        }
    }

    /// <inheritdoc />
    public bool TryGetFeature(FeatureKey key, [NotNullWhen(true)] out FeatureSpec? spec)
    {
        return _byKey.TryGetValue(key, out spec);
    }
}
