using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>
/// Records a single payment event applied against an <see cref="Invoice"/>.
/// </summary>
/// <param name="Id">Unique payment identifier.</param>
/// <param name="InvoiceId">The invoice this payment is applied to.</param>
/// <param name="Amount">
/// Amount of this payment.
/// <para><b>Precision note:</b> stored as <see cref="decimal"/> with a two-decimal-place
/// assumption. Rounding enforcement is deferred to a follow-up.</para>
/// </param>
/// <param name="PaidAtUtc">Instant at which the payment was received or recorded.</param>
/// <param name="Method">
/// Payment method as an opaque string. Recognised values by convention:
/// <c>"cash"</c>, <c>"check"</c>, <c>"ach"</c>, <c>"card"</c>.
/// No enum enforced in this pass — Plaid/Stripe integration is deferred.
/// </param>
/// <param name="Reference">
/// Optional reference identifier: cheque number, ACH transaction ID, etc.
/// </param>
public sealed record Payment(
    PaymentId Id,
    InvoiceId InvoiceId,
    decimal Amount,
    Instant PaidAtUtc,
    string Method,
    string? Reference);
