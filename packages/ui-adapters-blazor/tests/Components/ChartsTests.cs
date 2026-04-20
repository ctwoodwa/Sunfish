using Sunfish.UIAdapters.Blazor.Components.Charts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

public class ChartsTests
{
    [Fact]
    public void SunfishChart_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishChart);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Charts", type.Namespace);
    }

    [Fact]
    public void SunfishStockChart_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishStockChart);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Charts", type.Namespace);
    }
}
