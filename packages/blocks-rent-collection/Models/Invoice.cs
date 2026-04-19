using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>
/// A single billing event generated from a <see cref="RentSchedule"/>.
/// Tracks the amounts owed and received for one billing period.
/// </summary>
/// <param name="Id">Unique invoice identifier.</param>
/// <param name="ScheduleId">The schedule that generated this invoice.</param>
/// <param name="LeaseId">
/// Opaque reference to the associated lease. Uses a plain <see langword="string"/> in this pass.
/// TODO: migrate to <c>Sunfish.Blocks.Leases.Models.LeaseId</c> once G14 is on main.
/// </param>
/// <param name="PeriodStart">First day of the billing period this invoice covers.</param>
/// <param name="PeriodEnd">Last day of the billing period this invoice covers.</param>
/// <param name="DueDate">Calendar date by which payment must be received.</param>
/// <param name="AmountDue">
/// Total amount owed for this period.
/// <para><b>Precision note:</b> stored as <see cref="decimal"/> with a two-decimal-place
/// assumption. Rounding enforcement is deferred to a follow-up.</para>
/// </param>
/// <param name="AmountPaid">
/// Running total of all payments applied to this invoice.
/// Overpayments are allowed in this pass (AmountPaid &gt; AmountDue); no credit-memo
/// logic is applied — the status is set to <see cref="InvoiceStatus.Paid"/> and the
/// overpayment amount is retained. TODO: implement credit-memo logic in a follow-up.
/// <para><b>Precision note:</b> stored as <see cref="decimal"/> with a two-decimal-place
/// assumption. Rounding enforcement is deferred to a follow-up.</para>
/// </param>
/// <param name="Status">Current lifecycle status of the invoice.</param>
/// <param name="GeneratedAtUtc">Instant at which the invoice was created.</param>
public sealed record Invoice(
    InvoiceId Id,
    RentScheduleId ScheduleId,
    string LeaseId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    decimal AmountDue,
    decimal AmountPaid,
    InvoiceStatus Status,
    Instant GeneratedAtUtc);
