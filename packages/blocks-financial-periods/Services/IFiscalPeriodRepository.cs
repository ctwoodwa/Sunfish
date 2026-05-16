using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Persistence contract for <see cref="FiscalPeriod"/> per Stage 02
/// §3.16. The production implementation is SQLite-backed (lands with
/// the per-cluster local-node persistence wiring); tests use the
/// in-memory fake <see cref="InMemoryFiscalPeriodRepository"/>.
/// </summary>
public interface IFiscalPeriodRepository
{
    /// <summary>Fetch by id, or <c>null</c> when unknown.</summary>
    Task<FiscalPeriod?> GetAsync(FiscalPeriodId id, CancellationToken cancellationToken = default);

    /// <summary>List the periods that belong to a fiscal year (ordered by start date).</summary>
    Task<IReadOnlyList<FiscalPeriod>> GetByFiscalYearAsync(
        FiscalYearId fiscalYearId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find the single period that covers <paramref name="date"/> within
    /// the given chart, or <c>null</c> when no covering period exists.
    /// </summary>
    Task<FiscalPeriod?> FindByChartAndDateAsync(
        ChartOfAccountsId chartId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Insert a new period (caller-owned transaction is optional).</summary>
    Task InsertAsync(FiscalPeriod period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing period. Returns <c>true</c> when a row was
    /// updated; <c>false</c> when the id was not found.
    /// </summary>
    Task<bool> UpdateAsync(FiscalPeriod period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Look up a period by an external-system reference (ERPNext doctype
    /// key, vendor identifier, etc.). Used by the migration importer
    /// (PR 4) and round-trip integrations.
    /// </summary>
    Task<FiscalPeriod?> GetByExternalRefAsync(string externalRef, CancellationToken cancellationToken = default);
}
