using Sunfish.UIAdapters.Blazor.Components.Navigation;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

public class NavigationTests
{
    [Fact]
    public void SunfishNavBar_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishNavBar);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Navigation", type.Namespace);
    }

    [Fact]
    public void SunfishTreeView_TypeExists()
    {
        var type = typeof(SunfishTreeView);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Navigation", type.Namespace);
    }
}
