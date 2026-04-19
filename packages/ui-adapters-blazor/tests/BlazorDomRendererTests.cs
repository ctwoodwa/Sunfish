using Microsoft.AspNetCore.Components;
using Sunfish.Components.Blazor.Renderers;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Components.Blazor.Tests;

public class BlazorDomRendererTests
{
    [Fact]
    public void BlazorDomRenderer_PlatformIsBlazorDom()
    {
        var renderer = new BlazorDomRenderer();

        Assert.Equal("blazor-dom", renderer.Platform);
    }

    [Fact]
    public void BlazorDomRenderer_Render_ProducesRenderFragment()
    {
        var renderer = new BlazorDomRenderer();
        var descriptor = new SunfishWidgetDescriptor(
            "div",
            new Dictionary<string, object?> { ["class"] = "sf-root" },
            new[]
            {
                new SunfishWidgetDescriptor(
                    "span",
                    new Dictionary<string, object?>(),
                    Array.Empty<SunfishWidgetDescriptor>()),
            });

        var payload = renderer.Render(descriptor);

        Assert.NotNull(payload);
        var fragment = Assert.IsAssignableFrom<RenderFragment>(payload);
        Assert.NotNull(fragment);
    }
}
