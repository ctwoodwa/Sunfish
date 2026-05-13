using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.UICore.Primitives;
using Xunit;

namespace Sunfish.Blocks.ShipsOffice.Tests;

public sealed class ShipsOfficeSearchBarTests : BunitContext
{
    private IRenderedComponent<ShipsOfficeSearchBar> RenderBar()
    {
        Services.AddSingleton(Substitute.For<ISearchAsYouType<ShipsOfficeDocumentView>>());
        return Render<ShipsOfficeSearchBar>(p => p
            .Add(c => c.OnSearch, EventCallback.Factory.Create<string>(this, _ => { })));
    }

    [Fact]
    public void Combobox_has_role_aria_expanded_aria_activedescendant()
    {
        var cut = RenderBar();

        var combobox = cut.Find("input[role='combobox']");
        Assert.NotNull(combobox);
        Assert.NotNull(combobox.GetAttribute("aria-expanded"));
        Assert.NotNull(combobox.GetAttribute("aria-haspopup"));
        Assert.NotNull(combobox.GetAttribute("aria-owns"));
    }

    [Fact]
    public void Result_count_announced_via_aria_live_polite()
    {
        var cut = RenderBar();

        var liveRegion = cut.Find("[aria-live='polite'][aria-atomic='true']");
        Assert.NotNull(liveRegion);
        Assert.Contains("visually-hidden", liveRegion.ClassName ?? string.Empty);
    }
}
