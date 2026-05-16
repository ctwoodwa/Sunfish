using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 2 coverage for the in-memory <see cref="ITaxRateLookup"/>:
/// non-overlap validation, payable-account type/subtype validation,
/// active-rate filtering, history ordering, and atomic supersede.
/// </summary>
public class InMemoryTaxRateLookupTests
{
    private static DateOnly D(int y, int m, int d) => new DateOnly(y, m, d);

    private static GLAccount TaxesPayableAccount(string code = "2200") =>
        GLAccount.Create(
            id: GLAccountId.NewId(),
            chartId: ChartOfAccountsId.NewId(),
            code: code,
            name: "State sales tax payable",
            type: GLAccountType.Liability,
            subtype: AccountSubtype.TaxesPayable,
            currency: "USD");

    private static GLAccount LiabilityNotTaxesAccount() =>
        GLAccount.Create(
            id: GLAccountId.NewId(),
            chartId: ChartOfAccountsId.NewId(),
            code: "2000",
            name: "Accounts payable",
            type: GLAccountType.Liability,
            subtype: AccountSubtype.AccountsPayable,
            currency: "USD");

    private static GLAccount AssetAccount() =>
        GLAccount.Create(
            id: GLAccountId.NewId(),
            chartId: ChartOfAccountsId.NewId(),
            code: "1100",
            name: "Bank — operating",
            type: GLAccountType.Asset,
            subtype: AccountSubtype.BankAccount,
            currency: "USD");

    [Fact]
    public async Task Upsert_NewRate_NoConflict_Inserts()
    {
        var payable = TaxesPayableAccount();
        var accounts = new InMemoryAccountResolver(new[] { payable });
        var lookup = new InMemoryTaxRateLookup(accounts);

        var candidate = TaxRate.Create(
            taxCodeId: TaxCodeId.NewId(),
            jurisdictionId: TaxJurisdictionId.NewId(),
            ratePercent: 5.3m,
            effectiveDate: D(2026, 1, 1),
            payableAccountId: payable.Id);
        var result = await lookup.UpsertAsync(candidate);

        Assert.Equal(TaxRateValidationError.None, result.Error);
        Assert.NotNull(result.Rate);
        Assert.Equal(candidate.Id, result.Rate!.Id);
    }

    [Fact]
    public async Task Upsert_OverlappingRange_ReturnsDateRangeOverlap()
    {
        var payable = TaxesPayableAccount();
        var accounts = new InMemoryAccountResolver(new[] { payable });
        var lookup = new InMemoryTaxRateLookup(accounts);
        var codeId = TaxCodeId.NewId();
        var jurisdictionId = TaxJurisdictionId.NewId();

        var existing = TaxRate.Create(codeId, jurisdictionId, 5m, D(2026, 1, 1), payable.Id, expiryDate: D(2026, 12, 31));
        await lookup.UpsertAsync(existing);

        // Overlapping candidate — effective in the middle of existing's window.
        var overlap = TaxRate.Create(codeId, jurisdictionId, 6m, D(2026, 6, 1), payable.Id);
        var result = await lookup.UpsertAsync(overlap);

        Assert.Equal(TaxRateValidationError.DateRangeOverlap, result.Error);
        Assert.Null(result.Rate);
    }

    [Fact]
    public async Task Upsert_PayableAccountIsAsset_ReturnsPayableAccountWrongType()
    {
        var bad = AssetAccount();
        var accounts = new InMemoryAccountResolver(new[] { bad });
        var lookup = new InMemoryTaxRateLookup(accounts);

        var candidate = TaxRate.Create(TaxCodeId.NewId(), TaxJurisdictionId.NewId(), 5m, D(2026, 1, 1), bad.Id);
        var result = await lookup.UpsertAsync(candidate);

        Assert.Equal(TaxRateValidationError.PayableAccountWrongType, result.Error);
    }

    [Fact]
    public async Task Upsert_PayableAccountIsLiabilityButNotTaxesPayable_ReturnsPayableAccountWrongSubtype()
    {
        var bad = LiabilityNotTaxesAccount();
        var accounts = new InMemoryAccountResolver(new[] { bad });
        var lookup = new InMemoryTaxRateLookup(accounts);

        var candidate = TaxRate.Create(TaxCodeId.NewId(), TaxJurisdictionId.NewId(), 5m, D(2026, 1, 1), bad.Id);
        var result = await lookup.UpsertAsync(candidate);

        Assert.Equal(TaxRateValidationError.PayableAccountWrongSubtype, result.Error);
    }

    [Fact]
    public async Task Upsert_PayableAccountNotFound_ReturnsPayableAccountNotFound()
    {
        var accounts = new InMemoryAccountResolver();
        var lookup = new InMemoryTaxRateLookup(accounts);

        var candidate = TaxRate.Create(TaxCodeId.NewId(), TaxJurisdictionId.NewId(), 5m, D(2026, 1, 1), GLAccountId.NewId());
        var result = await lookup.UpsertAsync(candidate);

        Assert.Equal(TaxRateValidationError.PayableAccountNotFound, result.Error);
    }

    [Fact]
    public async Task GetActiveRates_ReturnsOnlyRatesActiveOnDate()
    {
        var payable = TaxesPayableAccount();
        var lookup = new InMemoryTaxRateLookup(new InMemoryAccountResolver(new[] { payable }));
        var codeId = TaxCodeId.NewId();
        var j = TaxJurisdictionId.NewId();

        await lookup.UpsertAsync(TaxRate.Create(codeId, j, 4m, D(2025, 1, 1), payable.Id, expiryDate: D(2025, 12, 31)));
        await lookup.UpsertAsync(TaxRate.Create(codeId, j, 5m, D(2026, 1, 1), payable.Id, expiryDate: D(2026, 12, 31)));
        await lookup.UpsertAsync(TaxRate.Create(codeId, j, 6m, D(2027, 1, 1), payable.Id));

        var active = await lookup.GetActiveRatesAsync(codeId, D(2026, 6, 15), new[] { j });

        Assert.Single(active);
        Assert.Equal(5m, active[0].RatePercent);
    }

