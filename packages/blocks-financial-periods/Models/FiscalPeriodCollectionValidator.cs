namespace Sunfish.Blocks.FinancialPeriods.Models;

/// <summary>
/// Cross-period invariant validator per Stage 02 §3.16 rules 1–3 +
/// rule 3 (status discipline). Verifies that a set of
/// <see cref="FiscalPeriod"/>s within a <see cref="FiscalYear"/> is
/// contiguous, non-overlapping, exactly covers the FY span, and that
/// any <see cref="FiscalPeriodStatus.Locked"/> period is only valid
/// while the parent FY is <see cref="FiscalYearStatus.Closed"/>.
/// </summary>
public static class FiscalPeriodCollectionValidator
{
    /// <summary>Outcome of <see cref="Validate"/>. Empty Errors on success.</summary>
    public sealed record ValidationResult(
        bool IsValid,
        IReadOnlyList<string> Errors);

    /// <summary>
    /// Validate <paramref name="periods"/> against <paramref name="fiscalYear"/>.
    /// </summary>
    public static ValidationResult Validate(
        FiscalYear fiscalYear,
        IReadOnlyList<FiscalPeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(fiscalYear);
        ArgumentNullException.ThrowIfNull(periods);

        var errors = new List<string>();
        var sorted = periods.OrderBy(p => p.StartDate).ToList();

        // Rule 1: contiguous, non-overlapping (sorted by StartDate)
        for (var i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i].EndDate.AddDays(1) != sorted[i + 1].StartDate)
            {
                errors.Add(
                    $"Period gap or overlap between {sorted[i].Label} "
                    + $"(ends {sorted[i].EndDate:O}) and {sorted[i + 1].Label} "
                    + $"(starts {sorted[i + 1].StartDate:O}).");
            }
        }

        // Rule 2: union covers FY span
        if (sorted.Count > 0)
        {
            if (sorted[0].StartDate != fiscalYear.StartDate)
            {
                errors.Add(
                    $"First period {sorted[0].Label} starts "
                    + $"{sorted[0].StartDate:O}; FY {fiscalYear.Label} "
                    + $"starts {fiscalYear.StartDate:O}.");
            }
            if (sorted[^1].EndDate != fiscalYear.EndDate)
            {
                errors.Add(
                    $"Last period {sorted[^1].Label} ends "
                    + $"{sorted[^1].EndDate:O}; FY {fiscalYear.Label} "
                    + $"ends {fiscalYear.EndDate:O}.");
            }
        }

        // Rule 3: Locked only if FY is Closed
        foreach (var p in sorted)
        {
            if (p.Status == FiscalPeriodStatus.Locked
                && fiscalYear.Status != FiscalYearStatus.Closed)
            {
                errors.Add(
                    $"Period {p.Label} is Locked but FY {fiscalYear.Label} is Open. "
                    + "Locking is only valid as part of year-close.");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}
