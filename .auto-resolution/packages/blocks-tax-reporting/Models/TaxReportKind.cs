namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// Discriminates the kind of tax report and its corresponding
/// <see cref="TaxReportBody"/> subtype.
/// </summary>
public enum TaxReportKind
{
    /// <summary>
    /// IRS Schedule E — Supplemental Income and Loss (rental income/expense per property).
    /// Body type: <see cref="ScheduleEBody"/>.
    /// </summary>
    ScheduleE,

    /// <summary>
    /// IRS Form 1099-NEC — Nonemployee Compensation.
    /// Body type: <see cref="Form1099NecBody"/>.
    /// </summary>
    Form1099Nec,

    /// <summary>
    /// State personal-property tax return.
    /// Body type: <see cref="StatePersonalPropertyBody"/>.
    /// Per-state templates are deferred; this is a schema carrier only.
    /// </summary>
    StatePersonalProperty,
}
