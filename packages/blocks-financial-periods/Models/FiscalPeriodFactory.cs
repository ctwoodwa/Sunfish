using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialPeriods.Models;

/// <summary>
/// Synthesizes <see cref="FiscalPeriod"/> sets covering a
/// <see cref="FiscalYear"/>. Used by the ERPNext importer (PR 4) and
/// by manual FY-creation flows.
/// </summary>
public static class FiscalPeriodFactory
{
    /// <summary>
    /// Build a contiguous monthly period set covering
    /// [<paramref name="fy"/>.StartDate, <paramref name="fy"/>.EndDate]
    /// inclusive. Labels are <c>"{FY.Label}-M01"…"{FY.Label}-M12"</c>.
    /// The last period clips to FY.EndDate when the FY doesn't end on
    /// a calendar-month boundary.
    /// </summary>
    public static IReadOnlyList<FiscalPeriod> BuildMonthlyPeriods(
        FiscalYear fy,
        Instant? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(fy);
        var periods = new List<FiscalPeriod>(12);
        var cursor = fy.StartDate;
        var monthIndex = 1;

        while (cursor <= fy.EndDate)
        {
            var endOfMonth = new DateOnly(
                cursor.Year, cursor.Month,
                DateTime.DaysInMonth(cursor.Year, cursor.Month));
            var periodEnd = endOfMonth < fy.EndDate ? endOfMonth : fy.EndDate;

            periods.Add(FiscalPeriod.CreateOpen(
                id:           FiscalPeriodId.NewId(),
                chartId:      fy.ChartId,
                fiscalYearId: fy.Id,
                kind:         FiscalPeriodKind.Monthly,
                label:        $"{fy.Label}-M{monthIndex:D2}",
                startDate:    cursor,
                endDate:      periodEnd,
                createdAtUtc: createdAtUtc));

            cursor = periodEnd.AddDays(1);
            monthIndex++;
        }

        return periods;
    }

    /// <summary>
    /// Build a contiguous quarterly period set covering the FY span.
    /// Labels are <c>"{FY.Label}-Q1"…"{FY.Label}-Q4"</c>. Quarters end
    /// at the boundary of months 3/6/9 within the FY (so a calendar-year
    /// FY's quarters are Jan-Mar / Apr-Jun / Jul-Sep / Oct-Dec).
    /// </summary>
    public static IReadOnlyList<FiscalPeriod> BuildQuarterlyPeriods(
        FiscalYear fy,
        Instant? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(fy);
        var periods = new List<FiscalPeriod>(4);
        var cursor = fy.StartDate;
        for (var quarter = 1; quarter <= 4 && cursor <= fy.EndDate; quarter++)
        {
            // Quarter spans 3 calendar months from cursor.
            var qEnd = cursor.AddMonths(3).AddDays(-1);
            if (qEnd > fy.EndDate) qEnd = fy.EndDate;
            periods.Add(FiscalPeriod.CreateOpen(
                id:           FiscalPeriodId.NewId(),
                chartId:      fy.ChartId,
                fiscalYearId: fy.Id,
                kind:         FiscalPeriodKind.Quarterly,
                label:        $"{fy.Label}-Q{quarter}",
                startDate:    cursor,
                endDate:      qEnd,
                createdAtUtc: createdAtUtc));
            cursor = qEnd.AddDays(1);
        }
        return periods;
    }

    /// <summary>
    /// Build a single annual period covering the entire FY span. Label
    /// is the FY's label.
    /// </summary>
    public static IReadOnlyList<FiscalPeriod> BuildAnnualPeriod(
        FiscalYear fy,
        Instant? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(fy);
        return new[]
        {
            FiscalPeriod.CreateOpen(
                id:           FiscalPeriodId.NewId(),
                chartId:      fy.ChartId,
                fiscalYearId: fy.Id,
                kind:         FiscalPeriodKind.Annual,
                label:        fy.Label,
                startDate:    fy.StartDate,
                endDate:      fy.EndDate,
                createdAtUtc: createdAtUtc),
        };
    }
}
