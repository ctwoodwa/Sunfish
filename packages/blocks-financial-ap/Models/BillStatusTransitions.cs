namespace Sunfish.Blocks.FinancialAp.Models;

/// <summary>
/// Allowed transitions in the bill lifecycle:
///
/// <code>
///   Draft         → Received | Voided
///   Received      → Approved | PartiallyPaid | Paid | Voided | Disputed
///   Approved      → PartiallyPaid | Paid | Voided | Disputed
///   PartiallyPaid → Paid | Voided | Disputed
///   Disputed      → Received | Approved              (dispute resolves back to prior payable state)
///   Paid, Voided                                     (terminal)
/// </code>
///
/// <para>
/// Notably allowed but absent from AR: <c>Received → PartiallyPaid</c>
/// without going through <c>Approved</c> first (when policy doesn't
/// require approval). Notably forbidden: <c>Disputed → Paid</c>
/// directly (the dispute must resolve back to a normal payable state
/// before payment can apply).
/// </para>
/// </summary>
public static class BillStatusTransitions
{
    /// <summary>True if <paramref name="from"/> may legally transition to <paramref name="to"/>.</summary>
    public static bool IsAllowed(BillStatus from, BillStatus to) =>
        (from, to) switch
        {
            // Draft
            (BillStatus.Draft,         BillStatus.Received)       => true,
            (BillStatus.Draft,         BillStatus.Voided)         => true,
            // Received
            (BillStatus.Received,      BillStatus.Approved)       => true,
            (BillStatus.Received,      BillStatus.PartiallyPaid)  => true,
            (BillStatus.Received,      BillStatus.Paid)           => true,
            (BillStatus.Received,      BillStatus.Voided)         => true,
            (BillStatus.Received,      BillStatus.Disputed)       => true,
            // Approved
            (BillStatus.Approved,      BillStatus.PartiallyPaid)  => true,
            (BillStatus.Approved,      BillStatus.Paid)           => true,
            (BillStatus.Approved,      BillStatus.Voided)         => true,
            (BillStatus.Approved,      BillStatus.Disputed)       => true,
            // PartiallyPaid
            (BillStatus.PartiallyPaid, BillStatus.Paid)           => true,
            (BillStatus.PartiallyPaid, BillStatus.Voided)         => true,
            (BillStatus.PartiallyPaid, BillStatus.Disputed)       => true,
            // Disputed (resolves back)
            (BillStatus.Disputed,      BillStatus.Received)       => true,
            (BillStatus.Disputed,      BillStatus.Approved)       => true,
            // Anything else
            _ => false,
        };
}
