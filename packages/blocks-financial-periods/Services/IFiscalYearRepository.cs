using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Persistence contract for <see cref="FiscalYear"/> per Stage 02
/// §3.15. The production implementation is SQLite-backed; tests use
/// <see cref="InMemoryFiscalYearRepository"/>.
/// </summary>
public interface IFiscalYearRepository
{
    /// <summary>Fetch by id, or <c>null</c> when unknown.</summary>
    Task<FiscalYear?> GetAsync(FiscalYearId id, CancellationToken cancellationToken = default);

    /// <summary>List fiscal years for a chart, ordered by start date.</summary>
    Task<IReadOnlyList<FiscalYear>> GetByChartAsync(
        ChartOfAccountsId chartId, CancellationToken cancellationToken = default);

    /// <summary>Insert a new fiscal year.</summary>
    Task InsertAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing fiscal year. Returns <c>true</c> when a row
    /// was updated; <c>false</c> when the id was not found.
    /// </summary>
    Task<bool> UpdateAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>External-system reference lookup (ERPNext / vendor).</summary>
    Task<FiscalYear?> GetByExternalRefAsync(string externalRef, CancellationToken cancellationToken = default);
}
