using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Anchor implementation of <see cref="IIntegrationAtlasContext"/> per ADR 0067 §6.
/// Resolves tenant + actor from the local-node session (single-operator posture).
/// </summary>
internal sealed class AnchorIntegrationAtlasContext : IIntegrationAtlasContext
{
    private readonly AnchorSessionService _session;
    private readonly IActiveTeamAccessor _activeTeam;

    public AnchorIntegrationAtlasContext(AnchorSessionService session, IActiveTeamAccessor activeTeam)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(activeTeam);
        _session = session;
        _activeTeam = activeTeam;
    }

    public TenantId CurrentTenantId =>
        _activeTeam.Active is { } team
            ? new TenantId(team.TeamId.Value.ToString("D"))
            : default;

    public ActorId CurrentActorId =>
        new ActorId(_session.NodeId ?? string.Empty);
}
