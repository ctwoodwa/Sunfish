using System;
using System.Collections.Generic;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;
using Sunfish.UIAdapters.Blazor.Internal.Interop;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components.LocalFirst;

public class SunfishConflictListTests : BunitContext
{
    public SunfishConflictListTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<ISunfishJsModuleLoader>(
            _ => Substitute.For<ISunfishJsModuleLoader>());
    }

    private static ConflictItem Make(string id, string kind = "merge") =>
        new(id, $"rec-{id}", kind, DateTimeOffset.UtcNow, $"description for {id}");

    [Fact]
    public void EmptyList_RendersEmptyMessage()
    {
        var cut = Render<SunfishConflictList>(p => p
            .Add(x => x.Conflicts, new List<ConflictItem>()));

        Assert.Contains("No pending conflicts", cut.Markup);
    }

    [Fact]
    public void NullList_RendersEmptyMessage()
    {
        var cut = Render<SunfishConflictList>(p => p
            .Add(x => x.Conflicts, (IReadOnlyList<ConflictItem>?)null));

        Assert.Contains("No pending conflicts", cut.Markup);
    }

    [Fact]
    public void MultipleConflicts_RendersEachRow()
    {
        var conflicts = new List<ConflictItem> { Make("a"), Make("b"), Make("c") };
        var cut = Render<SunfishConflictList>(p => p
            .Add(x => x.Conflicts, conflicts));

        Assert.Equal(3, cut.FindAll(".sf-conflict-list__row").Count);
        Assert.Contains("description for a", cut.Markup);
        Assert.Contains("description for b", cut.Markup);
        Assert.Contains("description for c", cut.Markup);
    }

    [Fact]
    public void ResolveButton_FiresOnResolve_WithConflictItem()
    {
        var conflicts = new List<ConflictItem> { Make("x") };
        ConflictItem? received = null;
        var cut = Render<SunfishConflictList>(p => p
            .Add(x => x.Conflicts, conflicts)
            .Add(x => x.OnResolve,
                EventCallback.Factory.Create<ConflictItem>(this, c => received = c)));

        cut.Find("[data-action='resolve']").Click();

        Assert.NotNull(received);
        Assert.Equal("x", received!.Id);
    }

    [Fact]
    public void DismissButton_FiresOnDismiss_WithConflictItem()
    {
        var conflicts = new List<ConflictItem> { Make("y") };
        ConflictItem? received = null;
        var cut = Render<SunfishConflictList>(p => p
            .Add(x => x.Conflicts, conflicts)
            .Add(x => x.OnDismiss,
                EventCallback.Factory.Create<ConflictItem>(this, c => received = c)));

        cut.Find("[data-action='dismiss']").Click();

        Assert.NotNull(received);
        Assert.Equal("y", received!.Id);
    }
}
