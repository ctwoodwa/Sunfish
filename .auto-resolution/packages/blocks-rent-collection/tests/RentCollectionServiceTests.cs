using Sunfish.Blocks.RentCollection.Models;
using Sunfish.Blocks.RentCollection.Services;
using Xunit;

namespace Sunfish.Blocks.RentCollection.Tests;

public class RentCollectionServiceTests
{
    private static InMemoryRentCollectionService CreateService() => new();

    // -------------------------------------------------------------------------
    // CreateScheduleAsync
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task CreateScheduleAsync_RejectsDueDayOfMonthOutOfRange(int invalidDay)
    {
        var svc = CreateService();
        var request = new CreateScheduleRequest(
            LeaseId: "lease-1",
            StartDate: new DateOnly(2025, 1, 1),
            EndDate: null,
            MonthlyAmount: 1500m,
            DueDayOfMonth: invalidDay);

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CreateScheduleAsync(request).AsTask());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(28)]
    public async Task CreateScheduleAsync_AcceptsDueDayOfMonthInRange(int validDay)
    {
        var svc = CreateService();
        var request = new CreateScheduleRequest(
            LeaseId: "lease-1",
            StartDate: new DateOnly(2025, 1, 1),
            EndDate: null,
            MonthlyAmount: 1200m,
            DueDayOfMonth: validDay);

        var schedule = await svc.CreateScheduleAsync(request);

        Assert.Equal(validDay, schedule.DueDayOfMonth);
        Assert.Equal("lease-1", schedule.LeaseId);
        Assert.Equal(1200m, schedule.MonthlyAmount);
    }

    // -------------------------------------------------------------------------
    // GenerateInvoiceAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateInvoiceAsync_MonthlyFrequency_ComputesCorrectPeriodDates()
    {
        var svc = CreateService();
        var schedule = await svc.CreateScheduleAsync(new CreateScheduleRequest(
            LeaseId: "lease-2",
            StartDate: new DateOnly(2025, 3, 1),
            EndDate: null,
            MonthlyAmount: 2000m,
            DueDayOfMonth: 5,
            Frequency: BillingFrequency.Monthly));

        var periodStart = new DateOnly(2025, 3, 1);
        var invoice = await svc.GenerateInvoiceAsync(schedule.Id, periodStart);

        Assert.Equal(periodStart, invoice.PeriodStart);
        Assert.Equal(new DateOnly(2025, 3, 31), invoice.PeriodEnd);   // March has 31 days
        Assert.Equal(new DateOnly(2025, 3, 5), invoice.DueDate);
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Equal(0m, invoice.AmountPaid);
        Assert.Equal(2000m, invoice.AmountDue);
    }

    [Fact]
    public async Task GenerateInvoiceAsync_ThrowsWhenScheduleNotFound()
    {
        var svc = CreateService();
        var missing = RentScheduleId.NewId();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GenerateInvoiceAsync(missing, new DateOnly(2025, 1, 1)).AsTask());
    }

    // -------------------------------------------------------------------------
    // RecordPaymentAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RecordPaymentAsync_PartialPayment_SetsPartiallyPaidStatus()
    {
        var svc = CreateService();
        var (_, invoice) = await CreateScheduleAndInvoice(svc, amountDue: 1000m);

        await svc.RecordPaymentAsync(new RecordPaymentRequest(
            InvoiceId: invoice.Id,
            Amount: 400m,
            PaidAtUtc: null,
            Method: "cash"));

        var updated = await svc.GetInvoiceAsync(invoice.Id);
        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.PartiallyPaid, updated!.Status);
        Assert.Equal(400m, updated.AmountPaid);
    }

    [Fact]
    public async Task RecordPaymentAsync_FullPayment_SetsPaidStatus()
    {
        var svc = CreateService();
        var (_, invoice) = await CreateScheduleAndInvoice(svc, amountDue: 1000m);

        await svc.RecordPaymentAsync(new RecordPaymentRequest(
            InvoiceId: invoice.Id,
            Amount: 1000m,
            PaidAtUtc: null,
            Method: "check",
            Reference: "CHK-001"));

        var updated = await svc.GetInvoiceAsync(invoice.Id);
        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.Paid, updated!.Status);
        Assert.Equal(1000m, updated.AmountPaid);
    }

    [Fact]
    public async Task RecordPaymentAsync_Overpayment_SetsPaidStatus()
    {
        var svc = CreateService();
        var (_, invoice) = await CreateScheduleAndInvoice(svc, amountDue: 1000m);

        await svc.RecordPaymentAsync(new RecordPaymentRequest(
            InvoiceId: invoice.Id,
            Amount: 1500m,   // 500 overpayment
            PaidAtUtc: null,
            Method: "ach"));

        var updated = await svc.GetInvoiceAsync(invoice.Id);
        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.Paid, updated!.Status);
        Assert.Equal(1500m, updated.AmountPaid); // overpayment retained, no credit-memo yet
    }

    [Fact]
    public async Task RecordPaymentAsync_NonExistentInvoice_Throws()
    {
        var svc = CreateService();
        var missing = InvoiceId.NewId();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.RecordPaymentAsync(new RecordPaymentRequest(
                InvoiceId: missing,
                Amount: 100m,
                PaidAtUtc: null,
                Method: "cash")).AsTask());
    }

    [Fact]
    public async Task RecordPaymentAsync_ConcurrentPayments_SerializedCorrectly()
    {
        var svc = CreateService();
        var (_, invoice) = await CreateScheduleAndInvoice(svc, amountDue: 1000m);

        // Fire 10 concurrent payments of 100 each — total should be exactly 1000.
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => svc.RecordPaymentAsync(new RecordPaymentRequest(
                InvoiceId: invoice.Id,
                Amount: 100m,
                PaidAtUtc: null,
                Method: "cash")).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        var updated = await svc.GetInvoiceAsync(invoice.Id);
        Assert.NotNull(updated);
        Assert.Equal(1000m, updated!.AmountPaid);
        Assert.Equal(InvoiceStatus.Paid, updated.Status);
    }

    // -------------------------------------------------------------------------
    // LateFeePolicy validation
    // -------------------------------------------------------------------------

    [Fact]
    public void LateFeePolicy_RejectsNeitherFlatNorPercentage()
    {
        Assert.Throws<ArgumentException>(() =>
            new LateFeePolicy(
                Id: LateFeePolicyId.NewId(),
                GracePeriodDays: 5,
                FlatFee: null,
                PercentageFee: null,
                CapAmount: null));
    }

    [Fact]
    public void LateFeePolicy_AcceptsBothFlatAndPercentage()
    {
        var policy = new LateFeePolicy(
            Id: LateFeePolicyId.NewId(),
            GracePeriodDays: 5,
            FlatFee: 50m,
            PercentageFee: 5m,
            CapAmount: 200m);

        Assert.Equal(50m, policy.FlatFee);
        Assert.Equal(5m, policy.PercentageFee);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<(RentSchedule Schedule, Invoice Invoice)> CreateScheduleAndInvoice(
        InMemoryRentCollectionService svc,
        decimal amountDue = 1000m)
    {
        var schedule = await svc.CreateScheduleAsync(new CreateScheduleRequest(
            LeaseId: "lease-test",
            StartDate: new DateOnly(2025, 1, 1),
            EndDate: null,
            MonthlyAmount: amountDue,
            DueDayOfMonth: 1));

        var invoice = await svc.GenerateInvoiceAsync(schedule.Id, new DateOnly(2025, 1, 1));
        return (schedule, invoice);
    }
}
