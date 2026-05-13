using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Read-side identity profile for an actor per ADR 0066 §Phase 3.
/// Plain-text fields only — no field decryption (OQ-4 prohibition).
/// </summary>
/// <param name="ActorId">Stable actor identifier.</param>
/// <param name="DisplayName">Display name; may be null when not yet set.</param>
/// <param name="ContactEmail">Contact email; may be null when not yet set.</param>
/// <param name="PhoneNumber">Phone number; may be null when not yet set.</param>
public sealed record IdentityProfile(
    ActorId ActorId,
    string? DisplayName,
    string? ContactEmail,
    string? PhoneNumber);
