using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Read-side access to all team memberships for an actor per ADR 0066 §Phase 3.
/// Distinct from <c>IActiveTeamAccessor</c> (which returns the CURRENT team only);
/// this registry returns the full membership set across all teams.
/// Real implementation ships in a future wallet/keystore workstream; until then
/// <see cref="NullTeamRegistry"/> is the default.
/// </summary>
public interface ITeamRegistry
{
    /// <summary>
    /// Returns all team memberships for <paramref name="actor"/>.
    /// Returns an empty list when the actor is not a member of any team.
    /// </summary>
    ValueTask<IReadOnlyList<TeamMembership>> GetMembershipsAsync(
        ActorId actor,
        CancellationToken ct = default);
}
