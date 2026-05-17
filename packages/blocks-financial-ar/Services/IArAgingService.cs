using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// Computes AR aging snapshots — bucket totals + the contributing
/// invoice rows. Stateless: every call recomputes from the
/// repository's current view, so a freshly-issued invoice is
/// reflected on the next call without invalidation plumbing.
///
/// <para>
/// Three projection scopes: by chart (the whole AR book), by
/// customer (collections workflow), by property (rent-roll
/// drill-in). Each returns an <see cref="AgingSummary"/> with
/// bucket totals + per-invoice rows in deterministic order.
/// </para>
/// </summary>
public interface IArAgingService
{
    /// <summary>Snapshot for every open invoice in <paramref name="chartId"/>.</summary>
    Task<AgingSummary> GetAgingForChartAsync(
        ChartOfAccountsId chartId,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Snapshot for one customer's open invoices in <paramref name="chartId"/>.</summary>
    Task<AgingSummary> GetAgingForCustomerAsync(
        ChartOfAccountsId chartId,
        PartyId customerId,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Snapshot scoped to one property's open invoices in <paramref name="chartId"/>.</summary>
    Task<AgingSummary> GetAgingForPropertyAsync(
        ChartOfAccountsId chartId,
        string propertyId,
        DateOnly asOf,
        CancellationToken cancellationToken = default);
}
