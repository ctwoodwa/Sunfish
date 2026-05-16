using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Migration;

/// <summary>
/// ERPNext-side <c>FiscalPeriod</c> import entry-point per Stage 02
/// §10.3. ERPNext does NOT export <c>FiscalPeriod</c> as a discrete
/// doctype — it synthesizes monthly buckets at query time. This
/// importer's responsibility is therefore to <b>synthesize</b> the
/// period set at import time from an already-imported
/// <see cref="FiscalYear"/>, using
/// <see cref="FiscalPeriodFactory"/> helpers.
/// </summary>
public interface IErpnextFiscalPeriodImporter
{
    /// <summary>
    /// Synthesize the <see cref="FiscalPeriod"/> rows for an imported
    /// <see cref="FiscalYear"/>. Idempotent per-FY: re-running on a FY
    /// whose periods already exist returns
    /// <see cref="ImportAction.Skipped"/> for every shape; first run
    /// returns <see cref="ImportAction.Inserted"/>.
    /// </summary>
    /// <param name="fiscalYearId">FK to the previously-imported FY.</param>
    /// <param name="kind">Monthly (default) / Quarterly / Annual.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ImportOutcome<FiscalPeriod>>> SynthesizePeriodsForFiscalYearAsync(
        FiscalYearId fiscalYearId,
        FiscalPeriodKind kind = FiscalPeriodKind.Monthly,
        CancellationToken cancellationToken = default);
}
