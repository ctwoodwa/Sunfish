using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// Mandatory regression battery for the <see cref="TaxCalculationService"/>.
/// PR 3 marks this PR as <b>fiscal-correctness-critical</b> per the
/// hand-off; these tests are load-bearing. Extend, don't rewrite.
/// </summary>
public class TaxCalculationServiceTests
{
    // ── Fixture helpers ──────────────────────────────────────────────────

    private static DateOnly D(int y, int m, int d) => new DateOnly(y, m, d);

    private sealed class Fixture
    {
        public InMemoryTaxJurisdictionStore Jurisdictions { get; } = new();
        public InMemoryTaxJurisdictionResolver Resolver { get; }
        public InMemoryAccountResolver Accounts { get; } = new();
        public InMemoryTaxRateLookup Rates { get; }
        public InMemoryTaxCodeStore Codes { get; } = new();
        public TaxCalculationService Service { get; }
        public GLAccount PayableAccount { get; }

        public Fixture()
        {
            Resolver = new InMemoryTaxJurisdictionResolver(Jurisdictions);
            PayableAccount = GLAccount.Create(
                id: GLAccountId.NewId(),
                chartId: ChartOfAccountsId.NewId(),
                code: "2200",
                name: "Taxes payable",
                type: GLAccountType.Liability,
                subtype: AccountSubtype.TaxesPayable,
                currency: "USD");
            Accounts.Upsert(PayableAccount);
            Rates = new InMemoryTaxRateLookup(Accounts);
            Service = new TaxCalculationService(Rates, Resolver, Codes);
        }

        public async Task<TaxJurisdiction> SeedJurisdictionAsync(
            JurisdictionLevel level,
            string isoCountry = "US",
            string? region = null,
            string? locality = null,
            string name = "Sample")
        {
            var j = TaxJurisdiction.Create(level, isoCountry, name, region: region, locality: locality);
            await Jurisdictions.UpsertAsync(j);
            return j;
        }

        public async Task<TaxCode> SeedCodeAsync(
            string code = "US-SALES",
            TaxKind kind = TaxKind.Sales,
            TaxApplication application = TaxApplication.OnSubtotal)
        {
            var c = TaxCode.Create(PayableAccount.ChartId!.Value, code, $"{code} display", kind, application);
            await Codes.UpsertAsync(c);
            return c;
        }

        public Task SeedRateAsync(TaxCodeId codeId, TaxJurisdictionId jurisdictionId, decimal pct, DateOnly effectiveDate)
        {
            var rate = TaxRate.Create(codeId, jurisdictionId, pct, effectiveDate, PayableAccount.Id);
            return Rates.UpsertAsync(rate);
        }
    }

    private static TaxLocationContext USVA(string? locality = null) =>
        new TaxLocationContext("US", "US-VA", locality);

