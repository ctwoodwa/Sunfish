using System;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Team membership record returned by <see cref="ITeamRegistry"/> per ADR 0066 §Phase 3.
/// <see cref="TeamId"/> is a <see cref="Guid"/> per the cycle-break decision documented in
/// <c>ActiveTeamOverviewViewModel</c> in <c>Sunfish.UICore.Wayfinder</c> —
/// <c>kernel-runtime</c> already references <c>ui-core</c>, so foundation packages
/// MUST NOT reference <c>kernel-runtime.Teams.TeamId</c> to avoid a circular dependency.
/// Consumers in kernel-runtime / accelerators wrap the Guid back into
/// <c>Sunfish.Kernel.Runtime.Teams.TeamId</c> at the boundary.
/// </summary>
/// <param name="TeamId">Team identifier as a raw Guid (cycle-break).</param>
/// <param name="DisplayName">Human-readable team display name.</param>
/// <param name="RoleDisplayName">Actor's role within this team (localized display string).</param>
/// <param name="SubkeyFingerprint">Fingerprint of the actor's team-scoped sub-key.</param>
public sealed record TeamMembership(
    Guid TeamId,
    string DisplayName,
    string RoleDisplayName,
    KeyFingerprint SubkeyFingerprint);
