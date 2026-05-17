using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

public class BillRepositoryTests
{
    private static TenantId Tenant() => new("acme");

    private static Bill NewBill(
        ChartOfAccountsId chart,
        PartyId vendor,
        string number = "VND-001",
        string? propertyId = null,
        string? externalRef = null) =>
        Bill.Create(
            tenantId: Tenant(),
            chartId: chart,
            billNumber: number,
            vendorId: vendor,
            billDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 5, 31),
            lines: new[] { BillLine.Create(BillId.NewId(), 1, "x", 1m, 100m, GLAccountId.NewId()) },
            apAccountId: GLAccountId.NewId(),
            propertyId: propertyId,
            externalRef: externalRef);

    [Fact]
    public async Task Upsert_RoundtripsViaGet()
    {
        var repo = new InMemoryBillRepository();
        var bill = NewBill(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(bill);

        var fetched = await repo.GetAsync(bill.Id);
        Assert.NotNull(fetched);
        Assert.Equal(bill.Id, fetched!.Id);
        Assert.Equal(100m, fetched.Total);
    }

    [Fact]
    public async Task GetByVendorBillNumber_ChartAndVendorScoped()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var alice = PartyId.NewId();
        var bob = PartyId.NewId();
        var aliceBill = NewBill(chart, alice, number: "INV-001");
        var bobBill = NewBill(chart, bob, number: "INV-001");
        await repo.UpsertAsync(aliceBill);
        await repo.UpsertAsync(bobBill);

        Assert.Equal(aliceBill.Id, (await repo.GetByVendorBillNumberAsync(chart, alice, "INV-001"))?.Id);
        Assert.Equal(bobBill.Id, (await repo.GetByVendorBillNumberAsync(chart, bob, "INV-001"))?.Id);

        // Same number on a different vendor: should NOT match alice's bill.
        var aliceLookupBobNumber = await repo.GetByVendorBillNumberAsync(chart, alice, "INV-001");
        Assert.NotEqual(bobBill.Id, aliceLookupBobNumber?.Id);
    }

    [Fact]
    public async Task GetByExternalRef_ChartScopedAndTombstoneExcluding()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var bill = NewBill(chart, PartyId.NewId(), externalRef: "erpnext:pinv:PINV-0042");
        await repo.UpsertAsync(bill);

        Assert.Equal(bill.Id, (await repo.GetByExternalRefAsync(chart, "erpnext:pinv:PINV-0042"))?.Id);

        await repo.SoftDeleteAsync(bill.Id, PartyId.NewId(), "test");
        Assert.Null(await repo.GetByExternalRefAsync(chart, "erpnext:pinv:PINV-0042"));
    }

    [Fact]
    public async Task ListByChart_ExcludesOtherCharts_AndTombstones()
    {
        var repo = new InMemoryBillRepository();
        var chartA = ChartOfAccountsId.NewId();
        var chartB = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(NewBill(chartA, PartyId.NewId(), "A1"));
        await repo.UpsertAsync(NewBill(chartA, PartyId.NewId(), "A2"));
        await repo.UpsertAsync(NewBill(chartB, PartyId.NewId(), "B1"));

        var aDead = NewBill(chartA, PartyId.NewId(), "A-DEAD");
        await repo.UpsertAsync(aDead);
        await repo.SoftDeleteAsync(aDead.Id, PartyId.NewId(), null);

        var aRows = await repo.ListByChartAsync(chartA);
        Assert.Equal(2, aRows.Count);
    }

    [Fact]
    public async Task ListByVendor_FiltersToOneVendor()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var alice = PartyId.NewId();
        var bob = PartyId.NewId();
        await repo.UpsertAsync(NewBill(chart, alice, "A1"));
        await repo.UpsertAsync(NewBill(chart, alice, "A2"));
        await repo.UpsertAsync(NewBill(chart, bob, "B1"));

        var aliceBills = await repo.ListByVendorAsync(chart, alice);
        Assert.Equal(2, aliceBills.Count);
    }

    [Fact]
    public async Task QueryOpen_OnlyOpenStatuses_OptionalFilters()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var vendor = PartyId.NewId();

        var b1 = NewBill(chart, vendor, "B-RECV", propertyId: "P1");
        b1 = b1 with { Status = BillStatus.Received };
        await repo.UpsertAsync(b1);

        var b2 = NewBill(chart, vendor, "B-APPR", propertyId: "P2");
        b2 = b2 with { Status = BillStatus.Approved };
        await repo.UpsertAsync(b2);

        var b3 = NewBill(chart, vendor, "B-PAID", propertyId: "P1");
        b3 = b3 with { Status = BillStatus.Paid, Balance = 0m };
        await repo.UpsertAsync(b3);

        var b4 = NewBill(chart, vendor, "B-DISP", propertyId: "P1");
        b4 = b4 with { Status = BillStatus.Disputed };
        await repo.UpsertAsync(b4);

        // All open: b1 + b2 (Disputed and Paid excluded).
        var allOpen = await repo.QueryOpenAsync(chart);
        Assert.Equal(2, allOpen.Count);
        Assert.Contains(allOpen, b => b.Id == b1.Id);
        Assert.Contains(allOpen, b => b.Id == b2.Id);

        // Filter by property P1: only b1.
        var p1Open = await repo.QueryOpenAsync(chart, propertyId: "P1");
        Assert.Single(p1Open);
        Assert.Equal(b1.Id, p1Open[0].Id);

        // Filter by vendor + property combo.
        var combo = await repo.QueryOpenAsync(chart, vendorId: vendor, propertyId: "P2");
        Assert.Single(combo);
        Assert.Equal(b2.Id, combo[0].Id);
    }

    [Fact]
    public async Task SoftDelete_OnUnknownId_ReturnsFalse_IsIdempotent()
    {
        var repo = new InMemoryBillRepository();
        Assert.False(await repo.SoftDeleteAsync(BillId.NewId(), PartyId.NewId(), null));

        var bill = NewBill(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(bill);
        Assert.True(await repo.SoftDeleteAsync(bill.Id, PartyId.NewId(), "first"));
        Assert.True(await repo.SoftDeleteAsync(bill.Id, PartyId.NewId(), "second")); // idempotent
    }

    [Fact]
    public async Task Upsert_OnTombstonedBill_Throws()
    {
        var repo = new InMemoryBillRepository();
        var bill = NewBill(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(bill);
        await repo.SoftDeleteAsync(bill.Id, PartyId.NewId(), null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.UpsertAsync(bill with { Notes = "should not save" }));
    }
}
