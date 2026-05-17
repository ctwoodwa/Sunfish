using Rc = global::Sunfish.Blocks.RentCollection.Models;
using Sunfish.Blocks.RentCollection.Migration;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.RentCollection.Tests;

public class ArInvoiceProjectionTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static GLAccountId Account() => GLAccountId.NewId();
    private static PartyId Customer() => PartyId.NewId();

    private static Rc.Invoice NewRentInvoice(
        decimal amountDue = 1000m,
        decimal amountPaid = 0m,
        Rc.InvoiceStatus status = Rc.InvoiceStatus.Open,
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null,
        DateOnly? dueDate = null)
    {
        return new Rc.Invoice(
            Id: Rc.InvoiceId.NewId(),
            ScheduleId: Rc.RentScheduleId.NewId(),
            LeaseId: "lease-001",
            PeriodStart: periodStart ?? new DateOnly(2026, 5, 1),
            PeriodEnd: periodEnd ?? new DateOnly(2026, 5, 31),
            DueDate: dueDate ?? new DateOnly(2026, 5, 1),
            AmountDue: amountDue,
            AmountPaid: amountPaid,
            Status: status,
            GeneratedAtUtc: new Instant(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero)));
    }

    // ── ToCanonicalAr — happy path ────────────────────────────────────

    [Fact]
    public void ToCanonicalAr_MapsCoreFieldsCorrectly()
    {
        var rent = NewRentInvoice(amountDue: 1500m, amountPaid: 0m, status: Rc.InvoiceStatus.Open);
        var customer = Customer();
        var chart = Chart();

        var ar = ArInvoiceProjection.ToCanonicalAr(
            rentInvoice: rent,
            tenantId: Tenant(),
            chartId: chart,
            customerId: customer,
            arAccountId: Account(),
            rentIncomeAccountId: Account(),
            invoiceNumber: "INV-2026-04-25-CW-0001",
            propertyId: "100-MAIN");

        Assert.Equal(chart, ar.ChartId);
        Assert.Equal(customer, ar.CustomerId);
        Assert.Equal("INV-2026-04-25-CW-0001", ar.InvoiceNumber);
        Assert.Equal(1500m, ar.Total);
        Assert.Equal(1500m, ar.Balance);
        Assert.Equal(InvoiceStatus.Issued, ar.Status);
        Assert.Equal("100-MAIN", ar.PropertyId);
        Assert.Equal(rent.DueDate, ar.DueDate);
        Assert.Single(ar.Lines);
        Assert.Equal(1500m, ar.Lines[0].Amount);
    }

    [Fact]
    public void ToCanonicalAr_PreservesRentInvoiceIdViaExternalRef()
    {
        var rent = NewRentInvoice();
        var ar = ArInvoiceProjection.ToCanonicalAr(
            rent, Tenant(), Chart(), Customer(), Account(), Account(),
            invoiceNumber: "INV-2026-04-25-CW-0001");

        Assert.Equal($"rent-invoice:{rent.Id.Value}", ar.ExternalRef);
    }

    [Fact]
    public void ToCanonicalAr_IssueDateDerivedFromGeneratedAtUtc()
    {
        var rent = NewRentInvoice();
        var ar = ArInvoiceProjection.ToCanonicalAr(
            rent, Tenant(), Chart(), Customer(), Account(), Account(),
            invoiceNumber: "INV-2026-04-25-CW-0001");

        // GeneratedAtUtc = 2026-04-25T12:00:00Z → IssueDate = 2026-04-25.
        Assert.Equal(new DateOnly(2026, 4, 25), ar.IssueDate);
    }

    [Fact]
    public void ToCanonicalAr_LineDescription_IncludesPeriod()
    {
        var rent = NewRentInvoice(
            periodStart: new DateOnly(2026, 6, 1),
            periodEnd: new DateOnly(2026, 6, 30));

        var ar = ArInvoiceProjection.ToCanonicalAr(
            rent, Tenant(), Chart(), Customer(), Account(), Account(),
            invoiceNumber: "INV-2026-05-15-CW-0001");

        var line = ar.Lines[0];
        Assert.Contains("2026-06-01", line.Description);
        Assert.Contains("2026-06-30", line.Description);
    }

    [Fact]
    public void ToCanonicalAr_AmountPaid_PropagatesAndBalanceDerives()
    {
        var rent = NewRentInvoice(amountDue: 1000m, amountPaid: 400m, status: Rc.InvoiceStatus.PartiallyPaid);
        var ar = ArInvoiceProjection.ToCanonicalAr(
            rent, Tenant(), Chart(), Customer(), Account(), Account(),
            invoiceNumber: "INV-2026-04-25-CW-0001");

        Assert.Equal(400m, ar.AmountPaid);
        Assert.Equal(600m, ar.Balance);
        Assert.Equal(InvoiceStatus.PartiallyPaid, ar.Status);
    }

    [Fact]
    public void ToCanonicalAr_Overpaid_FloorsBalanceAtZero()
    {
        // Rent-collection allows AmountPaid > AmountDue. Canonical AR projects
        // the balance as the non-negative remainder (overpayment surplus lives
        // on a credit-memo flow in the future; balance never goes negative).
        var rent = NewRentInvoice(amountDue: 1000m, amountPaid: 1200m, status: Rc.InvoiceStatus.Paid);
        var ar = ArInvoiceProjection.ToCanonicalAr(
            rent, Tenant(), Chart(), Customer(), Account(), Account(),
            invoiceNumber: "INV-2026-04-25-CW-0001");

        Assert.Equal(1200m, ar.AmountPaid);
        Assert.Equal(0m, ar.Balance);
        Assert.Equal(InvoiceStatus.Paid, ar.Status);
    }

    // ── MapStatus — all 6 rent-statuses ───────────────────────────────

    [Theory]
    [InlineData(Rc.InvoiceStatus.Draft,         InvoiceStatus.Draft)]
    [InlineData(Rc.InvoiceStatus.Open,          InvoiceStatus.Issued)]
    [InlineData(Rc.InvoiceStatus.PartiallyPaid, InvoiceStatus.PartiallyPaid)]
    [InlineData(Rc.InvoiceStatus.Paid,          InvoiceStatus.Paid)]
    [InlineData(Rc.InvoiceStatus.Overdue,       InvoiceStatus.Issued)]    // canonical derives overdue
    [InlineData(Rc.InvoiceStatus.Cancelled,     InvoiceStatus.Voided)]    // closest semantic match
    public void MapStatus_AllSixRentStatuses_MapToCanonical(Rc.InvoiceStatus rent, InvoiceStatus expected)
    {
        Assert.Equal(expected, ArInvoiceProjection.MapStatus(rent));
    }

    [Fact]
    public void ToCanonicalAr_NullRentInvoice_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ArInvoiceProjection.ToCanonicalAr(
                rentInvoice: null!,
                tenantId: Tenant(),
                chartId: Chart(),
                customerId: Customer(),
                arAccountId: Account(),
                rentIncomeAccountId: Account(),
                invoiceNumber: "INV-2026-04-25-CW-0001"));
    }

    [Fact]
    public void ToCanonicalAr_DraftRentInvoice_ProducesDraftCanonical_AcceptingEmptyNumber()
    {
        // Sanity: a Draft projection with an empty invoice number must
        // survive UpsertAsync — only non-Draft canonical invoices are
        // gated by the canonical number-format check.
        var rent = NewRentInvoice(status: Rc.InvoiceStatus.Draft);
        var ar = ArInvoiceProjection.ToCanonicalAr(
            rent, Tenant(), Chart(), Customer(), Account(), Account(),
            invoiceNumber: "");

        Assert.Equal(InvoiceStatus.Draft, ar.Status);
        Assert.Equal("", ar.InvoiceNumber);
    }
}