    [Fact]
    public async Task GetActiveRates_FiltersByJurisdictionSet()
    {
        var payable = TaxesPayableAccount();
        var lookup = new InMemoryTaxRateLookup(new InMemoryAccountResolver(new[] { payable }));
        var codeId = TaxCodeId.NewId();
        var j1 = TaxJurisdictionId.NewId();
        var j2 = TaxJurisdictionId.NewId();
        var j3 = TaxJurisdictionId.NewId();

        await lookup.UpsertAsync(TaxRate.Create(codeId, j1, 4m, D(2026, 1, 1), payable.Id));
        await lookup.UpsertAsync(TaxRate.Create(codeId, j2, 5m, D(2026, 1, 1), payable.Id));
        await lookup.UpsertAsync(TaxRate.Create(codeId, j3, 6m, D(2026, 1, 1), payable.Id));

        var hits = await lookup.GetActiveRatesAsync(codeId, D(2026, 6, 15), new[] { j1, j3 });

        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, r => r.JurisdictionId == j1);
        Assert.Contains(hits, r => r.JurisdictionId == j3);
        Assert.DoesNotContain(hits, r => r.JurisdictionId == j2);
    }

    [Fact]
    public async Task GetHistory_ReturnsOldestFirst()
    {
        var payable = TaxesPayableAccount();
        var lookup = new InMemoryTaxRateLookup(new InMemoryAccountResolver(new[] { payable }));
        var codeId = TaxCodeId.NewId();
        var j = TaxJurisdictionId.NewId();

        await lookup.UpsertAsync(TaxRate.Create(codeId, j, 5m, D(2026, 1, 1), payable.Id, expiryDate: D(2026, 12, 31)));
        await lookup.UpsertAsync(TaxRate.Create(codeId, j, 4m, D(2025, 1, 1), payable.Id, expiryDate: D(2025, 12, 31)));
        await lookup.UpsertAsync(TaxRate.Create(codeId, j, 6m, D(2027, 1, 1), payable.Id));

        var history = await lookup.GetHistoryAsync(codeId, j);

        Assert.Equal(3, history.Count);
        Assert.Equal(D(2025, 1, 1), history[0].EffectiveDate);
        Assert.Equal(D(2026, 1, 1), history[1].EffectiveDate);
        Assert.Equal(D(2027, 1, 1), history[2].EffectiveDate);
    }

    [Fact]
    public async Task Supersede_HappyPath_ExpiresOldRateAndInsertsNew_ReturnsBothInResult()
    {
        var payable = TaxesPayableAccount();
        var lookup = new InMemoryTaxRateLookup(new InMemoryAccountResolver(new[] { payable }));
        var codeId = TaxCodeId.NewId();
        var j = TaxJurisdictionId.NewId();

        var initial = TaxRate.Create(codeId, j, 5m, D(2025, 1, 1), payable.Id);
        await lookup.UpsertAsync(initial);

        var result = await lookup.SupersedeAsync(codeId, j, 6m, D(2026, 7, 1), payable.Id);

        Assert.Equal(TaxRateValidationError.None, result.Error);
        Assert.NotNull(result.OldRate);
        Assert.NotNull(result.NewRate);
        Assert.Equal(D(2026, 6, 30), result.OldRate!.ExpiryDate);
        Assert.Equal(6m, result.NewRate!.RatePercent);

        // History reflects both rows in order.
        var history = await lookup.GetHistoryAsync(codeId, j);
        Assert.Equal(2, history.Count);
        Assert.Equal(5m, history[0].RatePercent);
        Assert.Equal(D(2026, 6, 30), history[0].ExpiryDate);
        Assert.Equal(6m, history[1].RatePercent);
        Assert.Null(history[1].ExpiryDate);
    }

    [Fact]
    public async Task Supersede_NoActiveRate_ReturnsNoActiveRateToSupersede()
    {
        var payable = TaxesPayableAccount();
        var lookup = new InMemoryTaxRateLookup(new InMemoryAccountResolver(new[] { payable }));

        var result = await lookup.SupersedeAsync(
            TaxCodeId.NewId(),
            TaxJurisdictionId.NewId(),
            5m,
            D(2026, 1, 1),
            payable.Id);

        Assert.Equal(TaxRateValidationError.NoActiveRateToSupersede, result.Error);
    }

    [Fact]
    public async Task Supersede_BadPayableAccount_NeitherChangeIsPersisted()
    {
        var realPayable = TaxesPayableAccount();
        var nonExistent = GLAccountId.NewId();
        var lookup = new InMemoryTaxRateLookup(new InMemoryAccountResolver(new[] { realPayable }));
        var codeId = TaxCodeId.NewId();
        var j = TaxJurisdictionId.NewId();

        await lookup.UpsertAsync(TaxRate.Create(codeId, j, 5m, D(2025, 1, 1), realPayable.Id));

        var result = await lookup.SupersedeAsync(codeId, j, 6m, D(2026, 7, 1), nonExistent);

        Assert.Equal(TaxRateValidationError.PayableAccountNotFound, result.Error);
        // Verify the old rate is still open-ended (un-expired).
        var history = await lookup.GetHistoryAsync(codeId, j);
        Assert.Single(history);
        Assert.Null(history[0].ExpiryDate);
    }
}
