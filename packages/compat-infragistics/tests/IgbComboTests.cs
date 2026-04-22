using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbComboTests
{
    [Fact]
    public void IgbCombo_IsGenericOverTItemAndTValue()
    {
        var t = typeof(Sunfish.Compat.Infragistics.IgbCombo<,>);
        Assert.True(t.IsGenericTypeDefinition);
        Assert.Equal(2, t.GetGenericArguments().Length);
        Assert.Equal("Sunfish.Compat.Infragistics", t.Namespace);
    }
}
