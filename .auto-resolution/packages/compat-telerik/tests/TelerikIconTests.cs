using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikIconTests
{
    [Fact]
    public void TelerikIcon_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikIcon);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
