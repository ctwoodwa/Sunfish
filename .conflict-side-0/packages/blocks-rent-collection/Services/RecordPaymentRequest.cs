using Sunfish.Foundation.Assets.Common;
using Sunfish.Blocks.RentCollection.Models;

namespace Sunfish.Blocks.RentCollection.Services;

/// <summary>Input model for recording a payment against an <see cref="Invoice"/>.</summary>
/// <param name="InvoiceId">The invoice being paid.</param>
/// <param name="Amount">
/// Amount of this payment.
/// <para><b>Precision note:</b> two-decimal-place assumption; rounding enforcement deferred.</para>
/// </param>
/// <param name="PaidAtUtc">
/// Instant the payment was received. Defaults to <see cref="Instant.Now"/> if not supplied.
/// </param>
/// <param name="Method">
/// Payment method string, e.g. <c>"cash"</c>, <c>"check"</c>, <c>"ach"</c>, <c>"card"</c>.
/// </param>
/// <param name="Reference">Optional reference such as a cheque number or transaction ID.</param>
public sealed record RecordPaymentRequest(
    InvoiceId InvoiceId,
    decimal Amount,
    Instant? PaidAtUtc,
    string Method,
    string? Reference = null);
