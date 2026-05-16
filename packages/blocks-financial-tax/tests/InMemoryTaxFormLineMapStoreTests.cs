using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

public class InMemoryTaxFormLineMapStoreTests
{
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();

    [Fact]
    public async Task SeedScheduleE_OnEmptyStore_Inserts20Rows()
    {
        var store = new InMemoryTaxFormLineMapStore();
        var chart = Chart();

        var inserted = await store.SeedScheduleEAsync(chart, taxYear: 2026);

        Assert.Equal(20, inserted);
    }

    [Fact]
    public async Task SeedScheduleE_OnPreSeededStore_ReturnsZeroAndPreservesExisting()
    {
        var store = new InMemoryTaxFormLineMapStore();
        var chart = Chart();
        await store.SeedScheduleEAsync(chart, taxYear: 2026);
        var before = await store.GetForFormAsync(chart, TaxFormKind.ScheduleE, 2026);

        var inserted = await store.SeedScheduleEAsync(chart, taxYear: 2026);
        var after = await store.GetForFormAsync(chart, TaxFormKind.ScheduleE, 2026);

        Assert.Equal(0, inserted);
        Assert.Equal(before.Count, after.Count);
        // Ids must match — preservation, not re-creation.
        Assert.Equal(
            before.Select(r => r.Id.Value).OrderBy(v => v, StringComparer.Ordinal),
            after.Select(r => r.Id.Value).OrderBy(v => v, StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetForForm_ScheduleE_2026_Returns20Rows()
    {
        var store = new InMemoryTaxFormLineMapStore();
        var chart = Chart();
        await store.SeedScheduleEAsync(chart, taxYear: 2026);

        var rows = await store.GetForFormAsync(chart, TaxFormKind.ScheduleE, 2026);

        Assert.Equal(20, rows.Count);
    }

    [Fact]
    public async Task GetForForm_ScheduleE_2027_OnUnseededYear_ReturnsEmpty()
    {
        var store = new InMemoryTaxFormLineMapStore();
        var chart = Chart();
        await store.SeedScheduleEAsync(chart, taxYear: 2026);

        var rows = await store.GetForFormAsync(chart, TaxFormKind.ScheduleE, 2027);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Upsert_BumpsVersion_OnReUpsert()
    {
        var store = new InMemoryTaxFormLineMapStore();
        var chart = Chart();
        var initial = TaxFormLineMap.Create(
            chartId: chart,
            formKind: TaxFormKind.ScheduleE,
            taxYear: 2026,
            line: "Line5",
            description: "Advertising",
            selectors: new[] { new TaxAccountSelector(AccountCode: "5100") },
            perPropertyDimension: true,
            isProvisional: true);
        await store.UpsertAsync(initial);

        var edited = initial with { Description = "Advertising (edited)" };
        await store.UpsertAsync(edited);

        var fetched = await store.GetAsync(initial.Id);
        Assert.NotNull(fetched);
        Assert.Equal(2, fetched!.Version);
        Assert.Equal("Advertising (edited)", fetched.Description);
    }
}
