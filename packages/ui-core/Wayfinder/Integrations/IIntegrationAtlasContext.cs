using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Ambient tenant + actor context for Atlas Integration-Config calls
/// per ADR 0067. Hosts register a concrete implementation against DI:
/// Bridge wires it scoped via the HttpContext per-request principal;
/// Anchor wires it as a singleton bound to the local-node identity.
/// </summary>
/// <remarks>
/// Cycle-safe: references only <see cref="TenantId"/> + <see cref="ActorId"/>
/// from <c>Sunfish.Foundation.Assets.Common</c>, both of which
/// <c>ui-core</c> already pulls in via its <c>foundation</c>
/// project-reference.
/// </remarks>
public interface IIntegrationAtlasContext
{
    /// <summary>Tenant the current request is scoped to.</summary>
    TenantId CurrentTenantId { get; }

    /// <summary>Actor invoking the operation.</summary>
    ActorId CurrentActorId { get; }
}
