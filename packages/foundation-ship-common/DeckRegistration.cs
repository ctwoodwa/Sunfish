using System.Collections.Generic;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Registration record for a deck-pane surface per ADR 0077 §3. Surfaces
/// declare their <see cref="Depth"/> at registration time; the Shared Design
/// System renders the surface conditionally based on
/// <see cref="IPermissionResolver.ResolveAsync"/> applied to
/// <see cref="PrimaryAction"/>.
/// </summary>
/// <param name="Location">The location this surface belongs to.</param>
/// <param name="Depth">The declared deck depth.</param>
/// <param name="SurfaceId">
/// Stable identifier for the deck-pane (e.g., <c>"engine-room.main-propulsion"</c>).
/// MUST be unique within <paramref name="Location"/>.
/// </param>
/// <param name="DisplayNameKey">Localization key for the surface's display name.</param>
/// <param name="PrimaryAction">
/// The action evaluated for visibility — when
/// <see cref="IPermissionResolver"/> denies this action for the subject, the
/// surface is hidden / replaced with a denial pane per the First-Aid contract.
/// </param>
/// <param name="DefaultLandingFor">
/// Roles whose default landing-deck is this surface per §3.1. Empty when this
/// surface is not anyone's default landing.
/// </param>
public sealed record DeckRegistration(
    ShipLocation Location,
    DeckDepth Depth,
    string SurfaceId,
    string DisplayNameKey,
    ShipAction PrimaryAction,
    IReadOnlyList<ShipRole> DefaultLandingFor);
