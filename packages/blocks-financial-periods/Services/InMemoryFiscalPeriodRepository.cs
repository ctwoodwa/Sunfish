using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// In-memory <see cref="IFiscalPeriodRepository"/> for tests, the
/// kitchen-sink demo, and ERPNext migration dry-runs. Production
/// callers wire a SQLite-backed implementation (lands with the local-
/// node persistence wiring).
/// </summary>
public sealed class InMemoryFiscalPeriodRepository : IFiscalPeriodRepository
{
    private readonly ConcurrentDictionary<FiscalPeriodId, FiscalPeriod> _byId = new();
    private readonly ConcurrentDictionary<string, FiscalPeriodId> _byExternalRef = new();

    /// <inheritdoc />
    public Task<FiscalPeriod?> GetAsync(FiscalPeriodId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id, out var p) ? p : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<FiscalPeriod>> GetByFiscalYearAsync(
        FiscalYearId fiscalYearId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FiscalPeriod> rows = _byId.Values
            .Where(p => p.FiscalYearId.Equals(fiscalYearId))
            .OrderBy(p => p.StartDate)
            .ToList();
        return Task.FromResult(rows);
    }

    /// <inheritdoc />
    public Task<FiscalPeriod?> FindByChartAndDateAsync(
        ChartOfAccountsId chartId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var match = _byId.Values.FirstOrDefault(p =>
            p.ChartId.Equals(chartId) && p.Contains(date));
        return Task.FromResult(match);
    }

    /// <inheritdoc />
    public Task InsertAsync(FiscalPeriod period, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);
        if (!_byId.TryAdd(period.Id, period))
            throw new InvalidOperationException(
                $"FiscalPeriod {period.Id.Value} already exists.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(FiscalPeriod period, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);
        // Optimistic-concurrency compare-and-swap: the incoming row
        // must carry Version == stored.Version + 1 (caller bumped on
        // mutation). Returns false if (a) the row was deleted between
        // fetch + write or (b) another writer raced ahead and bumped
        // the stored Version. The caller (PeriodCloseService /
        // FiscalYearCloseService) surfaces ConcurrentUpdate when
        // appropriate; the row-deleted case is treated as
        // PeriodNotFound on the caller's next pass.
        while (true)
        {
            if (!_byId.TryGetValue(period.Id, out var current))
                return Task.FromResult(false);
            if (period.Version != current.Version + 1)
                return Task.FromResult(false);
            if (_byId.TryUpdate(period.Id, period, current))
                return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<FiscalPeriod?> GetByExternalRefAsync(
        string externalRef, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalRef))
            return Task.FromResult<FiscalPeriod?>(null);
        if (_byExternalRef.TryGetValue(externalRef, out var id) && _byId.TryGetValue(id, out var p))
            return Task.FromResult<FiscalPeriod?>(p);
        return Task.FromResult<FiscalPeriod?>(null);
    }

    /// <summary>
    /// Associate an external-system reference with a stored period.
    /// Used by the migration importer in PR 4; exposed for tests.
    /// </summary>
    public void TagExternalRef(string externalRef, FiscalPeriodId id)
    {
        if (string.IsNullOrWhiteSpace(externalRef))
            throw new ArgumentException("External reference must be non-empty.", nameof(externalRef));
        _byExternalRef[externalRef] = id;
    }
}
