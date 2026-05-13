using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Team membership record returned by <see cref="ITeamRegistry"/> per ADR 0066 §Phase 3.
/// <see cref="TeamId"/> is the stable team identifier;
/// <see cref="SubkeyFingerprint"/> is the actor's team-specific sub-key fingerprint.
/// </summary>
/// <param name="TeamId">Stable team identifier (Guid-backed).</param>
/// <param name="DisplayName">Human-readable team display name.</param>
/// <param name="RoleDisplayName">Actor's role within this team (localized display string).</param>
/// <param name="SubkeyFingerprint">Fingerprint of the actor's team-scoped sub-key.</param>
public sealed record TeamMembership(
    TeamId TeamId,
    string DisplayName,
    string RoleDisplayName,
    KeyFingerprint SubkeyFingerprint);

/// <summary>
/// Strongly-typed team identifier per ADR 0066 §Phase 3 cycle-break decision.
/// Wraps a <see cref="System.Guid"/> to distinguish teams from other Guid-based identifiers
/// without importing <c>Sunfish.Kernel.Runtime.Teams</c> into the foundation tier.
/// </summary>
public readonly record struct TeamId(System.Guid Value)
{
    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
