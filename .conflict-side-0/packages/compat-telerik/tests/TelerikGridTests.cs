using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikGridTests
{
    [Fact]
    public void TelerikGrid_TypeIsPublicGenericAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikGrid<>);
        Assert.True(type.IsPublic);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Single(type.GetGenericArguments());
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
