using System.Collections.Concurrent;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Default in-memory <see cref="IFeatureProvider"/>. Holds per-key overrides
/// registered at startup; returns <c>null</c> for any key without an override.
/// Useful for tests, demos, and lite-mode deployments.
/// </summary>
public sealed class InMemoryFeatureProvider : IFeatureProvider
{
    private readonly ConcurrentDictionary<FeatureKey, FeatureValue> _overrides = new();

    /// <summary>Registers or replaces an override for a feature.</summary>
    public void SetOverride(FeatureKey key, FeatureValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _overrides[key] = value;
    }

    /// <summary>Clears any override for the given key. Returns true if one was removed.</summary>
    public bool ClearOverride(FeatureKey key) => _overrides.TryRemove(key, out _);

    /// <inheritdoc />
    public ValueTask<FeatureValue?> TryGetAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        _overrides.TryGetValue(key, out var value);
        return ValueTask.FromResult(value);
    }
}
