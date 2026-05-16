namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// Body for IRS Schedule E — Supplemental Income and Loss (§199A-aligned).
/// Contains per-property rows and aggregate totals.
/// </summary>
/// <param name="Properties">
/// One row per rental property included in this Schedule E.
/// </param>
/// <param name="TotalRents">Sum of <see cref="SchedulePropertyRow.RentsReceived"/> across all properties.</param>
/// <param name="TotalExpenses">Sum of all deductible expenses across all properties.</param>
/// <param name="NetIncomeOrLoss">
/// Net rental income (positive) or loss (negative) for the tax year.
/// Equals <see cref="TotalRents"/> minus <see cref="TotalExpenses"/>.
/// </param>
public sealed record ScheduleEBody(
    IReadOnlyList<SchedulePropertyRow> Properties,
    decimal TotalRents,
    decimal TotalExpenses,
    decimal NetIncomeOrLoss) : TaxReportBody
{
    /// <inheritdoc />
    public override TaxReportKind Kind => TaxReportKind.ScheduleE;
}
