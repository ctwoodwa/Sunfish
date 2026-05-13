using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Thread-safe in-memory <see cref="IDeckRegistry"/> per ADR 0077 §3.
/// Registered by <c>AddSunfishSharedDesignSystem()</c>.
/// </summary>
public sealed class DefaultDeckRegistry : IDeckRegistry
{
    private readonly ConcurrentDictionary<ShipLocation, ConcurrentDictionary<string, DeckRegistration>>
        _byLocation = new();

    /// <inheritdoc/>
    public void Register(DeckRegistration registration)
    {
        var byLocation = _byLocation.GetOrAdd(
            registration.Location,
            _ => new ConcurrentDictionary<string, DeckRegistration>());
        byLocation[registration.SurfaceId] = registration;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DeckRegistration> ForLocation(ShipLocation location)
    {
        if (_byLocation.TryGetValue(location, out var byLocation))
            return [.. byLocation.Values];
        return [];
    }

    /// <inheritdoc/>
    public DeckDepth DefaultLandingDeck(ShipRole role, ShipLocation location)
    {
        if (!_byLocation.TryGetValue(location, out var byLocation))
            return DeckDepth.MainDeck;

        foreach (var reg in byLocation.Values)
        {
            if (reg.DefaultLandingFor.Contains(role))
                return reg.Depth;
        }
        return DeckDepth.MainDeck;
    }
}
