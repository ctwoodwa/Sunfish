using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Null-object <see cref="ITeamRegistry"/> that returns an empty membership list
/// for all queries.
/// Default registration until a real wallet/keystore workstream ships the production
/// implementation. Per ADR 0066 §Phase 3 stub-first pattern (W#55 P2d precedent).
/// </summary>
/// <remarks>
/// TODO: W#XX — replace with real wallet/keystore implementation reading from
/// kernel-security / kernel-runtime backing stores.
/// </remarks>
public sealed class NullTeamRegistry : ITeamRegistry
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<TeamMembership>> GetMembershipsAsync(
        ActorId actor,
        CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<TeamMembership>>(System.Array.Empty<TeamMembership>());
}
