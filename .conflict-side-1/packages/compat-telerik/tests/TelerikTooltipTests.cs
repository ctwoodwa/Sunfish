using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikTooltipTests
{
    [Fact]
    public void TelerikTooltip_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikTooltip);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
