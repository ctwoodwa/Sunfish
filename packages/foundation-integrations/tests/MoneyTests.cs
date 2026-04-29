using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Integrations.Signatures;
using Xunit;

namespace Sunfish.Foundation.Integrations.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Money_Construction_PreservesAmountAndCurrency()
    {
        var m = new Money(100m, CurrencyCode.USD);
        Assert.Equal(100m, m.Amount);
        Assert.Equal("USD", m.Currency.Iso4217);
    }

    [Fact]
    public void Money_UsdShorthand_BindsToUSD()
    {
        var m = Money.Usd(42.50m);
        Assert.Equal(CurrencyCode.USD, m.Currency);
        Assert.Equal(42.50m, m.Amount);
    }

    [Fact]
    public void Money_Equality_StructuralByValue()
    {
        Assert.Equal(Money.Usd(10m), new Money(10m, CurrencyCode.USD));
        Assert.NotEqual(Money.Usd(10m), Money.Usd(11m));
    }

    [Fact]
    public void SignatureEventRef_PreservesId()
    {
        var id = Guid.NewGuid();
        var r = new SignatureEventRef(id);
        Assert.Equal(id, r.SignatureEventId);
    }
}
