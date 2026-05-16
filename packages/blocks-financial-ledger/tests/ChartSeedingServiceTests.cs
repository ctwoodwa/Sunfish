using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Seeds;
using Sunfish.Blocks.FinancialLedger.Services;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// W#60 P4 PR 5 — coverage for the
/// <see cref="InMemoryChartSeedingService"/> expansion logic.
/// </summary>
public sealed class ChartSeedingServiceTests
{
    [Fact]
    public async Task SeedChart_CreatesChartOfAccountsRecord()
    {
        var sut = new InMemoryChartSeedingService();
        var chart = await sut.SeedChartAsync(
            LegalEntityId.NewId(), "Acero Properties LLC",
            DefaultChartTemplates.RentalRealEstate);

        Assert.NotNull(chart);
        Assert.Equal("Acero Properties LLC", chart.Name);
        Assert.Equal("USD", chart.BaseCurrency);
        Assert.True(chart.IsActive);
        Assert.Single(sut.SeededCharts);
    }

    [Fact]
    public async Task SeedChart_CreatesAllTemplateAccounts()
    {
        var sut = new InMemoryChartSeedingService();
        await sut.SeedChartAsync(
            LegalEntityId.NewId(), "Test",
            DefaultChartTemplates.RentalRealEstate);

        Assert.Equal(DefaultChartTemplates.RentalRealEstate.Accounts.Count,
            sut.SeededAccounts.Count);
    }

    [Fact]
    public async Task SeedChart_TopologicalOrdering_AllParentsResolve()
    {
        var sut = new InMemoryChartSeedingService();
        await sut.SeedChartAsync(
            LegalEntityId.NewId(), "Test",
            DefaultChartTemplates.RentalRealEstate);

        var idsSeen = new HashSet<GLAccountId>();
        foreach (var a in sut.SeededAccounts)
        {
            if (a.ParentAccountId is { } parent)
            {
                Assert.Contains(parent, idsSeen);
            }
            idsSeen.Add(a.Id);
        }
    }

    [Fact]
    public async Task SeedChart_SetsBaseCurrencyFromArg()
    {
        var sut = new InMemoryChartSeedingService();
        var chart = await sut.SeedChartAsync(
            LegalEntityId.NewId(), "EuroChart",
            DefaultChartTemplates.RentalRealEstate,
            baseCurrency: "EUR");

        Assert.Equal("EUR", chart.BaseCurrency);
        // Seeded GL accounts inherit the chart's base currency.
        Assert.All(sut.SeededAccounts, a => Assert.Equal("EUR", a.Currency));
    }

    [Fact]
    public async Task SeedChart_NormalBalanceDerivedFromType_ForAllSeededAccounts()
    {
        var sut = new InMemoryChartSeedingService();
        await sut.SeedChartAsync(
            LegalEntityId.NewId(), "Test",
            DefaultChartTemplates.RentalRealEstate);

        foreach (var a in sut.SeededAccounts)
        {
            var expected = a.Type switch
            {
                GLAccountType.Asset or GLAccountType.Expense => NormalBalance.Debit,
                _ => NormalBalance.Credit,
            };
            Assert.Equal(expected, a.NormalBalance);
        }
    }

    [Fact]
    public async Task SeedChart_RentalRealEstate_Has37Accounts()
    {
        var sut = new InMemoryChartSeedingService();
        await sut.SeedChartAsync(
            LegalEntityId.NewId(), "Test",
            DefaultChartTemplates.RentalRealEstate);

        Assert.Equal(37, sut.SeededAccounts.Count);
    }

    [Fact]
    public async Task SeedChart_DanglingParentCode_Throws()
    {
        var badTemplate = new ChartTemplate(
            Name: "Bad",
            Description: "dangling parent",
            Accounts: new ChartTemplateAccount[]
            {
                new("1100", "Orphan", GLAccountType.Asset, AccountSubtype.BankAccount, ParentCode: "9999"),
            });
        var sut = new InMemoryChartSeedingService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SeedChartAsync(LegalEntityId.NewId(), "Bad", badTemplate));
    }

    [Fact]
    public async Task SeedChart_AllAccountsBelongToTheSeededChart()
    {
        var sut = new InMemoryChartSeedingService();
        var chart = await sut.SeedChartAsync(
            LegalEntityId.NewId(), "Test",
            DefaultChartTemplates.RentalRealEstate);

        Assert.All(sut.SeededAccounts, a => Assert.Equal(chart.Id, a.ChartId));
    }
}
