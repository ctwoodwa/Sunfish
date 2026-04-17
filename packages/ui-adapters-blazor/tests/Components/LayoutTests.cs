using Sunfish.Components.Blazor.Components.Layout;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

public class LayoutTests
{
    [Fact]
    public void SunfishAccordion_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishAccordion);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Components.Blazor.Components.Layout", type.Namespace);
    }

    [Fact]
    public void SunfishAppBar_TypeExists()
    {
        var type = typeof(SunfishAppBar);
        Assert.Equal("Sunfish.Components.Blazor.Components.Layout", type.Namespace);
    }
}
