using Sunfish.Blocks.FinancialTax.Models;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

public class TaxAccountSelectorTests
{
    private static GLAccountReference Account(string code, params string[] tags) =>
        new GLAccountReference(code, tags);

    [Fact]
    public void Matches_ExactCode_Hits()
    {
        var sel = new TaxAccountSelector(AccountCode: "5100");
        Assert.True(sel.Matches(Account("5100")));
        Assert.False(sel.Matches(Account("5101")));
    }

    [Fact]
    public void Matches_Prefix_Hits()
    {
        var sel = new TaxAccountSelector(AccountCodePrefix: "41");
        Assert.True(sel.Matches(Account("4100")));
        Assert.True(sel.Matches(Account("4150")));
        Assert.False(sel.Matches(Account("5100")));
    }

    [Fact]
    public void Matches_Tag_Hits()
    {
        var sel = new TaxAccountSelector(AccountTag: "auto-travel");
        Assert.True(sel.Matches(Account("6500", "auto-travel", "other")));
        Assert.False(sel.Matches(Account("6500", "other-tag")));
    }

    [Fact]
    public void Matches_Invert_TogglesResult()
    {
        var sel = new TaxAccountSelector(AccountCode: "5100", Invert: true);
        Assert.False(sel.Matches(Account("5100")));
        Assert.True(sel.Matches(Account("5200")));
    }

    [Fact]
    public void Matches_NoSelector_DoesNotMatchAnything()
    {
        var sel = new TaxAccountSelector();
        Assert.False(sel.Matches(Account("5100")));
        Assert.False(sel.Matches(Account("5100", "any-tag")));
    }
}
