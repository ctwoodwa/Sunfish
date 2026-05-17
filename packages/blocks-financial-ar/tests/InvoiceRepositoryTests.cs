using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class InvoiceRepositoryTests
{
    private static TenantId Tenant() => new("acme");

    private static Invoice NewInvoice(
        ChartOfAccountsId chart,
        PartyId customer,
        string number = "INV-001") =>
        Invoice.Create(
            tenantId: Tenant(),
            chartId: chart,
            invoiceNumber: number,
            customerId: customer,
            issueDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 5, 31),
            lines: new[]
            {
                InvoiceLine.Create(InvoiceId.NewId(), 1, "x", 1m, 100m, GLAccountId.NewId()),
            },
            arAccountId: GLAccountId.NewId());

    [Fact]
    public async Task Upsert_RoundtripsViaGet()
    {
        var repo = new InMemoryInvoiceRepository();
        var inv = NewInvoice(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(inv);

        var fetched = await repo.GetAsync(inv.Id);
        Assert.NotNull(fetched);
        Assert.Equal(inv.Id, fetched!.Id);
        Assert.Equal(100m, fetched.Total);
    }

    [Fact]
    public async Task GetByNumber_ChartScoped_OnlyMatchesSameChart()
    {
        var repo = new InMemoryInvoiceRepository();
        var chartA = ChartOfAccountsId.NewId();
        var chartB = ChartOfAccountsId.NewId();
        var customer = PartyId.NewId();
        var aInv = NewInvoice(chartA, customer, "INV-001");
        var bInv = NewInvoice(chartB, customer, "INV-001");
        await repo.UpsertAsync(aInv);
        await repo.UpsertAsync(bInv);

        var foundA = await repo.GetByNumberAsync(chartA, "INV-001");
        var foundB = await repo.GetByNumberAsync(chartB, "INV-001");

        Assert.Equal(aInv.Id, foundA?.Id);
        Assert.Equal(bInv.Id, foundB?.Id);
        Assert.NotEqual(foundA?.Id, foundB?.Id);
    }

    [Fact]
    public async Task GetByNumber_TombstonedRow_ReturnsNull()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var inv = NewInvoice(chart, PartyId.NewId(), "INV-DEAD");
        await repo.UpsertAsync(inv);
        await repo.SoftDeleteAsync(inv.Id, PartyId.NewId(), "test cleanup");

        Assert.Null(await repo.GetByNumberAsync(chart, "INV-DEAD"));
        Assert.Null(await repo.GetAsync(inv.Id));
    }

    [Fact]
    public async Task ListByChart_ExcludesOtherCharts_AndTombstones()
    {
        var repo = new InMemoryInvoiceRepository();
        var chartA = ChartOfAccountsId.NewId();
        var chartB = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(NewInvoice(chartA, PartyId.NewId(), "A1"));
        await repo.UpsertAsync(NewInvoice(chartA, PartyId.NewId(), "A2"));
        await repo.UpsertAsync(NewInvoice(chartB, PartyId.NewId(), "B1"));

        var aDead = NewInvoice(chartA, PartyId.NewId(), "A-DEAD");
        await repo.UpsertAsync(aDead);
        await repo.SoftDeleteAsync(aDead.Id, PartyId.NewId(), null);

        var aRows = await repo.ListByChartAsync(chartA);
        Assert.Equal(2, aRows.Count);
        Assert.DoesNotContain(aRows, x => x.Id == aDead.Id);
    }

    [Fact]
    public async Task ListByCustomer_FiltersToOneCustomer()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var alice = PartyId.NewId();
        var bob = PartyId.NewId();
        await repo.UpsertAsync(NewInvoice(chart, alice, "A1"));
        await repo.UpsertAsync(NewInvoice(chart, alice, "A2"));
        await repo.UpsertAsync(NewInvoice(chart, bob, "B1"));

        var aliceInvoices = await repo.ListByCustomerAsync(chart, alice);
        Assert.Equal(2, aliceInvoices.Count);
        Assert.All(aliceInvoices, i => Assert.Equal(alice, i.CustomerId));
    }

    [Fact]
    public async Task SoftDelete_OnUnknownId_ReturnsFalse()
    {
        var repo = new InMemoryInvoiceRepository();
        var result = await repo.SoftDeleteAsync(InvoiceId.NewId(), PartyId.NewId(), null);
        Assert.False(result);
    }

    [Fact]
    public async Task SoftDelete_IsIdempotent()
    {
        var repo = new InMemoryInvoiceRepository();
        var inv = NewInvoice(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(inv);

        Assert.True(await repo.SoftDeleteAsync(inv.Id, PartyId.NewId(), "first"));
        Assert.True(await repo.SoftDeleteAsync(inv.Id, PartyId.NewId(), "second"));
    }

    [Fact]
    public async Task Upsert_OnTombstonedInvoice_Throws()
    {
        var repo = new InMemoryInvoiceRepository();
        var inv = NewInvoice(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(inv);
        await repo.SoftDeleteAsync(inv.Id, PartyId.NewId(), null);

        var bumped = inv with { Notes = "should not save" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpsertAsync(bumped));
    }
}
