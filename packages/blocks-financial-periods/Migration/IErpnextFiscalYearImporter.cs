using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Migration;

/// <summary>
/// ERPNext-side <c>Fiscal Year</c> import entry-point per Stage 02
/// §10.1. Idempotent on <see cref="ErpnextFiscalYearSource.Name"/> +
/// <see cref="ErpnextFiscalYearSource.Modified"/> — re-running the
/// importer on an unchanged source returns
/// <see cref="ImportAction.Skipped"/>.
/// </summary>
public interface IErpnextFiscalYearImporter
{
    /// <summary>
    /// Insert a new <see cref="FiscalYear"/> or update an existing one
    /// matched by external reference. Returns
    /// <see cref="ImportAction.Inserted"/> when a new row was created,
    /// <see cref="ImportAction.Updated"/> when the existing row's
    /// version moved forward, and <see cref="ImportAction.Skipped"/>
    /// otherwise (same / lower / unparseable version).
    /// </summary>
    /// <remarks>
    /// Closed local fiscal years are NEVER reopened by the importer —
    /// an ERPNext re-export with later <c>modified</c> on a Closed FY
    /// updates label/date fields but leaves
    /// <see cref="FiscalYear.Status"/> at <see cref="FiscalYearStatus.Closed"/>.
    /// Year-close is the <c>FiscalYearCloseService</c> path, not the
    /// importer's responsibility.
    /// </remarks>
    Task<ImportOutcome<FiscalYear>> UpsertFromErpnextAsync(
        ErpnextFiscalYearSource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default);
}
