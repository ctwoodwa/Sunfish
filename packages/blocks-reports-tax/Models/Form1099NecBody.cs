namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// Body for IRS Form 1099-NEC — Nonemployee Compensation.
/// Contains one row per payee who received $600 or more during the tax year.
/// </summary>
/// <param name="Recipients">
/// One row per payee. Consumers are responsible for filtering or flagging rows
/// below the $600 IRS threshold using <see cref="Nec1099Recipient.MeetsThreshold"/>
/// or <see cref="Nec1099Recipient.Validate"/>.
/// </param>
public sealed record Form1099NecBody(
    IReadOnlyList<Nec1099Recipient> Recipients) : TaxReportBody
{
    /// <inheritdoc />
    public override TaxReportKind Kind => TaxReportKind.Form1099Nec;
}
