namespace Sunfish.Blocks.FinancialAr.Models;

/// <summary>
/// Allowed transitions in the invoice lifecycle. The full graph:
///
/// <code>
///   Draft → Issued
///   Issued → PartiallyPaid | Paid | Voided | WrittenOff
///   PartiallyPaid → Paid | Voided | WrittenOff
///   Paid, Voided, WrittenOff → ⊥ (terminal — no further transitions)
/// </code>
///
/// <para>
/// Notably absent: <c>Issued → Draft</c> (no "un-issue" — voiding is the
/// answer), <c>Paid → PartiallyPaid</c> (a Paid invoice that receives a
/// refund creates a separate credit-memo, doesn't rewind status), and any
/// transition out of a terminal state.
/// </para>
/// </summary>
public static class InvoiceStatusTransitions
{
    /// <summary>True if <paramref name="from"/> may legally transition to <paramref name="to"/>.</summary>
    public static bool IsAllowed(InvoiceStatus from, InvoiceStatus to) =>
        (from, to) switch
        {
            (InvoiceStatus.Draft,         InvoiceStatus.Issued)        => true,
            (InvoiceStatus.Issued,        InvoiceStatus.PartiallyPaid) => true,
            (InvoiceStatus.Issued,        InvoiceStatus.Paid)          => true,
            (InvoiceStatus.Issued,        InvoiceStatus.Voided)        => true,
            (InvoiceStatus.Issued,        InvoiceStatus.WrittenOff)    => true,
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid)          => true,
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Voided)        => true,
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.WrittenOff)    => true,
            _ => false,
        };
}
