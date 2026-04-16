using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Components.Blazor.Base;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Components.Blazor.Tests;

public class SunfishComponentBaseTests : BunitContext
{
    public SunfishComponentBaseTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
    }

    [Fact]
    public void CombineClasses_AppendsUserClassToBaseClass()
    {
        var cut = Render<TestSunfishComponent>(p => p
            .Add(x => x.Class, "user-class")
            .Add(x => x.BaseClass, "base-class"));

        Assert.Contains("base-class user-class", cut.Markup);
    }

    [Fact]
    public void CombineClasses_WhenNoUserClass_ReturnsBaseClassOnly()
    {
        var cut = Render<TestSunfishComponent>(p => p
            .Add(x => x.BaseClass, "only-base"));

        Assert.Contains("only-base", cut.Markup);
    }

    [Fact]
    public void AdditionalAttributes_ArePropagatedToElement()
    {
        var cut = Render<TestSunfishComponent>(p => p
            .AddUnmatched("data-testid", "my-comp"));

        Assert.Contains("data-testid=\"my-comp\"", cut.Markup);
    }
}

public class TestSunfishComponent : SunfishComponentBase
{
    [Microsoft.AspNetCore.Components.Parameter]
    public string BaseClass { get; set; } = string.Empty;

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", CombineClasses(BaseClass));
        builder.AddMultipleAttributes(2, AdditionalAttributes);
        builder.CloseElement();
    }
}
