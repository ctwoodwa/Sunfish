namespace Sunfish.Blocks.Accounting.Models;

/// <summary>
/// Specifies the cost-allocation method for a <see cref="DepreciationSchedule"/>.
/// Computation is deferred — this enum captures intent for the follow-up G17 pass.
/// </summary>
public enum DepreciationMethod
{
    /// <summary>
    /// Allocates an equal portion of cost each period.
    /// Annual expense = (OriginalCost − SalvageValue) / UsefulLifeYears.
    /// </summary>
    StraightLine,

    /// <summary>
    /// Applies a fixed percentage to the remaining book value each period,
    /// producing higher charges in early years.
    /// </summary>
    DecliningBalance,

    /// <summary>
    /// Expense is proportional to actual usage (units produced, hours run, etc.)
    /// rather than elapsed time.
    /// </summary>
    UnitsOfProduction,
}
