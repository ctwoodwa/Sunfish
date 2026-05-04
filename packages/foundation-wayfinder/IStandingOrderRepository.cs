using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Per-tenant Standing Order log accessor. Per ADR 0065 §1 / §2.
/// </summary>
/// <remarks>
/// Phase 1 ships only the contract; the CRDT-backed implementation
/// (<c>CrdtStandingOrderRepository</c>) lands in Phase 2 and materializes the
/// log into a per-tenant Loro document at <c>wayfinder/standing-orders/{tenantId}</c>
/// per ADR 0065 §2.
/// </remarks>
public interface IStandingOrderRepository
{
    /// <summary>
    /// Persist a Standing Order into the per-tenant log. Idempotent on
    /// <see cref="StandingOrder.Id"/>: re-appending the same id is a no-op.
    /// </summary>
    /// <param name="order">The order to append.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask AppendAsync(StandingOrder order, CancellationToken ct);

    /// <summary>
    /// Retrieve a Standing Order by id within a tenant.
    /// </summary>
    /// <param name="tenantId">Tenant under which to search.</param>
    /// <param name="id">Identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching order, or <c>null</c> when no order exists with that id under the tenant.</returns>
    ValueTask<StandingOrder?> GetAsync(TenantId tenantId, StandingOrderId id, CancellationToken ct);

    /// <summary>
    /// Stream every Standing Order ever issued in the per-tenant log, in
    /// issuance order. Used by Atlas projection (Phase 3a) and audit replay.
    /// </summary>
    /// <param name="tenantId">Tenant to enumerate.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<StandingOrder> EnumerateAsync(TenantId tenantId, CancellationToken ct);
}
