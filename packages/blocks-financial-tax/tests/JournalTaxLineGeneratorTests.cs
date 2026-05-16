using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 5 coverage for <see cref="JournalTaxLineGenerator"/>. Verifies
/// that pre-tax journal lines get expanded into balanced sets with
/// one tax-payable line per <see cref="TaxRateBreakdownLine"/> + the
/// originating <c>TaxCodeId</c> propagated for audit.
/// </summary>
public class JournalTaxLineGeneratorTests
{
    private static DateOnly D(int y, int m, int d) => new DateOnly(y, m, d);

    private sealed class Fx
    {
        public InMemoryTaxJurisdictionStore Jurisdictions { get; } = new();
        public InMemoryAccountResolver Accounts { get; } = new();
        public InMemoryTaxRateLookup Rates { get; }
        public InMemoryTaxCodeStore Codes { get; } = new();
        public InMemoryTaxJurisdictionResolver Resolver { get; }
        public TaxCalculationService Calc { get; }
        public JournalTaxLineGenerator Generator { get; }
        public FL.GLAccount Revenue { get; }
        public FL.GLAccount Payable { get; }
        public FL.ChartOfAccountsId Chart { get; }

        public Fx()
        {
            Chart = FL.ChartOfAccountsId.NewId();
            Revenue = FL.GLAccount.Create(
                id: FL.GLAccountId.NewId(),
                chartId: Chart,
                code: "4100",
                name: "Rental revenue",
                type: FL.GLAccountType.Revenue,
                subtype: FL.AccountSubtype.OperatingIncome,
                currency: "USD");
            Payable = FL.GLAccount.Create(
                id: FL.GLAccountId.NewId(),
                chartId: Chart,
                code: "2200",
                name: "Sales tax payable",
                type: FL.GLAccountType.Liability,
                subtype: FL.AccountSubtype.TaxesPayable,
                currency: "USD");
            Accounts.Upsert(Revenue);
            Accounts.Upsert(Payable);
            Resolver = new InMemoryTaxJurisdictionResolver(Jurisdictions);
            Rates = new InMemoryTaxRateLookup(Accounts);
            Calc = new TaxCalculationService(Rates, Resolver, Codes);
            Generator = new JournalTaxLineGenerator(Calc);
        }

        public async Task<TaxCode> SeedCodeAsync(
            TaxApplication application = TaxApplication.OnSubtotal,
            params decimal[] ratePcts)
        {
            var code = TaxCode.Create(Chart, "US-VA-SALES", "Virginia sales", TaxKind.Sales, application);
            await Codes.UpsertAsync(code);
            for (int i = 0; i < ratePcts.Length; i++)
            {
                var jurisdiction = TaxJurisdiction.Create(
                    level: i == 0 ? JurisdictionLevel.State : JurisdictionLevel.County,
                    isoCountry: "US",
                    region: "US-VA",
                    locality: i == 0 ? null : "Frederick County",
                    name: i == 0 ? "Virginia" : $"Locality{i}");
                await Jurisdictions.UpsertAsync(jurisdiction);
                await Rates.UpsertAsync(TaxRate.Create(
                    code.Id, jurisdiction.Id, ratePcts[i], D(2026, 1, 1), Payable.Id));
            }
            return code;
        }
    }

    private static TaxLocationContext Loc() =>
        new TaxLocationContext("US", "US-VA", "Frederick County");

    [Fact]
    public async Task Generate_NoTaxCodes_ReturnsLinesUnchanged()
    {
        var fx = new Fx();
        var revenueLine = new FL.JournalEntryLine(fx.Revenue.Id, 0m, 100m, "Rent — no tax");
        var result = await fx.Generator.GenerateAsync(
            new[] { revenueLine }, D(2026, 6, 1), Loc());

        Assert.Equal(0m, result.TotalTaxAmount);
        Assert.Single(result.AllLines);
        Assert.Equal(revenueLine, result.AllLines[0]);
        Assert.Empty(result.PerLineResults);
        Assert.Null(result.FirstError);
    }

