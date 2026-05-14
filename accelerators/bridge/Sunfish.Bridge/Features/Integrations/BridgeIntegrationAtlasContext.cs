using Sunfish.Bridge.Client.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Bridge.Features.Integrations;

/// <summary>
/// Bridge implementation of <see cref="IIntegrationAtlasContext"/> per ADR 0067 §6.
/// Resolves tenant + actor from the per-circuit <see cref="IBridgeRequestContext"/>.
/// </summary>
public sealed class BridgeIntegrationAtlasContext : IIntegrationAtlasContext
{
    private readonly IBridgeRequestContext _requestContext;

    public BridgeIntegrationAtlasContext(IBridgeRequestContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        _requestContext = requestContext;
    }

    public TenantId CurrentTenantId => _requestContext.TenantId;

    public ActorId CurrentActorId => _requestContext.ActorId;
}
