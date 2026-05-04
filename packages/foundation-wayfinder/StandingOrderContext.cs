using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Ambient context passed to every <see cref="IStandingOrderValidator"/> in the
/// chain. Carries enough information for validators to look up tenant policy,
/// issuer authority, and concurrent-issuance state without re-resolving from
/// scratch. Per ADR 0065 §3.
/// </summary>
/// <param name="TenantId">Tenant under which the order is being issued.</param>
/// <param name="IssuingActor">Actor performing the issuance; the authority validator uses this to consult the capability graph.</param>
public sealed record StandingOrderContext(TenantId TenantId, ActorId IssuingActor);
