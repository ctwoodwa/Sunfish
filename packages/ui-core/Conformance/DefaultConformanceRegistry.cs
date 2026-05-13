using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sunfish.UICore.Conformance;

/// <summary>
/// Thread-safe in-memory <see cref="IConformanceRegistry"/> per ADR 0077 §7.
/// Registered by <c>AddSunfishSharedDesignSystem()</c> in
/// <c>Sunfish.Foundation.Ship.Common.ShipDesignSystemServiceExtensions</c>.
/// </summary>
public sealed class DefaultConformanceRegistry : IConformanceRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConformanceDeclaration>>
        _byLocation = new(System.StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Register(ConformanceDeclaration declaration)
    {
        var byLocation = _byLocation.GetOrAdd(
            declaration.LocationId,
            _ => new ConcurrentDictionary<string, ConformanceDeclaration>());
        byLocation[declaration.SurfaceId] = declaration;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ConformanceDeclaration> ForLocation(string locationId)
    {
        if (_byLocation.TryGetValue(locationId, out var byLocation))
            return [.. byLocation.Values];
        return [];
    }
}
