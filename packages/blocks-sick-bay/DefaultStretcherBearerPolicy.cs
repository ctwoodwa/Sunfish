using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SickBay;

namespace Sunfish.Blocks.SickBay;

/// <summary>
/// Reference <see cref="IStretcherBearerPolicy"/> per ADR 0082 §3 +
/// W#54 Phase 2. Returns the four canonical
/// <see cref="StretcherBearerRole"/> values unconditionally for v1;
/// tenant-override via Standing Order is deferred per ADR 0082 Open
/// Question §2.
/// </summary>
/// <remarks>
/// <b>§Trust:</b> the returned list is for notification routing /
/// display only. Authority decisions continue to flow through
/// <c>IPermissionResolver</c> with <see cref="Sunfish.Foundation.Ship.Common.ShipRole"/>
/// values. The constrained-enum surface is deliberate — it prevents
/// accidental role-escalation via this list.
/// </remarks>
internal sealed class DefaultStretcherBearerPolicy : IStretcherBearerPolicy
{
    private static readonly IReadOnlyList<StretcherBearerRole> AllRoles =
    [
        StretcherBearerRole.DCA,
        StretcherBearerRole.MPA,
        StretcherBearerRole.CommsOfficer,
        StretcherBearerRole.SonarOfficer,
    ];

    /// <inheritdoc />
    public Task<IReadOnlyList<StretcherBearerRole>> GetEligibleRespondersAsync(
        TenantId tenant,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(AllRoles);
    }
}
