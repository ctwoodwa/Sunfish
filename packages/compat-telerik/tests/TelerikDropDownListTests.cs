using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikDropDownListTests
{
    [Fact]
    public void TelerikDropDownList_TypeIsPublicGenericAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikDropDownList<,>);
        Assert.True(type.IsPublic);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Equal(2, type.GetGenericArguments().Length);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
