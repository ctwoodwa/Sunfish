using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 1 coverage for the <see cref="TaxCode"/> record per Stage 02
/// §3.12. Contract-level only — full upsert / version-bump semantics
/// exercise the <c>ITaxCodeStore</c> in a later PR.
/// </summary>
public class TaxCodeTests
{
    private static ChartOfAccountsId NewChart() => ChartOfAccountsId.NewId();

    [Fact]
    public void Create_PopulatesAllFields()
    {
        var chart = NewChart();
        var code = TaxCode.Create(
            chartId: chart,
            code: "US-VA-SALES",
            name: "Virginia retail sales tax",
            kind: TaxKind.Sales,
            application: TaxApplication.OnSubtotal,
            notes: "Combined state + Frederick County rate land in PR 2.");

        Assert.Equal(chart, code.ChartId);
        Assert.Equal("US-VA-SALES", code.Code);
        Assert.Equal("Virginia retail sales tax", code.Name);
        Assert.Equal(TaxKind.Sales, code.Kind);
        Assert.Equal(TaxApplication.OnSubtotal, code.Application);
        Assert.Equal("Combined state + Frederick County rate land in PR 2.", code.Notes);
    }

    [Fact]
    public void Create_VersionStartsAt1()
    {
        var code = TaxCode.Create(NewChart(), "US-VA-SALES", "Virginia sales",
            TaxKind.Sales, TaxApplication.OnSubtotal);

        Assert.Equal(1, code.Version);
    }

    [Fact]
    public void Create_KindExempt_IsAllowed()
    {
        var code = TaxCode.Create(NewChart(), "EXEMPT", "Tax-exempt",
            TaxKind.Exempt, TaxApplication.OnSubtotal);

        Assert.Equal(TaxKind.Exempt, code.Kind);
    }

    [Fact]
    public void Create_DefaultsIsActiveTrue()
    {
        var code = TaxCode.Create(NewChart(), "US-VA-SALES", "Virginia sales",
            TaxKind.Sales, TaxApplication.OnSubtotal);

        Assert.True(code.IsActive);
        Assert.Null(code.DeletedAtUtc);
    }
}
