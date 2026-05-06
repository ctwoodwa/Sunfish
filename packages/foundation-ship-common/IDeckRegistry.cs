using System.Collections.Generic;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Registry of <see cref="DeckRegistration"/> records per ADR 0077 §3. UI
/// shells call <see cref="ForLocation"/> to enumerate the surfaces in a
/// location and <see cref="DefaultLandingDeck"/> to pick the per-role
/// default landing per §3.1.
/// </summary>
public interface IDeckRegistry
{
    /// <summary>
    /// Register a surface. Idempotent on
    /// <c>(<see cref="DeckRegistration.Location"/>, <see cref="DeckRegistration.SurfaceId"/>)</c>;
    /// re-registering an existing pair overwrites the prior entry.
    /// </summary>
    void Register(DeckRegistration registration);

    /// <summary>
    /// Returns all surfaces registered to <paramref name="location"/> in
    /// registration order. Caller-side sorting (by depth, by display name)
    /// is the consumer's responsibility.
    /// </summary>
    IReadOnlyList<DeckRegistration> ForLocation(ShipLocation location);

    /// <summary>
    /// Returns the deck depth that <paramref name="role"/> should land on
    /// when navigating to <paramref name="location"/> per §3.1. Defaults to
    /// <see cref="DeckDepth.MainDeck"/> when no specific landing is configured.
    /// </summary>
    DeckDepth DefaultLandingDeck(ShipRole role, ShipLocation location);
}
