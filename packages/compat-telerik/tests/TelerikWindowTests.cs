using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikWindowTests
{
    [Fact]
    public void TelerikWindow_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikWindow);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
