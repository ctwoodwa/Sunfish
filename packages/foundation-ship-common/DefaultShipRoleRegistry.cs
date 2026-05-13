using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Thread-safe in-memory <see cref="IShipRoleRegistry"/> per ADR 0077 §1.1.
/// Registered by <c>AddSunfishSharedDesignSystem()</c>.
/// </summary>
public sealed class DefaultShipRoleRegistry : IShipRoleRegistry
{
    // Keyed by (baseRole, scopeKey) — one tenantLabel per scope per role.
    // Re-registering the same (baseRole, scope) with a different tenantLabel
    // overwrites per IShipRoleRegistry.AssignLabel contract.
    private readonly ConcurrentDictionary<ShipRole, ConcurrentDictionary<string, ShipRoleLabel>>
        _labels = new();

    /// <inheritdoc/>
    public void AssignLabel(ShipRole baseRole, string tenantLabel, ScopeRestriction? scope)
    {
        var byScope = _labels.GetOrAdd(
            baseRole,
            _ => new ConcurrentDictionary<string, ShipRoleLabel>(System.StringComparer.Ordinal));
        byScope[scope?.ScopeKey ?? string.Empty] = new ShipRoleLabel(baseRole, tenantLabel, scope);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ShipRoleLabel> LabelsFor(ShipRole baseRole)
    {
        if (_labels.TryGetValue(baseRole, out var byScope))
            return [.. byScope.Values];
        return [];
    }
}
