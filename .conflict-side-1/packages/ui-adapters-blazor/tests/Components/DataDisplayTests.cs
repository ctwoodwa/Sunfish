using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

public class DataDisplayTests
{
    [Fact]
    public void SunfishAvatar_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishAvatar);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.DataDisplay", type.Namespace);
    }

    [Fact]
    public void SunfishCard_TypeExists()
    {
        var type = typeof(SunfishCard);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.DataDisplay", type.Namespace);
    }

    [Fact]
    public void SunfishCalendar_TypeExists()
    {
        var type = typeof(SunfishCalendar);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.DataDisplay", type.Namespace);
    }
}
