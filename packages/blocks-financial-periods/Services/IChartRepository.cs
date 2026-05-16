using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Chart-of-accounts lookup by id — needed by year-end close to read
/// the chart's <see cref="ChartOfAccounts.RetainedEarningsAccountId"/>
/// (the rollover target). Distinct from the ledger's
/// <c>IAccountResolver</c> (account-by-id) — this is the chart
/// envelope, not an account leaf.
/// </summary>
public interface IChartRepository
{
    /// <summary>Fetch a chart by id, or <c>null</c> when unknown.</summary>
    Task<ChartOfAccounts?> GetAsync(
        ChartOfAccountsId chartId,
        CancellationToken cancellationToken = default);
}
