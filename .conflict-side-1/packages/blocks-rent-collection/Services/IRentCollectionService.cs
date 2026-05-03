using Sunfish.Blocks.RentCollection.Models;

namespace Sunfish.Blocks.RentCollection.Services;

/// <summary>
/// Core service contract for the rent-collection domain.
/// Implementations are expected to be scoped or singleton depending on the host.
/// </summary>
public interface IRentCollectionService
{
    /// <summary>
    /// Creates and persists a new rent schedule from the supplied request.
    /// </summary>
    /// <param name="request">Schedule configuration including lease reference, amounts, and frequency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="RentSchedule"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="request"/> contains invalid data, e.g.
    /// <c>DueDayOfMonth</c> outside 1–28.
    /// </exception>
    ValueTask<RentSchedule> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken ct = default);

    /// <summary>
    /// Generates an invoice for the given schedule covering the billing period that starts on
    /// <paramref name="periodStart"/>. Period end and due date are computed from the schedule's
    /// <see cref="BillingFrequency"/> and <see cref="RentSchedule.DueDayOfMonth"/>.
    /// </summary>
    /// <param name="scheduleId">The schedule to invoice.</param>
    /// <param name="periodStart">Start date of the billing period.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly generated <see cref="Invoice"/> in <see cref="InvoiceStatus.Open"/> status.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no schedule with <paramref name="scheduleId"/> is found.
    /// </exception>
    ValueTask<Invoice> GenerateInvoiceAsync(RentScheduleId scheduleId, DateOnly periodStart, CancellationToken ct = default);

    /// <summary>
    /// Records a payment against an existing invoice and updates the invoice's
    /// <c>AmountPaid</c> and <c>Status</c> atomically.
    /// </summary>
    /// <remarks>
    /// Overpayments are allowed (AmountPaid &gt; AmountDue). The status is set to
    /// <see cref="InvoiceStatus.Paid"/> and the surplus is retained in <c>AmountPaid</c>.
    /// No credit-memo logic is applied in this pass — that is a follow-up item.
    /// </remarks>
    /// <param name="request">Payment details including the invoice reference and amount.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="Payment"/> record as persisted.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the referenced invoice does not exist.
    /// </exception>
    ValueTask<Payment> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single invoice by its identifier.
    /// </summary>
    /// <param name="id">Invoice identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="Invoice"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<Invoice?> GetInvoiceAsync(InvoiceId id, CancellationToken ct = default);

    /// <summary>
    /// Lists invoices matching the supplied filter query.
    /// Returns all invoices when the query contains no filter constraints.
    /// </summary>
    /// <param name="query">Optional filter criteria.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async sequence of matching <see cref="Invoice"/> records.</returns>
    IAsyncEnumerable<Invoice> ListInvoicesAsync(ListInvoicesQuery query, CancellationToken ct = default);
}
