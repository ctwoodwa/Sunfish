using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Thread-safe in-memory <see cref="IShipRoleRegistry"/> per ADR 0077 §1.1.
/// Registered by <c>AddSunfishSharedDesignSystem()</c>.
/// </summary>
public sealed class DefaultShipRoleRegistry : IShipRoleRegistry
{
    private readonly ConcurrentDictionary<ShipRole, ConcurrentBag<ShipRoleLabel>> _labels = new();

    /// <inheritdoc/>
    public void AssignLabel(ShipRole baseRole, string tenantLabel, ScopeRestriction? scope)
    {
        var bag = _labels.GetOrAdd(baseRole, _ => new ConcurrentBag<ShipRoleLabel>());
        foreach (var existing in bag)
        {
            if (existing.TenantLabel == tenantLabel && existing.Scope?.ScopeKey == scope?.ScopeKey)
                return; // idempotent: same triple already registered
        }
        bag.Add(new ShipRoleLabel(baseRole, tenantLabel, scope));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ShipRoleLabel> LabelsFor(ShipRole baseRole)
    {
        if (_labels.TryGetValue(baseRole, out var bag))
            return [.. bag];
        return [];
    }
}
