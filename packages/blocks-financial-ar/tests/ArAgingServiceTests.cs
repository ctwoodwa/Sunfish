using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class ArAgingServiceTests
{
    private static TenantId Tenant() => new("acme");

    // ── AgingClassifier — pure-function unit tests ────────────────────

    [Theory]
    [InlineData(-30, AgingBucket.Current)]   // not yet due
    [InlineData(-1,  AgingBucket.Current)]   // due tomorrow
    [InlineData(0,   AgingBucket.Current)]   // due today
    [InlineData(1,   AgingBucket.Days0To30)] // 1 day overdue
    [InlineData(30,  AgingBucket.Days0To30)] // boundary high end
    [InlineData(31,  AgingBucket.Days31To60)]
    [InlineData(60,  AgingBucket.Days31To60)]
    [InlineData(61,  AgingBucket.Days61To90)]
    [InlineData(90,  AgingBucket.Days61To90)]
    [InlineData(91,  AgingBucket.Days90Plus)]
    [InlineData(365, AgingBucket.Days90Plus)]
    public void ClassifyByDays_BucketBoundaries_AreInclusiveOnLowerSide(int daysPastDue, AgingBucket expected)
    {
        Assert.Equal(expected, AgingClassifier.ClassifyByDays(daysPastDue));
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.WrittenOff)]
    public void TryClassify_NonOpenStatus_ReturnsNull(InvoiceStatus status)
    {
        var inv = NewInvoice(due: new DateOnly(2026, 5, 1), status: status, balance: 100m);
        Assert.Null(AgingClassifier.TryClassify(inv, new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void TryClassify_ZeroBalance_ReturnsNull()
    {
        var inv = NewInvoice(due: new DateOnly(2026, 5, 1), status: InvoiceStatus.Issued, balance: 0m);
        Assert.Null(AgingClassifier.TryClassify(inv, new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void TryClassify_NegativeBalance_ReturnsNull()
    {
        var inv = NewInvoice(due: new DateOnly(2026, 5, 1), status: InvoiceStatus.Issued, balance: -50m);
        Assert.Null(AgingClassifier.TryClassify(inv, new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void TryClassify_IssuedWithPositiveBalance_NotYetDue_IsCurrent()
    {
        var inv = NewInvoice(due: new DateOnly(2026, 6, 1), status: InvoiceStatus.Issued, balance: 100m);
        Assert.Equal(AgingBucket.Current, AgingClassifier.TryClassify(inv, new DateOnly(2026, 5, 15)));
    }

    // ── Service — projection over repository ──────────────────────────

    [Fact]
    public async Task GetAgingForChart_EmptyRepo_ReturnsEmptySummary()
    {
        var repo = new InMemoryInvoiceRepository();
        var svc = new ArAgingService(repo);
        var summary = await svc.GetAgingForChartAsync(ChartOfAccountsId.NewId(), new DateOnly(2026, 5, 17));
        Assert.Equal(0m, summary.Total);
        Assert.Empty(summary.Rows);
    }

    [Fact]
    public async Task GetAgingForChart_TombstonedInvoice_Excluded()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var inv = NewInvoiceInChart(chart, due: new DateOnly(2026, 5, 1), status: InvoiceStatus.Issued, balance: 100m);
        await repo.UpsertAsync(inv);
        await repo.SoftDeleteAsync(inv.Id, PartyId.NewId(), "test");

        var summary = await new ArAgingService(repo).GetAgingForChartAsync(chart, new DateOnly(2026, 6, 1));
        Assert.Equal(0m, summary.Total);
    }

    [Fact]
    public async Task GetAgingForChart_BucketBoundary_30vs31_LandsInCorrectBucket()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        // asOf May 31 → exactly 30 days past May 1.
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 5, 1), status: InvoiceStatus.Issued, balance: 100m));
        // asOf May 31 → exactly 31 days past April 30.
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 4, 30), status: InvoiceStatus.Issued, balance: 200m));

        var summary = await new ArAgingService(repo).GetAgingForChartAsync(chart, new DateOnly(2026, 5, 31));
        Assert.Equal(100m, summary.Days0To30);
        Assert.Equal(200m, summary.Days31To60);
        Assert.Equal(300m, summary.Total);
    }

    [Fact]
    public async Task GetAgingForChart_AllBucketsPopulated_SumsCorrectly()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var asOf = new DateOnly(2026, 5, 17);
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: asOf.AddDays(10),  status: InvoiceStatus.Issued, balance: 100m));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: asOf.AddDays(-15), status: InvoiceStatus.Issued, balance: 200m));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: asOf.AddDays(-45), status: InvoiceStatus.PartiallyPaid, balance: 300m));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: asOf.AddDays(-75), status: InvoiceStatus.Issued, balance: 400m));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: asOf.AddDays(-120), status: InvoiceStatus.Issued, balance: 500m));

        var summary = await new ArAgingService(repo).GetAgingForChartAsync(chart, asOf);
        Assert.Equal(100m, summary.Current);
        Assert.Equal(200m, summary.Days0To30);
        Assert.Equal(300m, summary.Days31To60);
        Assert.Equal(400m, summary.Days61To90);
        Assert.Equal(500m, summary.Days90Plus);
        Assert.Equal(1500m, summary.Total);
        Assert.Equal(5, summary.Rows.Count);
    }

    [Fact]
    public async Task GetAgingForChart_PaidInvoice_DoesNotAppear()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 4, 1), status: InvoiceStatus.Paid, balance: 0m));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 4, 1), status: InvoiceStatus.Issued, balance: 50m));

        var summary = await new ArAgingService(repo).GetAgingForChartAsync(chart, new DateOnly(2026, 5, 17));
        Assert.Equal(50m, summary.Total);
        Assert.Single(summary.Rows);
    }

    [Fact]
    public async Task GetAgingForCustomer_FiltersToOneCustomer()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var alice = PartyId.NewId();
        var bob = PartyId.NewId();
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 5, 1), status: InvoiceStatus.Issued, balance: 100m, customer: alice));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 5, 1), status: InvoiceStatus.Issued, balance: 200m, customer: bob));

        var aliceSummary = await new ArAgingService(repo).GetAgingForCustomerAsync(chart, alice, new DateOnly(2026, 5, 17));
        Assert.Equal(100m, aliceSummary.Total);
        Assert.Single(aliceSummary.Rows);
        Assert.Equal(alice, aliceSummary.Rows[0].CustomerId);
    }

    [Fact]
    public async Task GetAgingForProperty_FiltersToOneProperty_AndEmptyOnUnknown()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 5, 1), status: InvoiceStatus.Issued, balance: 100m, property: "100-MAIN"));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 5, 1), status: InvoiceStatus.Issued, balance: 200m, property: "200-OAK"));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 5, 1), status: InvoiceStatus.Issued, balance: 50m, property: null));

        var svc = new ArAgingService(repo);
        var mainSummary = await svc.GetAgingForPropertyAsync(chart, "100-MAIN", new DateOnly(2026, 5, 17));
        Assert.Equal(100m, mainSummary.Total);

        var unknownSummary = await svc.GetAgingForPropertyAsync(chart, "999-NOWHERE", new DateOnly(2026, 5, 17));
        Assert.Equal(0m, unknownSummary.Total);

        var emptyIdSummary = await svc.GetAgingForPropertyAsync(chart, "", new DateOnly(2026, 5, 17));
        Assert.Equal(0m, emptyIdSummary.Total);
    }

    [Fact]
    public async Task Summary_Rows_AreSortedByDueDateThenInvoiceNumber()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        // Numbers carry replica-tag prefixes B / A / C so the ordinal compare
        // on the canonical format produces A < B < C when due dates tie.
        var numB = Canonical("B");
        var numA = Canonical("A");
        var numC = Canonical("C");
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 4, 15), status: InvoiceStatus.Issued, balance: 50m, number: numB));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 4, 15), status: InvoiceStatus.Issued, balance: 75m, number: numA));
        await repo.UpsertAsync(NewInvoiceInChart(chart, due: new DateOnly(2026, 4, 1),  status: InvoiceStatus.Issued, balance: 100m, number: numC));

        var summary = await new ArAgingService(repo).GetAgingForChartAsync(chart, new DateOnly(2026, 5, 17));
        Assert.Equal(3, summary.Rows.Count);
        Assert.Equal(numC, summary.Rows[0].InvoiceNumber); // earliest due
        Assert.Equal(numA, summary.Rows[1].InvoiceNumber); // tie on due → ordinal sort puts A first
        Assert.Equal(numB, summary.Rows[2].InvoiceNumber);
    }

    [Fact]
    public void AgingSummary_EmptyFactory_MaterializesZeroes()
    {
        var s = AgingSummary.Empty(new DateOnly(2026, 5, 17));
        Assert.Equal(0m, s.Total);
        Assert.Empty(s.Rows);
    }

    // ── Test fixtures ─────────────────────────────────────────────────

    private static Invoice NewInvoice(
        DateOnly due,
        InvoiceStatus status,
        decimal balance,
        DateOnly? issue = null) =>
        NewInvoiceInChart(ChartOfAccountsId.NewId(), due, status, balance, issue: issue);

    private static int _seq;

    /// <summary>
    /// Build a canonical-format invoice number. Tests that need ordering by
    /// number (e.g. tie-breaking sort) pass an explicit <paramref name="replicaTag"/>
    /// (e.g. "A" / "B" / "C") into the replica slot so the resulting numbers
    /// sort predictably ordinal.
    /// </summary>
    private static string Canonical(string replicaTag = "CW")
    {
        var n = Interlocked.Increment(ref _seq);
        return $"INV-2026-05-17-{replicaTag}-{n:D4}";
    }

    private static Invoice NewInvoiceInChart(
        ChartOfAccountsId chart,
        DateOnly due,
        InvoiceStatus status,
        decimal balance,
        PartyId? customer = null,
        string? property = null,
        string number = "",
        DateOnly? issue = null)
    {
        var actualNumber = string.IsNullOrEmpty(number) ? Canonical() : number;
        var inv = Invoice.Create(
            tenantId: Tenant(),
            chartId: chart,
            invoiceNumber: actualNumber,
            customerId: customer ?? PartyId.NewId(),
            issueDate: issue ?? due.AddDays(-30),
            dueDate: due,
            lines: new[] { InvoiceLine.Create(InvoiceId.NewId(), 1, "x", 1m, balance, GLAccountId.NewId()) },
            arAccountId: GLAccountId.NewId(),
            propertyId: property);
        return inv with { Status = status, Balance = balance };
    }
}
