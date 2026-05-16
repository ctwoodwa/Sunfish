using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Resolves a <see cref="FiscalPeriod"/> for a given chart + date.
/// Returns <c>null</c> if no period covers the date. Implementations
/// include the in-memory test fake (<see cref="InMemoryPeriodResolver"/>)
/// and the SQLite-backed production resolver that ships with
/// <c>blocks-financial-periods</c>.
/// </summary>
public interface IPeriodResolver
{
    /// <summary>
    /// Look up the period covering <paramref name="date"/> within the
    /// chart identified by <paramref name="chartId"/>.
    /// </summary>
    Task<FiscalPeriod?> ResolveAsync(
        ChartOfAccountsId chartId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory <see cref="IPeriodResolver"/>. By default returns an
/// always-Open period (for tests that don't care about period-gating).
/// Test setup can swap the returned status via
/// <see cref="WithStatus"/>.
/// </summary>
public sealed class InMemoryPeriodResolver : IPeriodResolver
{
    private FiscalPeriodStatus _status = FiscalPeriodStatus.Open;

    public InMemoryPeriodResolver WithStatus(FiscalPeriodStatus status)
    {
        _status = status;
        return this;
    }

    /// <inheritdoc />
    public Task<FiscalPeriod?> ResolveAsync(
        ChartOfAccountsId chartId,
        DateOnly date,
        CancellationToken cancellationToken = default)
        => Task.FromResult<FiscalPeriod?>(new FiscalPeriod(
            Id: FiscalPeriodId.NewId(),
            ChartId: chartId,
            StartDate: new DateOnly(date.Year, date.Month, 1),
            EndDate:   new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)),
            Status:    _status));
}

/// <summary>
/// <see cref="IPeriodResolver"/> that always returns <c>null</c> —
/// triggers <see cref="PostError.NoPeriodForDate"/> in the posting service.
/// </summary>
public sealed class NullPeriodResolver : IPeriodResolver
{
    /// <inheritdoc />
    public Task<FiscalPeriod?> ResolveAsync(
        ChartOfAccountsId chartId,
        DateOnly date,
        CancellationToken cancellationToken = default)
        => Task.FromResult<FiscalPeriod?>(null);
}
