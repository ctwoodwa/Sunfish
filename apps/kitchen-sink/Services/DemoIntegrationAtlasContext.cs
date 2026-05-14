using Sunfish.Foundation.Assets.Common;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.KitchenSink.Services;

/// <summary>
/// Demo-only IIntegrationAtlasContext for the kitchen-sink integration atlas showcase.
/// Returns fixed demo identifiers — not for production use.
/// </summary>
internal sealed class DemoIntegrationAtlasContext : IIntegrationAtlasContext
{
    public TenantId CurrentTenantId => new TenantId("demo-tenant");
    public ActorId CurrentActorId => new ActorId("demo-actor");
}
