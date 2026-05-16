using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;

namespace Sunfish.Blocks.FinancialPeriods.Migration;

/// <summary>
/// Default <see cref="IErpnextFiscalPeriodImporter"/>. Synthesizes
/// periods via <see cref="FiscalPeriodFactory"/> and persists through
/// <see cref="IFiscalPeriodRepository"/>.
/// </summary>
public sealed class ErpnextFiscalPeriodImporter : IErpnextFiscalPeriodImporter
{
    private readonly IFiscalYearRepository _years;
    private readonly IFiscalPeriodRepository _periods;

    public ErpnextFiscalPeriodImporter(
        IFiscalYearRepository years,
        IFiscalPeriodRepository periods)
    {
        _years   = years   ?? throw new ArgumentNullException(nameof(years));
        _periods = periods ?? throw new ArgumentNullException(nameof(periods));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportOutcome<FiscalPeriod>>> SynthesizePeriodsForFiscalYearAsync(
        FiscalYearId fiscalYearId,
        FiscalPeriodKind kind = FiscalPeriodKind.Monthly,
        CancellationToken cancellationToken = default)
    {
        var fy = await _years.GetAsync(fiscalYearId, cancellationToken).ConfigureAwait(false);
        if (fy is null)
            return Array.Empty<ImportOutcome<FiscalPeriod>>();

        var existing = await _periods.GetByFiscalYearAsync(fy.Id, cancellationToken).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            return existing
                .Select(p => new ImportOutcome<FiscalPeriod>(p, ImportAction.Skipped, "Periods already synthesized."))
                .ToList();
        }

        var synthesized = kind switch
        {
            FiscalPeriodKind.Quarterly => FiscalPeriodFactory.BuildQuarterlyPeriods(fy),
            FiscalPeriodKind.Annual    => FiscalPeriodFactory.BuildAnnualPeriod(fy),
            _                          => FiscalPeriodFactory.BuildMonthlyPeriods(fy),
        };

        // Defense-in-depth: validate the synthesized set covers the FY
        // contiguously (catches off-by-one edge cases — e.g., a fiscal
        // year that ends mid-month — before we persist).
        var validation = FiscalPeriodCollectionValidator.Validate(fy, synthesized);
        if (!validation.IsValid)
            return synthesized
                .Select(p => new ImportOutcome<FiscalPeriod>(p, ImportAction.Skipped,
                    $"Validation failed: {string.Join(" | ", validation.Errors)}"))
                .ToList();

        var outcomes = new List<ImportOutcome<FiscalPeriod>>(synthesized.Count);
        foreach (var p in synthesized)
        {
            await _periods.InsertAsync(p, cancellationToken).ConfigureAwait(false);
            outcomes.Add(new ImportOutcome<FiscalPeriod>(p, ImportAction.Inserted, null));
        }
        return outcomes;
    }
}
