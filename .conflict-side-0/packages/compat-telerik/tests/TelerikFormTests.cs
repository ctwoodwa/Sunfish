using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikFormTests
{
    [Fact]
    public void TelerikForm_TypeIsPublicGenericAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikForm<>);
        Assert.True(type.IsPublic);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Single(type.GetGenericArguments());
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
