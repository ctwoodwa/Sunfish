using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 2 coverage for the <see cref="TaxCodeRatesExtensions.GetRatesAsync"/>
/// query-based accessor — verifies cross-jurisdiction collection +
/// expected ordering.
/// </summary>
public class TaxCodeRatesExtensionsTests
{
    private static DateOnly D(int y, int m, int d) => new DateOnly(y, m, d);

    [Fact]
    public async Task GetRatesAsync_ReturnsRatesAcrossAllJurisdictions_OrderedByJurisdictionThenEffectiveDate()
    {
        var payable = GLAccount.Create(
            id: GLAccountId.NewId(),
            chartId: ChartOfAccountsId.NewId(),
            code: "2200",
            name: "State sales tax payable",
            type: GLAccountType.Liability,
            subtype: AccountSubtype.TaxesPayable,
            currency: "USD");
        var accounts = new InMemoryAccountResolver(new[] { payable });
        var lookup = new InMemoryTaxRateLookup(accounts);

        var code = TaxCode.Create(
            chartId: payable.ChartId!.Value,
            code: "US-VA-SALES",
            name: "Virginia sales tax",
            kind: TaxKind.Sales,
            application: TaxApplication.OnSubtotal);

        // Two jurisdictions, multiple rates each. Pre-pick IDs so we
        // can deterministically assert the sort order.
        var jA = new TaxJurisdictionId("aaa-jurisdiction");
        var jB = new TaxJurisdictionId("bbb-jurisdiction");

        await lookup.UpsertAsync(TaxRate.Create(code.Id, jB, 5m, D(2025, 1, 1), payable.Id, expiryDate: D(2025, 12, 31)));
        await lookup.UpsertAsync(TaxRate.Create(code.Id, jA, 4m, D(2025, 1, 1), payable.Id, expiryDate: D(2025, 12, 31)));
        await lookup.UpsertAsync(TaxRate.Create(code.Id, jB, 6m, D(2026, 1, 1), payable.Id));
        await lookup.UpsertAsync(TaxRate.Create(code.Id, jA, 4.5m, D(2026, 1, 1), payable.Id));

        var rates = await code.GetRatesAsync(lookup);

        Assert.Equal(4, rates.Count);
        // jA rows come first (Ordinal "aaa" < "bbb"), ordered by EffectiveDate.
        Assert.Equal(jA, rates[0].JurisdictionId);
        Assert.Equal(D(2025, 1, 1), rates[0].EffectiveDate);
        Assert.Equal(jA, rates[1].JurisdictionId);
        Assert.Equal(D(2026, 1, 1), rates[1].EffectiveDate);
        // Then jB rows.
        Assert.Equal(jB, rates[2].JurisdictionId);
        Assert.Equal(D(2025, 1, 1), rates[2].EffectiveDate);
        Assert.Equal(jB, rates[3].JurisdictionId);
        Assert.Equal(D(2026, 1, 1), rates[3].EffectiveDate);
    }
}
