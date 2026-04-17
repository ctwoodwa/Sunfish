using Sunfish.Components.Blazor.Components.DataDisplay;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

public class DataDisplayTests
{
    [Fact]
    public void SunfishAvatar_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishAvatar);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Components.Blazor.Components.DataDisplay", type.Namespace);
    }

    [Fact]
    public void SunfishCard_TypeExists()
    {
        var type = typeof(SunfishCard);
        Assert.Equal("Sunfish.Components.Blazor.Components.DataDisplay", type.Namespace);
    }

    [Fact]
    public void SunfishCalendar_TypeExists()
    {
        var type = typeof(SunfishCalendar);
        Assert.Equal("Sunfish.Components.Blazor.Components.DataDisplay", type.Namespace);
    }
}
