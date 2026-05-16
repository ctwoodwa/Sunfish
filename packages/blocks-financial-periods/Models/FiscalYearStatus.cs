namespace Sunfish.Blocks.FinancialPeriods.Models;

/// <summary>Lifecycle state of a <see cref="FiscalYear"/> per Stage 02 §3.15.</summary>
public enum FiscalYearStatus
{
    /// <summary>Periods within may be Open, SoftClosed, or (rarely) Locked.</summary>
    Open,

    /// <summary>
    /// <c>closeFiscalYear()</c> executed; closing JE posted; all periods Locked.
    /// </summary>
    Closed,
}
