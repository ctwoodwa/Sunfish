using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Resolves eligible stretcher-bearer responders for a tenant per ADR
/// 0082 §3. Returns <see cref="StretcherBearerRole"/> values
/// (intentionally a constrained subset, NOT
/// <see cref="Sunfish.Foundation.Ship.Common.ShipRole"/>) so the
/// notification-routing list cannot be misused as an authority list.
/// </summary>
/// <remarks>
/// <b>§Trust:</b> the result list MUST NOT be consumed for
/// permission/authority decisions — only for notification routing /
/// display. Authority continues to flow through
/// <c>IPermissionResolver</c> with <see cref="Sunfish.Foundation.Ship.Common.ShipRole"/>
/// values.
/// </remarks>
public interface IStretcherBearerPolicy
{
    /// <summary>
    /// Returns the eligible responders for the tenant's medevac flow.
    /// Per W#54 P1 council Minor m2: order is NOT significant; an empty
    /// list indicates no eligible responders are currently registered,
    /// in which case the caller MUST surface a fallback path (e.g.,
    /// fall back to direct Captain notification or the W#46 §Trust
    /// escalation surface).
    /// </summary>
    Task<IReadOnlyList<StretcherBearerRole>> GetEligibleRespondersAsync(
        TenantId tenant,
        CancellationToken ct = default);
}
