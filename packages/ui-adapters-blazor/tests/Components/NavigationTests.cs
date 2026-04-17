using Sunfish.Components.Blazor.Components.Navigation;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

public class NavigationTests
{
    [Fact]
    public void SunfishNavBar_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishNavBar);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Components.Blazor.Components.Navigation", type.Namespace);
    }

    [Fact]
    public void SunfishTreeView_TypeExists()
    {
        var type = typeof(SunfishTreeView);
        Assert.Equal("Sunfish.Components.Blazor.Components.Navigation", type.Namespace);
    }
}