    [Fact]
    public async Task Generate_OneLineWithTaxCode_AddsOneTaxPayableLine()
    {
        var fx = new Fx();
        var code = await fx.SeedCodeAsync(TaxApplication.OnSubtotal, 5m);
        var revenueLine = new FL.JournalEntryLine(fx.Revenue.Id, 0m, 100m, "Rent")
        {
            TaxCodeId = new FL.TaxCodeId(code.Id.Value),
        };

        var result = await fx.Generator.GenerateAsync(
            new[] { revenueLine }, D(2026, 6, 1), Loc());

        Assert.Null(result.FirstError);
        Assert.Equal(5.00m, result.TotalTaxAmount);
        Assert.Equal(2, result.AllLines.Count);
        var taxLine = result.AllLines[1];
        Assert.Equal(fx.Payable.Id, taxLine.AccountId);
        Assert.Equal(5.00m, taxLine.Credit);
        Assert.Equal(0m, taxLine.Debit);
    }

    [Fact]
    public async Task Generate_OneLineWithCompoundTaxCode_AddsOneTaxPayableLinePerJurisdiction()
    {
        var fx = new Fx();
        // Compound: 5% state + 2% county; on 100 = 5.00 then 2% on 105 = 2.10.
        var code = await fx.SeedCodeAsync(TaxApplication.Compound, 5m, 2m);
        var revenueLine = new FL.JournalEntryLine(fx.Revenue.Id, 0m, 100m, "Rent")
        {
            TaxCodeId = new FL.TaxCodeId(code.Id.Value),
        };

        var result = await fx.Generator.GenerateAsync(
            new[] { revenueLine }, D(2026, 6, 1), Loc());

        Assert.Null(result.FirstError);
        Assert.Equal(7.10m, result.TotalTaxAmount);
        // 1 pre-tax line + 2 tax-payable lines (one per jurisdiction).
        Assert.Equal(3, result.AllLines.Count);
    }

    [Fact]
    public async Task Generate_TwoLinesSameTaxCode_KeepsPerLinePayableLines_ForAuditTrail()
    {
        // Design: keep per-line (don't aggregate). Aggregation is a
        // reports-layer concern; line-by-line preserves the audit.
        var fx = new Fx();
        var code = await fx.SeedCodeAsync(TaxApplication.OnSubtotal, 5m);
        var lineA = new FL.JournalEntryLine(fx.Revenue.Id, 0m, 100m, "Rent A")
        {
            TaxCodeId = new FL.TaxCodeId(code.Id.Value),
        };
        var lineB = new FL.JournalEntryLine(fx.Revenue.Id, 0m, 200m, "Rent B")
        {
            TaxCodeId = new FL.TaxCodeId(code.Id.Value),
        };

        var result = await fx.Generator.GenerateAsync(
            new[] { lineA, lineB }, D(2026, 6, 1), Loc());

        Assert.Null(result.FirstError);
        // 5 + 10 = 15 total tax.
        Assert.Equal(15.00m, result.TotalTaxAmount);
        // 2 pre-tax lines + 2 tax-payable lines (one each, NOT aggregated).
        Assert.Equal(4, result.AllLines.Count);
    }

    [Fact]
    public async Task Generate_TaxCodeNotFound_ReturnsFirstErrorWithDetail()
    {
        var fx = new Fx();
        var lineWithBogusCode = new FL.JournalEntryLine(fx.Revenue.Id, 0m, 100m, "Rent")
        {
            TaxCodeId = new FL.TaxCodeId(Guid.NewGuid().ToString()),
        };
        var result = await fx.Generator.GenerateAsync(
            new[] { lineWithBogusCode }, D(2026, 6, 1), Loc());

        Assert.Equal(TaxCalculationError.TaxCodeNotFound, result.FirstError);
        Assert.NotNull(result.Detail);
        Assert.Equal(0m, result.TotalTaxAmount);
    }

    [Fact]
    public async Task Generate_TaxPayableLinesCarryOriginalLineTaxCodeId_ForAuditTrail()
    {
        var fx = new Fx();
        var code = await fx.SeedCodeAsync(TaxApplication.OnSubtotal, 5m);
        var ledgerCodeId = new FL.TaxCodeId(code.Id.Value);
        var revenueLine = new FL.JournalEntryLine(fx.Revenue.Id, 0m, 100m, "Rent")
        {
            TaxCodeId = ledgerCodeId,
        };

        var result = await fx.Generator.GenerateAsync(
            new[] { revenueLine }, D(2026, 6, 1), Loc());

        var taxLine = result.AllLines.Single(l => l.AccountId == fx.Payable.Id);
        Assert.Equal(ledgerCodeId, taxLine.TaxCodeId);
    }
}
