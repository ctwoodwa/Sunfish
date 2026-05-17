using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAp.Services;

/// <summary>
/// Computes AP aging snapshots — bucket totals + the contributing bill
/// rows. Three projection scopes: by chart (the whole AP book), by
/// vendor (vendor-specific drill-in), by property (rent-roll cost-
/// allocation). Mirrors AR's <c>IArAgingService</c>; <see cref="BillStatus.Disputed"/>
/// bills are naturally excluded since they don't satisfy <c>IsOpen()</c>.
/// </summary>
public interface IApAgingService
{
    /// <summary>Snapshot for every open bill in <paramref name="chartId"/>.</summary>
    Task<AgingSummary> GetAgingForChartAsync(
        ChartOfAccountsId chartId,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Snapshot for one vendor's open bills in <paramref name="chartId"/>.</summary>
    Task<AgingSummary> GetAgingForVendorAsync(
        ChartOfAccountsId chartId,
        PartyId vendorId,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Snapshot scoped to one property's open bills in <paramref name="chartId"/>.</summary>
    Task<AgingSummary> GetAgingForPropertyAsync(
        ChartOfAccountsId chartId,
        string propertyId,
        DateOnly asOf,
        CancellationToken cancellationToken = default);
}
