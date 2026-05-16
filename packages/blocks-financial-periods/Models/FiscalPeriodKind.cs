namespace Sunfish.Blocks.FinancialPeriods.Models;

/// <summary>
/// Subdivision pattern for a <see cref="FiscalPeriod"/> set within a
/// <see cref="FiscalYear"/> per Stage 02 §3.16.
/// </summary>
public enum FiscalPeriodKind
{
    /// <summary>12 periods per year (calendar months or fiscal-equivalent months).</summary>
    Monthly,

    /// <summary>4 periods per year.</summary>
    Quarterly,

    /// <summary>1 period per year (the entire year is one period).</summary>
    Annual,

    /// <summary>Any other period scheme (e.g. 4-4-5 retail calendar).</summary>
    Custom,
}