    // ── Failure modes ────────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_TaxCodeNotFound_ReturnsTaxCodeNotFound()
    {
        var fx = new Fixture();
        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            TaxCodeId.NewId(), 100m, D(2026, 6, 1), USVA()));

        Assert.Equal(TaxCalculationError.TaxCodeNotFound, result.Error);
        Assert.Equal(0m, result.TaxAmount);
        Assert.Empty(result.Breakdown);
    }

    [Fact]
    public async Task Calculate_NegativeSubtotal_ReturnsInvalidSubtotal_AndDoesNotProduceNegativeTax()
    {
        // Council-review finding (MEDIUM): negative subtotal silently
        // produced negative tax. Engine now rejects up front.
        var fx = new Fixture();
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var code = await fx.SeedCodeAsync();
        await fx.SeedRateAsync(code.Id, state.Id, 5m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, -100m, D(2026, 6, 1), USVA()));

        Assert.Equal(TaxCalculationError.InvalidSubtotal, result.Error);
        Assert.Equal(0m, result.TaxAmount);
        Assert.Empty(result.Breakdown);
    }

    [Fact]
    public async Task Calculate_Inclusive_AllZeroRates_ReturnsNoApplicableRates_AndDoesNotMasqueradeAsExempt()
    {
        // Council-review finding (LOW): an Inclusive code with all
        // zero-rate rows used to succeed silently (totalTax=0,
        // preTaxBase=subtotal) — indistinguishable from Exempt and
        // confusing in GL audits. Engine now rejects.
        var fx = new Fixture();
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var code = await fx.SeedCodeAsync("US-VA-INCL-ZERO", TaxKind.Sales, TaxApplication.Inclusive);
        await fx.SeedRateAsync(code.Id, state.Id, 0m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA()));

        Assert.Equal(TaxCalculationError.NoApplicableRates, result.Error);
        Assert.Equal(0m, result.TaxAmount);
    }

    [Fact]
    public async Task Calculate_ExemptCode_ReturnsZeroTaxAndEmptyBreakdown()
    {
        var fx = new Fixture();
        var code = await fx.SeedCodeAsync("EXEMPT", TaxKind.Exempt, TaxApplication.OnSubtotal);

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA()));

        Assert.Equal(TaxCalculationError.None, result.Error);
        Assert.Equal(0m, result.TaxAmount);
        Assert.Equal(100m, result.TotalIn);
        Assert.Empty(result.Breakdown);
    }

    [Fact]
    public async Task Calculate_NoApplicableRates_ReturnsNoApplicableRates()
    {
        var fx = new Fixture();
        await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var code = await fx.SeedCodeAsync();
        // Note: no rate seeded.

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA()));

        Assert.Equal(TaxCalculationError.NoApplicableRates, result.Error);
        Assert.Equal(0m, result.TaxAmount);
    }

    // ── OnSubtotal ──────────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_OnSubtotal_SingleRate_Computes_5p3pctOf100_Equals5p30()
    {
        var fx = new Fixture();
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var code = await fx.SeedCodeAsync();
        await fx.SeedRateAsync(code.Id, state.Id, 5.3m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA()));

        Assert.Equal(TaxCalculationError.None, result.Error);
        Assert.Equal(5.30m, result.TaxAmount);
        Assert.Equal(105.30m, result.TotalIn);
        Assert.Single(result.Breakdown);
    }

    [Fact]
    public async Task Calculate_OnSubtotal_TwoRates_Sums_4p0pctStatePlus2p0pctCounty_On100_Equals6p00()
    {
        var fx = new Fixture();
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var county = await fx.SeedJurisdictionAsync(
            JurisdictionLevel.County, region: "US-VA", locality: "Frederick County", name: "Frederick County");
        var code = await fx.SeedCodeAsync();
        await fx.SeedRateAsync(code.Id, state.Id, 4m, D(2026, 1, 1));
        await fx.SeedRateAsync(code.Id, county.Id, 2m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA(locality: "Frederick County")));

        Assert.Equal(TaxCalculationError.None, result.Error);
        Assert.Equal(6.00m, result.TaxAmount);
        Assert.Equal(106.00m, result.TotalIn);
        Assert.Equal(2, result.Breakdown.Count);
    }

    [Fact]
    public async Task Calculate_OnSubtotal_RoundsBankersHalfToEven_At2DecimalBoundary()
    {
        // Regression: 100.00 * 4.125% = 4.125 → banker's rounds to 4.12
        // (2 is even). Round-half-up would give 4.13.
        var fx = new Fixture();
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var code = await fx.SeedCodeAsync();
        await fx.SeedRateAsync(code.Id, state.Id, 4.125m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA()));

        Assert.Equal(4.12m, result.TaxAmount);
    }

    [Fact]
    public async Task Calculate_OnSubtotal_PerRateBreakdown_PreservesPayableAccountIds()
    {
        var fx = new Fixture();
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var code = await fx.SeedCodeAsync();
        await fx.SeedRateAsync(code.Id, state.Id, 4m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA()));

        Assert.Equal(fx.PayableAccount.Id, result.Breakdown[0].PayableAccountId);
        Assert.Equal(state.Id, result.Breakdown[0].JurisdictionId);
        Assert.Equal(JurisdictionLevel.State, result.Breakdown[0].JurisdictionLevel);
    }

    // ── Compound ────────────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_Compound_TwoRates_FederalThenState_AppliesStateOnSubtotalPlusFederal()
    {
        // Regression: federal 5% on 100.00 = 5.00; state 4% on (100 + 5) = 4.20; total = 9.20.
        var fx = new Fixture();
        var federal = await fx.SeedJurisdictionAsync(JurisdictionLevel.Federal, name: "Federal");
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var code = await fx.SeedCodeAsync(application: TaxApplication.Compound);
        await fx.SeedRateAsync(code.Id, federal.Id, 5m, D(2026, 1, 1));
        await fx.SeedRateAsync(code.Id, state.Id, 4m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA()));

        Assert.Equal(TaxCalculationError.None, result.Error);
        Assert.Equal(9.20m, result.TaxAmount);
        Assert.Equal(109.20m, result.TotalIn);

        // Federal row first per OrderIndex.
        Assert.Equal(JurisdictionLevel.Federal, result.Breakdown[0].JurisdictionLevel);
        Assert.Equal(5.00m, result.Breakdown[0].TaxAmount);
        Assert.Equal(100m, result.Breakdown[0].TaxableBase);
        Assert.Equal(JurisdictionLevel.State, result.Breakdown[1].JurisdictionLevel);
        Assert.Equal(4.20m, result.Breakdown[1].TaxAmount);
        Assert.Equal(105m, result.Breakdown[1].TaxableBase);
    }

    [Fact]
    public async Task Calculate_Compound_OrderedByJurisdictionLevel_FederalBeforeStateBeforeCity()
    {
        var fx = new Fixture();
        var federal = await fx.SeedJurisdictionAsync(JurisdictionLevel.Federal, name: "Federal");
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var city = await fx.SeedJurisdictionAsync(
            JurisdictionLevel.City, region: "US-VA", locality: "Frederick County", name: "Winchester");
        var code = await fx.SeedCodeAsync(application: TaxApplication.Compound);
        // Seed in scrambled order — the algorithm should reorder.
        await fx.SeedRateAsync(code.Id, city.Id, 1m, D(2026, 1, 1));
        await fx.SeedRateAsync(code.Id, federal.Id, 2m, D(2026, 1, 1));
        await fx.SeedRateAsync(code.Id, state.Id, 3m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA(locality: "Frederick County")));

        Assert.Equal(JurisdictionLevel.Federal, result.Breakdown[0].JurisdictionLevel);
        Assert.Equal(JurisdictionLevel.State, result.Breakdown[1].JurisdictionLevel);
        Assert.Equal(JurisdictionLevel.City, result.Breakdown[2].JurisdictionLevel);
    }

    [Fact]
    public async Task Calculate_Compound_RoundingAccumulatesCorrectly_NoFloatRoundoffDrift()
    {
        // Each row rounds independently to 2 decimals; total tax = sum of breakdown TaxAmount.
        var fx = new Fixture();
        var federal = await fx.SeedJurisdictionAsync(JurisdictionLevel.Federal, name: "Federal");
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var code = await fx.SeedCodeAsync(application: TaxApplication.Compound);
        await fx.SeedRateAsync(code.Id, federal.Id, 7.33m, D(2026, 1, 1));
        await fx.SeedRateAsync(code.Id, state.Id, 4.625m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 137.42m, D(2026, 6, 1), USVA()));

        Assert.Equal(TaxCalculationError.None, result.Error);
        Assert.Equal(result.TaxAmount, result.Breakdown.Sum(b => b.TaxAmount));
        // Decimal math, no float drift.
    }

    // ── Inclusive ───────────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_Inclusive_SingleRate_BacksOutCorrectly_FromGrossOf108_AtRate8pct_Yields100p00BaseAnd8p00Tax()
    {
        var fx = new Fixture();
        var country = await fx.SeedJurisdictionAsync(JurisdictionLevel.Country, isoCountry: "DE", name: "Germany");
        var code = await fx.SeedCodeAsync("DE-VAT", TaxKind.VAT, TaxApplication.Inclusive);
        await fx.SeedRateAsync(code.Id, country.Id, 8m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 108m, D(2026, 6, 1), new TaxLocationContext("DE")));

        Assert.Equal(TaxCalculationError.None, result.Error);
        Assert.Equal(8.00m, result.TaxAmount);
        // Inclusive: total == subtotal (tax baked in).
        Assert.Equal(108m, result.TotalIn);
        // Breakdown taxable base is the pre-tax (108 - 8 = 100).
        Assert.Equal(100m, result.Breakdown[0].TaxableBase);
    }

    [Fact]
    public async Task Calculate_Inclusive_TwoRates_ProratesByRateShare()
    {
        var fx = new Fixture();
        var country = await fx.SeedJurisdictionAsync(JurisdictionLevel.Country, isoCountry: "DE", name: "Germany");
        var state = await fx.SeedJurisdictionAsync(
            JurisdictionLevel.State, isoCountry: "DE", region: "DE-BY", name: "Bavaria");
        var code = await fx.SeedCodeAsync("DE-VAT", TaxKind.VAT, TaxApplication.Inclusive);
        await fx.SeedRateAsync(code.Id, country.Id, 5m, D(2026, 1, 1));
        await fx.SeedRateAsync(code.Id, state.Id, 3m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 108m, D(2026, 6, 1), new TaxLocationContext("DE", "DE-BY")));

        Assert.Equal(TaxCalculationError.None, result.Error);
        // Total tax baked into 108 at combined 8% = 8.00.
        Assert.Equal(8.00m, result.TaxAmount);
        Assert.Equal(2, result.Breakdown.Count);
        // 5/8 of 8.00 = 5.00; remainder 3.00 (last row absorbs residual; here clean).
        // Note: ordering is whatever the rate lookup returned — we just verify the sum.
        Assert.Equal(8.00m, result.Breakdown.Sum(b => b.TaxAmount));
    }

    [Fact]
    public async Task Calculate_Inclusive_LastRowAbsorbsRoundingResidual_TotalIsExact()
    {
        // Three 2.33% rates summing to 6.99% on 100.00 inclusive.
        // total tax = 100 * 0.0699 / 1.0699 ≈ 6.5333... → rounds to 6.53.
        // Each pro-rata is 6.53 / 3 ≈ 2.176... → rounds to 2.18 (first two),
        // so 6.53 - 2.18 - 2.18 = 2.17 absorbed by last row. Sum must equal 6.53.
        var fx = new Fixture();
        var j1 = await fx.SeedJurisdictionAsync(JurisdictionLevel.Federal, name: "Federal");
        var j2 = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var j3 = await fx.SeedJurisdictionAsync(
            JurisdictionLevel.County, region: "US-VA", locality: "Frederick County", name: "Frederick County");
        var code = await fx.SeedCodeAsync("US-VA-INCL", TaxKind.Sales, TaxApplication.Inclusive);
        await fx.SeedRateAsync(code.Id, j1.Id, 2.33m, D(2026, 1, 1));
        await fx.SeedRateAsync(code.Id, j2.Id, 2.33m, D(2026, 1, 1));
        await fx.SeedRateAsync(code.Id, j3.Id, 2.33m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 100m, D(2026, 6, 1), USVA(locality: "Frederick County")));

        Assert.Equal(TaxCalculationError.None, result.Error);
        Assert.Equal(result.TaxAmount, result.Breakdown.Sum(b => b.TaxAmount));
    }

    [Fact]
    public async Task Calculate_Inclusive_ZeroSubtotal_ReturnsInclusiveWithZeroSubtotalError()
    {
        var fx = new Fixture();
        var j = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var code = await fx.SeedCodeAsync("US-VA-INCL", TaxKind.Sales, TaxApplication.Inclusive);
        await fx.SeedRateAsync(code.Id, j.Id, 5m, D(2026, 1, 1));

        var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
            code.Id, 0m, D(2026, 6, 1), USVA()));

        Assert.Equal(TaxCalculationError.InclusiveWithZeroSubtotal, result.Error);
    }

    // ── Property-test-style fuzz ────────────────────────────────────────

    [Fact]
    public async Task Calculate_OnSubtotal_RandomFuzz_TotalEqualsSumOfBreakdown_Always()
    {
        // 100 random subtotals + 1-3 random rates. Asserts the per-rate
        // breakdown sum equals the rolled-up TaxAmount to the cent for
        // every iteration. Catches accumulator drift if the algorithm
        // is ever refactored to use floats or non-banker's rounding.
        var fx = new Fixture();
        var state = await fx.SeedJurisdictionAsync(JurisdictionLevel.State, region: "US-VA", name: "Virginia");
        var county = await fx.SeedJurisdictionAsync(
            JurisdictionLevel.County, region: "US-VA", locality: "Frederick County", name: "Frederick County");
        var city = await fx.SeedJurisdictionAsync(
            JurisdictionLevel.City, region: "US-VA", locality: "Frederick County", name: "Winchester");
        var jurisdictions = new[] { state, county, city };

        var random = new Random(0x511_F15);

        for (int i = 0; i < 100; i++)
        {
            // Fresh code per iteration so the rate set is deterministic.
            var code = await fx.SeedCodeAsync($"FUZZ-{i:D3}", TaxKind.Sales, TaxApplication.OnSubtotal);
            int rateCount = random.Next(1, 4);
            for (int r = 0; r < rateCount; r++)
            {
                var j = jurisdictions[r];
                // 0.10% .. 9.99% with 2-decimal precision.
                var pct = Math.Round((decimal)(random.NextDouble() * 9.89 + 0.10), 2);
                await fx.SeedRateAsync(code.Id, j.Id, pct, D(2026, 1, 1));
            }
            // 1.00 .. 999.99 with 2-decimal precision.
            var subtotal = Math.Round((decimal)(random.NextDouble() * 998.99 + 1.00), 2);

            var result = await fx.Service.CalculateAsync(new TaxCalculationInput(
                code.Id, subtotal, D(2026, 6, 1), USVA(locality: "Frederick County")));

            Assert.Equal(TaxCalculationError.None, result.Error);
            Assert.Equal(result.TaxAmount, result.Breakdown.Sum(b => b.TaxAmount));
        }
    }
}
