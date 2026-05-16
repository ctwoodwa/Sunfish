using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 2 coverage for the <see cref="TaxRate"/> record per Stage 02
/// §3.13. Constructor-level validation + <see cref="TaxRate.IsActiveOn"/>
/// semantics only; service-layer (overlap, payable-account) tests live
/// in <see cref="InMemoryTaxRateLookupTests"/>.
/// </summary>
public class TaxRateTests
{
    private static TaxCodeId NewCode() => TaxCodeId.NewId();
    private static TaxJurisdictionId NewJurisdiction() => TaxJurisdictionId.NewId();
    private static GLAccountId NewAccount() => GLAccountId.NewId();
    private static DateOnly D(int y, int m, int d) => new DateOnly(y, m, d);

    [Fact]
    public void Create_RatePercentInRange_Succeeds()
    {
        var rate = TaxRate.Create(NewCode(), NewJurisdiction(), 5.3m, D(2026, 1, 1), NewAccount());

        Assert.Equal(5.3m, rate.RatePercent);
        Assert.Null(rate.ExpiryDate);
    }

    [Fact]
    public void Create_RatePercentBelowZero_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            TaxRate.Create(NewCode(), NewJurisdiction(), -0.01m, D(2026, 1, 1), NewAccount()));
        Assert.Equal("ratePercent", ex.ParamName);
    }

    [Fact]
    public void Create_RatePercentAbove100_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            TaxRate.Create(NewCode(), NewJurisdiction(), 100.01m, D(2026, 1, 1), NewAccount()));
        Assert.Equal("ratePercent", ex.ParamName);
    }

    [Fact]
    public void Create_ExpiryBeforeEffective_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TaxRate.Create(
                NewCode(), NewJurisdiction(), 5m,
                effectiveDate: D(2026, 6, 1),
                payableAccountId: NewAccount(),
                expiryDate: D(2026, 5, 31)));
        Assert.Equal("expiryDate", ex.ParamName);
    }

    [Fact]
    public void Create_ExpiryEqualEffective_Succeeds()
    {
        // Single-day rate.
        var rate = TaxRate.Create(
            NewCode(), NewJurisdiction(), 5m,
            effectiveDate: D(2026, 6, 1),
            payableAccountId: NewAccount(),
            expiryDate: D(2026, 6, 1));
        Assert.Equal(D(2026, 6, 1), rate.ExpiryDate);
    }

    [Fact]
    public void Create_ExpiryNull_SignifiesOpenEnded()
    {
        var rate = TaxRate.Create(NewCode(), NewJurisdiction(), 5m, D(2026, 1, 1), NewAccount());
        Assert.Null(rate.ExpiryDate);
    }

    [Fact]
    public void IsActiveOn_DateBeforeEffective_ReturnsFalse()
    {
        var rate = TaxRate.Create(NewCode(), NewJurisdiction(), 5m, D(2026, 6, 1), NewAccount());
        Assert.False(rate.IsActiveOn(D(2026, 5, 31)));
    }

    [Fact]
    public void IsActiveOn_DateAfterExpiry_ReturnsFalse()
    {
        var rate = TaxRate.Create(
            NewCode(), NewJurisdiction(), 5m,
            effectiveDate: D(2026, 1, 1),
            payableAccountId: NewAccount(),
            expiryDate: D(2026, 12, 31));
        Assert.False(rate.IsActiveOn(D(2027, 1, 1)));
    }

    [Fact]
    public void IsActiveOn_DateInRange_ReturnsTrue()
    {
        var rate = TaxRate.Create(
            NewCode(), NewJurisdiction(), 5m,
            effectiveDate: D(2026, 1, 1),
            payableAccountId: NewAccount(),
            expiryDate: D(2026, 12, 31));
        Assert.True(rate.IsActiveOn(D(2026, 6, 15)));
    }

    [Fact]
    public void IsActiveOn_AfterTombstoned_ReturnsFalse()
    {
        var rate = TaxRate.Create(NewCode(), NewJurisdiction(), 5m, D(2026, 1, 1), NewAccount())
            with { DeletedAtUtc = Instant.Now };
        Assert.False(rate.IsActiveOn(D(2026, 6, 15)));
    }
}
