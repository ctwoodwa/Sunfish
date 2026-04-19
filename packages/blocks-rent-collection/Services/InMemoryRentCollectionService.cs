using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Blocks.RentCollection.Models;

namespace Sunfish.Blocks.RentCollection.Services;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IRentCollectionService"/>.
/// Suitable for testing, prototyping, and kitchen-sink demos.
/// Not intended for production persistence — use a database-backed implementation for that.
/// </summary>
public sealed class InMemoryRentCollectionService : IRentCollectionService
{
    private readonly ConcurrentDictionary<RentScheduleId, RentSchedule> _schedules = new();
    private readonly ConcurrentDictionary<InvoiceId, Invoice> _invoices = new();
    private readonly ConcurrentDictionary<PaymentId, Payment> _payments = new();

    // Per-invoice locks for serializing concurrent payments on the same invoice.
    private readonly ConcurrentDictionary<InvoiceId, SemaphoreSlim> _invoiceLocks = new();

    // ---------------------------------------------------------------------------
    // IRentCollectionService
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public ValueTask<RentSchedule> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken ct = default)
    {
        if (request.DueDayOfMonth is < 1 or > 28)
            throw new ArgumentException(
                $"DueDayOfMonth must be between 1 and 28 (got {request.DueDayOfMonth}).",
                nameof(request));

        var schedule = new RentSchedule(
            Id: RentScheduleId.NewId(),
            LeaseId: request.LeaseId,
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            MonthlyAmount: request.MonthlyAmount,
            DueDayOfMonth: request.DueDayOfMonth,
            Frequency: request.Frequency);

        _schedules[schedule.Id] = schedule;
        return ValueTask.FromResult(schedule);
    }

    /// <inheritdoc />
    public ValueTask<Invoice> GenerateInvoiceAsync(RentScheduleId scheduleId, DateOnly periodStart, CancellationToken ct = default)
    {
        if (!_schedules.TryGetValue(scheduleId, out var schedule))
            throw new KeyNotFoundException($"RentSchedule '{scheduleId}' not found.");

        var periodEnd = ComputePeriodEnd(periodStart, schedule.Frequency);
        var dueDate = new DateOnly(periodStart.Year, periodStart.Month, schedule.DueDayOfMonth);

        var invoice = new Invoice(
            Id: InvoiceId.NewId(),
            ScheduleId: schedule.Id,
            LeaseId: schedule.LeaseId,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            DueDate: dueDate,
            AmountDue: schedule.MonthlyAmount,
            AmountPaid: 0m,
            Status: InvoiceStatus.Open,
            GeneratedAtUtc: Instant.Now);

        _invoices[invoice.Id] = invoice;
        return ValueTask.FromResult(invoice);
    }

    /// <inheritdoc />
    public async ValueTask<Payment> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken ct = default)
    {
        if (!_invoices.ContainsKey(request.InvoiceId))
            throw new KeyNotFoundException($"Invoice '{request.InvoiceId}' not found.");

        // Acquire per-invoice lock to serialize concurrent payments.
        var invoiceLock = _invoiceLocks.GetOrAdd(request.InvoiceId, _ => new SemaphoreSlim(1, 1));
        await invoiceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_invoices.TryGetValue(request.InvoiceId, out var invoice))
                throw new KeyNotFoundException($"Invoice '{request.InvoiceId}' not found.");

            var newAmountPaid = invoice.AmountPaid + request.Amount;
            var newStatus = ComputeStatus(invoice.AmountDue, newAmountPaid);

            var updated = invoice with
            {
                AmountPaid = newAmountPaid,
                Status = newStatus,
            };
            _invoices[invoice.Id] = updated;

            var payment = new Payment(
                Id: PaymentId.NewId(),
                InvoiceId: request.InvoiceId,
                Amount: request.Amount,
                PaidAtUtc: request.PaidAtUtc ?? Instant.Now,
                Method: request.Method,
                Reference: request.Reference);

            _payments[payment.Id] = payment;
            return payment;
        }
        finally
        {
            invoiceLock.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask<Invoice?> GetInvoiceAsync(InvoiceId id, CancellationToken ct = default)
    {
        _invoices.TryGetValue(id, out var invoice);
        return ValueTask.FromResult(invoice);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Invoice> ListInvoicesAsync(
        ListInvoicesQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var invoice in _invoices.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.LeaseId is not null && invoice.LeaseId != query.LeaseId)
                continue;
            if (query.ScheduleId is not null && invoice.ScheduleId != query.ScheduleId)
                continue;
            if (query.Status is not null && invoice.Status != query.Status)
                continue;
            if (query.DueBefore is not null && invoice.DueDate > query.DueBefore)
                continue;
            if (query.DueAfter is not null && invoice.DueDate < query.DueAfter)
                continue;

            yield return invoice;
        }

        await ValueTask.CompletedTask.ConfigureAwait(false); // satisfy async enumerator requirement
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static DateOnly ComputePeriodEnd(DateOnly periodStart, BillingFrequency frequency)
        => frequency switch
        {
            BillingFrequency.Monthly    => periodStart.AddMonths(1).AddDays(-1),
            BillingFrequency.BiMonthly  => periodStart.AddMonths(2).AddDays(-1),
            BillingFrequency.Quarterly  => periodStart.AddMonths(3).AddDays(-1),
            BillingFrequency.Annually   => periodStart.AddYears(1).AddDays(-1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null),
        };

    private static InvoiceStatus ComputeStatus(decimal amountDue, decimal amountPaid)
    {
        if (amountPaid <= 0m)
            return InvoiceStatus.Open;

        if (amountPaid >= amountDue)
        {
            // Overpayment is silently absorbed; no credit-memo in this pass.
            // TODO: implement credit-memo logic when payment-reconciliation follow-up ships.
            return InvoiceStatus.Paid;
        }

        return InvoiceStatus.PartiallyPaid;
    }
}
