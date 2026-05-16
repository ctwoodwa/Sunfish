using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// In-memory <see cref="IFiscalYearRepository"/> for tests and demos.
/// Production callers wire a SQLite-backed implementation.
/// </summary>
public sealed class InMemoryFiscalYearRepository : IFiscalYearRepository
{
    private readonly ConcurrentDictionary<FiscalYearId, FiscalYear> _byId = new();
    private readonly ConcurrentDictionary<string, FiscalYearId> _byExternalRef = new();

    /// <inheritdoc />
    public Task<FiscalYear?> GetAsync(FiscalYearId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id, out var fy) ? fy : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<FiscalYear>> GetByChartAsync(
        ChartOfAccountsId chartId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FiscalYear> rows = _byId.Values
            .Where(fy => fy.ChartId.Equals(chartId))
            .OrderBy(fy => fy.StartDate)
            .ToList();
        return Task.FromResult(rows);
    }

    /// <inheritdoc />
    public Task InsertAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fiscalYear);
        if (!_byId.TryAdd(fiscalYear.Id, fiscalYear))
            throw new InvalidOperationException(
                $"FiscalYear {fiscalYear.Id.Value} already exists.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fiscalYear);
        // CAS pattern — see InMemoryFiscalPeriodRepository.UpdateAsync.
        while (true)
        {
            if (!_byId.TryGetValue(fiscalYear.Id, out var current))
                return Task.FromResult(false);
            if (_byId.TryUpdate(fiscalYear.Id, fiscalYear, current))
                return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<FiscalYear?> GetByExternalRefAsync(
        string externalRef, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalRef))
            return Task.FromResult<FiscalYear?>(null);
        if (_byExternalRef.TryGetValue(externalRef, out var id) && _byId.TryGetValue(id, out var fy))
            return Task.FromResult<FiscalYear?>(fy);
        return Task.FromResult<FiscalYear?>(null);
    }

    /// <summary>
    /// Associate an external-system reference with a stored fiscal year.
    /// Exposed for tests + PR 4's migration importer hooks.
    /// </summary>
    public void TagExternalRef(string externalRef, FiscalYearId id)
    {
        if (string.IsNullOrWhiteSpace(externalRef))
            throw new ArgumentException("External reference must be non-empty.", nameof(externalRef));
        _byExternalRef[externalRef] = id;
    }
}
