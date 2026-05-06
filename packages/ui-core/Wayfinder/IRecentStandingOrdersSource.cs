using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// Cycle-safe seam exposing recent Standing Orders to ui-core
/// widgets. <c>ui-core</c> cannot reference
/// <c>foundation-wayfinder</c>'s
/// <c>IStandingOrderRepository</c> directly (cycle:
/// <c>ui-core → foundation-wayfinder → kernel-crdt → ui-core</c>),
/// so Phase 2 widgets consume this read-only abstraction. Phase 3
/// hosts wire a <c>foundation-wayfinder</c>-backed implementation
/// that projects <c>StandingOrder</c> records into
/// <see cref="RecentStandingOrderEntry"/> DTOs and (when
/// <c>IStandingOrderEventStream</c> is registered) invalidates the
/// per-actor cache reactively.
/// </summary>
/// <remarks>
/// <b>Side-effect-free contract:</b> implementations MUST be
/// projection-only — no mutations, no audit emission, no Standing
/// Order issuance. The Helm widget refresh tick fires every
/// <see cref="HelmOptions.PeriodicRefreshInterval"/>; the read path
/// must be cheap to call repeatedly.
/// </remarks>
public interface IRecentStandingOrdersSource
{
    /// <summary>
    /// Return the <paramref name="maxEntries"/> most-recent orders
    /// for the (<paramref name="tenantId"/>, <paramref name="actor"/>)
    /// pair, newest first. Implementations MAY return fewer entries
    /// than requested when the underlying repository has less
    /// history; they MUST NOT return more than
    /// <paramref name="maxEntries"/>.
    /// </summary>
    Task<IReadOnlyList<RecentStandingOrderEntry>> GetRecentAsync(
        TenantId tenantId,
        ActorId actor,
        int maxEntries,
        CancellationToken ct = default);
}
