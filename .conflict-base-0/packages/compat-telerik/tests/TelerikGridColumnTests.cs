using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikGridColumnTests
{
    [Fact]
    public void TelerikGridColumn_TypeIsPublicGenericAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikGridColumn<>);
        Assert.True(type.IsPublic);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Single(type.GetGenericArguments());
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }

    [Fact]
    public void GridColumns_ContainerTypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.GridColumns);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
