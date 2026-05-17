using System;
using System.Linq;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

public sealed class EnumerateForChartAsyncTests
{
    private static readonly ChartOfAccountsId ChartA = ChartOfAccountsId.NewId();
    private static readonly ChartOfAccountsId ChartB = ChartOfAccountsId.NewId();

    private static GLAccount Account(string code, ChartOfAccountsId chartId, bool isActive)
        => GLAccount.Create(GLAccountId.NewId(), chartId, code, $"acc-{code}", GLAccountType.Asset, AccountSubtype.BankAccount, "USD")
            with { IsActive = isActive };

    [Fact]
    public async Task EnumerateForChart_ReturnsOnlyChartAAccounts_ExcludesChartB()
    {
        var a1 = Account("1000", ChartA, true);
        var a2 = Account("1001", ChartA, true);
        var b1 = Account("2000", ChartB, true);
        var sut = new InMemoryAccountResolver(new[] { a1, a2, b1 });

        var result = await sut.EnumerateForChartAsync(ChartA);

        Assert.Equal(2, result.Count);
        Assert.Contains(a1, result);
        Assert.Contains(a2, result);
        Assert.DoesNotContain(b1, result);
    }

    [Fact]
    public async Task EnumerateForChart_IncludeInactiveFalse_FiltersInactive()
    {
        var active = Account("1000", ChartA, true);
        var inactive = Account("1001", ChartA, false);
        var sut = new InMemoryAccountResolver(new[] { active, inactive });

        var result = await sut.EnumerateForChartAsync(ChartA, includeInactive: false);

        Assert.Single(result);
        Assert.Contains(active, result);
    }

    [Fact]
    public async Task EnumerateForChart_IncludeInactiveTrue_IncludesInactive()
    {
        var active = Account("1000", ChartA, true);
        var inactive = Account("1001", ChartA, false);
        var sut = new InMemoryAccountResolver(new[] { active, inactive });

        var result = await sut.EnumerateForChartAsync(ChartA, includeInactive: true);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task EnumerateForChart_EmptyResolver_ReturnsEmpty()
    {
        var sut = new InMemoryAccountResolver();
        var result = await sut.EnumerateForChartAsync(ChartA);
        Assert.Empty(result);
    }
}
