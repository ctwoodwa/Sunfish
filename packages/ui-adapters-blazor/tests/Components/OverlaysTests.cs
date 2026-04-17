using Sunfish.Components.Blazor.Components.Overlays;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

public class OverlaysTests
{
    [Fact]
    public void SunfishWindow_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishWindow);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Components.Blazor.Components.Overlays", type.Namespace);
    }

    [Fact]
    public void SunfishPopup_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishPopup);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Components.Blazor.Components.Overlays", type.Namespace);
    }
}
