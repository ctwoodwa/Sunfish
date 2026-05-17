using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

public class ApAgingServiceTests
{
    private static TenantId Tenant() => new("acme");

    // ── AgingClassifier — pure-function unit tests ────────────────────

    [Theory]
    [InlineData(-30, AgingBucket.Current)]
    [InlineData(-1,  AgingBucket.Current)]
    [InlineData(0,   AgingBucket.Current)]
    [InlineData(1,   AgingBucket.Days0To30)]
    [InlineData(30,  AgingBucket.Days0To30)]
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
    [InlineData(BillStatus.Draft)]
    [InlineData(BillStatus.Paid)]
    [InlineData(BillStatus.Voided)]
    [InlineData(BillStatus.Disputed)] // hold — excluded from aging
    public void TryClassify_NonOpenStatus_ReturnsNull(BillStatus status)
    {
        var bill = NewBill(due: new DateOnly(2026, 5, 1), status: status, balance: 100m);
        Assert.Null(AgingClassifier.TryClassify(bill, new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void TryClassify_ZeroBalance_ReturnsNull()
    {
        var bill = NewBill(due: new DateOnly(2026, 5, 1), status: BillStatus.Received, balance: 0m);
        Assert.Null(AgingClassifier.TryClassify(bill, new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void TryClassify_ReceivedWithPositiveBalance_NotYetDue_IsCurrent()
    {
        var bill = NewBill(due: new DateOnly(2026, 6, 1), status: BillStatus.Received, balance: 100m);
        Assert.Equal(AgingBucket.Current, AgingClassifier.TryClassify(bill, new DateOnly(2026, 5, 15)));
    }

    // ── Service — projection over repository ──────────────────────────

    [Fact]
    public async Task GetAgingForChart_EmptyRepo_ReturnsEmptySummary()
    {
        var repo = new InMemoryBillRepository();
        var svc = new ApAgingService(repo);
        var summary = await svc.GetAgingForChartAsync(ChartOfAccountsId.NewId(), new DateOnly(2026, 5, 17));
        Assert.Equal(0m, summary.Total);
        Assert.Empty(summary.Rows);
    }

    [Fact]
    public async Task GetAgingForChart_DisputedBill_Excluded()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var open = NewBillInChart(chart, due: new DateOnly(2026, 4, 1), status: BillStatus.Received, balance: 100m);
        var disputed = NewBillInChart(chart, due: new DateOnly(2026, 4, 1), status: BillStatus.Disputed, balance: 200m);
        await repo.UpsertAsync(open);
        await repo.UpsertAsync(disputed);

        var summary = await new ApAgingService(repo).GetAgingForChartAsync(chart, new DateOnly(2026, 5, 17));
        Assert.Equal(100m, summary.Total);
        Assert.Single(summary.Rows);
        Assert.Equal(open.Id, summary.Rows[0].BillId);
    }

    [Fact]
    public async Task GetAgingForChart_BucketBoundary_30vs31_LandsInCorrectBucket()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(NewBillInChart(chart, due: new DateOnly(2026, 5, 1), status: BillStatus.Received, balance: 100m));
        await repo.UpsertAsync(NewBillInChart(chart, due: new DateOnly(2026, 4, 30), status: BillStatus.Received, balance: 200m));

        var summary = await new ApAgingService(repo).GetAgingForChartAsync(chart, new DateOnly(2026, 5, 31));
        Assert.Equal(100m, summary.Days0To30);
        Assert.Equal(200m, summary.Days31To60);
        Assert.Equal(300m, summary.Total);
    }

    [Fact]
    public async Task GetAgingForChart_AllBucketsPopulated_SumsCorrectly()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var asOf = new DateOnly(2026, 5, 17);
        await repo.UpsertAsync(NewBillInChart(chart, due: asOf.AddDays(10),  status: BillStatus.Received, balance: 100m));
        await repo.UpsertAsync(NewBillInChart(chart, due: asOf.AddDays(-15), status: BillStatus.Received, balance: 200m));
        await repo.UpsertAsync(NewBillInChart(chart, due: asOf.AddDays(-45), status: BillStatus.Approved, balance: 300m));
        await repo.UpsertAsync(NewBillInChart(chart, due: asOf.AddDays(-75), status: BillStatus.PartiallyPaid, balance: 400m));
        await repo.UpsertAsync(NewBillInChart(chart, due: asOf.AddDays(-120), status: BillStatus.Received, balance: 500m));

        var summary = await new ApAgingService(repo).GetAgingForChartAsync(chart, asOf);
        Assert.Equal(100m, summary.Current);
        Assert.Equal(200m, summary.Days0To30);
        Assert.Equal(300m, summary.Days31To60);
        Assert.Equal(400m, summary.Days61To90);
        Assert.Equal(500m, summary.Days90Plus);
        Assert.Equal(1500m, summary.Total);
        Assert.Equal(5, summary.Rows.Count);
    }

    [Fact]
    public async Task GetAgingForVendor_FiltersToOneVendor()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var acme = PartyId.NewId();
        var beta = PartyId.NewId();
        await repo.UpsertAsync(NewBillInChart(chart, due: new DateOnly(2026, 5, 1), status: BillStatus.Received, balance: 100m, vendor: acme));
        await repo.UpsertAsync(NewBillInChart(chart, due: new DateOnly(2026, 5, 1), status: BillStatus.Received, balance: 200m, vendor: beta));

        var acmeSummary = await new ApAgingService(repo).GetAgingForVendorAsync(chart, acme, new DateOnly(2026, 5, 17));
        Assert.Equal(100m, acmeSummary.Total);
        Assert.Single(acmeSummary.Rows);
        Assert.Equal(acme, acmeSummary.Rows[0].VendorId);
    }

    [Fact]
    public async Task GetAgingForProperty_FiltersToOneProperty_AndEmptyOnUnknown()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(NewBillInChart(chart, due: new DateOnly(2026, 5, 1), status: BillStatus.Received, balance: 100m, property: "100-MAIN"));
        await repo.UpsertAsync(NewBillInChart(chart, due: new DateOnly(2026, 5, 1), status: BillStatus.Received, balance: 200m, property: "200-OAK"));

        var svc = new ApAgingService(repo);
        var mainSummary = await svc.GetAgingForPropertyAsync(chart, "100-MAIN", new DateOnly(2026, 5, 17));
        Assert.Equal(100m, mainSummary.Total);

        var unknownSummary = await svc.GetAgingForPropertyAsync(chart, "999-NOWHERE", new DateOnly(2026, 5, 17));
        Assert.Equal(0m, unknownSummary.Total);

        var emptyIdSummary = await svc.GetAgingForPropertyAsync(chart, "", new DateOnly(2026, 5, 17));
        Assert.Equal(0m, emptyIdSummary.Total);
    }

    [Fact]
    public async Task Summary_Rows_AreSortedByDueDateThenBillNumber()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(NewBillInChart(chart, due: new DateOnly(2026, 4, 15), status: BillStatus.Received, balance: 50m, number: "B-VENDOR"));
        await repo.UpsertAsync(NewBillInChart(chart, due: new DateOnly(2026, 4, 15), status: BillStatus.Received, balance: 75m, number: "A-VENDOR"));
        await repo.UpsertAsync(NewBillInChart(chart, due: new DateOnly(2026, 4, 1),  status: BillStatus.Received, balance: 100m, number: "C-VENDOR"));

        var summary = await new ApAgingService(repo).GetAgingForChartAsync(chart, new DateOnly(2026, 5, 17));
        Assert.Equal(3, summary.Rows.Count);
        Assert.Equal("C-VENDOR", summary.Rows[0].BillNumber); // earliest due
        Assert.Equal("A-VENDOR", summary.Rows[1].BillNumber); // tie → ordinal sort
        Assert.Equal("B-VENDOR", summary.Rows[2].BillNumber);
    }

    [Fact]
    public void AgingSummary_EmptyFactory_MaterializesZeroes()
    {
        var s = AgingSummary.Empty(new DateOnly(2026, 5, 17));
        Assert.Equal(0m, s.Total);
        Assert.Empty(s.Rows);
    }

    // ── Test fixtures ─────────────────────────────────────────────────

    private static Bill NewBill(DateOnly due, BillStatus status, decimal balance) =>
        NewBillInChart(ChartOfAccountsId.NewId(), due, status, balance);

    private static Bill NewBillInChart(
        ChartOfAccountsId chart,
        DateOnly due,
        BillStatus status,
        decimal balance,
        PartyId? vendor = null,
        string? property = null,
        string number = "VND-001")
    {
        var bill = Bill.Create(
            tenantId: Tenant(),
            chartId: chart,
            billNumber: number,
            vendorId: vendor ?? PartyId.NewId(),
            billDate: due.AddDays(-30),
            dueDate: due,
            lines: new[] { BillLine.Create(BillId.NewId(), 1, "x", 1m, balance, GLAccountId.NewId()) },
            apAccountId: GLAccountId.NewId(),
            propertyId: property);
        return bill with { Status = status, Balance = balance };
    }
}
