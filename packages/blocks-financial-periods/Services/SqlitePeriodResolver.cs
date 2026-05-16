using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Production <see cref="IPeriodResolver"/> for the Anchor native
/// financial domain. Resolves the period covering a chart + date via
/// <see cref="IFiscalPeriodRepository"/> and projects the result to the
/// ledger's minimal <see cref="IPeriodResolver.PeriodSnapshot"/> shape.
/// </summary>
/// <remarks>
/// The "Sqlite" prefix denotes the production storage substrate that
/// the injected <see cref="IFiscalPeriodRepository"/> implementation
/// uses (Microsoft.Data.Sqlite, per Stage 02 §6.5(a)); the resolver
/// itself depends only on the repository abstraction so tests can
/// substitute <see cref="InMemoryFiscalPeriodRepository"/>.
/// </remarks>
public sealed class SqlitePeriodResolver : IPeriodResolver
{
    private readonly IFiscalPeriodRepository _periods;

    public SqlitePeriodResolver(IFiscalPeriodRepository periods)
    {
        _periods = periods ?? throw new ArgumentNullException(nameof(periods));
    }

    /// <inheritdoc />
    public async Task<IPeriodResolver.PeriodSnapshot?> ResolveAsync(
        ChartOfAccountsId chartId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var period = await _periods.FindByChartAndDateAsync(chartId, date, cancellationToken).ConfigureAwait(false);
        if (period is null) return null;

        return new IPeriodResolver.PeriodSnapshot(
            PeriodId: period.Id.Value,
            ChartId:  period.ChartId.Value,
            Status:   period.Status switch
            {
                FiscalPeriodStatus.Open       => IPeriodResolver.Status.Open,
                FiscalPeriodStatus.SoftClosed => IPeriodResolver.Status.SoftClosed,
                FiscalPeriodStatus.Locked     => IPeriodResolver.Status.Locked,
                // Fail-closed on an unknown enum case — silently
                // reporting Open would open the posting-gate for a
                // future status the ledger doesn't know how to handle.
                _                             => throw new InvalidOperationException(
                    $"Unhandled FiscalPeriodStatus '{period.Status}' for period {period.Id.Value}; "
                    + "SqlitePeriodResolver.Status switch must be extended to cover the new case."),
            });
    }
}
