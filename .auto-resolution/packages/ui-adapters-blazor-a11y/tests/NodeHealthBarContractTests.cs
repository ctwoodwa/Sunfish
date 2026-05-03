using System;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests;

/// <summary>
/// Plan 4B §5 — assert SunfishNodeHealthBar honours the ADR 0036 contract:
/// the host group has role="group" + an accessible label; child
/// SunfishSyncStatusIndicator instances inherit the role/aria-live contract from
/// the per-state mapping codified in the indicator's contract tests.
/// </summary>
public class NodeHealthBarContractTests : IClassFixture<NodeHealthBarContractTests.Ctx>
{
    private readonly Ctx _ctx;

    public NodeHealthBarContractTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public void Bar_HostHasRoleGroupAndAriaLabel()
    {
        var rendered = _ctx.Bunit.Render<SunfishNodeHealthBar>();
        var host = rendered.Find("div");

        Assert.Equal("group", host.GetAttribute("role"));
        Assert.Equal("Node health status", host.GetAttribute("aria-label"));
    }

    [Fact]
    public void Bar_AriaLabelCanBeOverridden()
    {
        var rendered = _ctx.Bunit.Render<SunfishNodeHealthBar>(p => p.Add(c => c.AriaLabel, "Bridge node telemetry"));
        var host = rendered.Find("div");

        Assert.Equal("Bridge node telemetry", host.GetAttribute("aria-label"));
    }

    [Fact]
    public void Bar_ChildIndicators_InheritStatusRoleByDefault()
    {
        // All three axes default to Healthy → role="status".
        var rendered = _ctx.Bunit.Render<SunfishNodeHealthBar>();
        var buttons = rendered.FindAll("button");

        Assert.Equal(3, buttons.Count);
        foreach (var btn in buttons)
        {
            Assert.Equal("status", btn.GetAttribute("role"));
            Assert.Equal("polite", btn.GetAttribute("aria-live"));
        }
    }

    [Fact]
    public void Bar_ChildIndicator_PromotesToAlertWhenStateIsConflict()
    {
        var rendered = _ctx.Bunit.Render<SunfishNodeHealthBar>(p => p
            .Add(c => c.NodeHealth, SyncState.Healthy)
            .Add(c => c.LinkStatus, SyncState.ConflictPending)
            .Add(c => c.DataFreshness, SyncState.Stale));

        var buttons = rendered.FindAll("button");
        Assert.Equal(3, buttons.Count);
        Assert.Equal("status", buttons[0].GetAttribute("role"));   // NodeHealth: Healthy
        Assert.Equal("alert", buttons[1].GetAttribute("role"));    // LinkStatus: ConflictPending
        Assert.Equal("assertive", buttons[1].GetAttribute("aria-live"));
        Assert.Equal("status", buttons[2].GetAttribute("role"));   // DataFreshness: Stale
    }

    public sealed class Ctx : IDisposable
    {
        public BunitContext Bunit { get; }

        public Ctx()
        {
            Bunit = new BunitContext();
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
        }

        public void Dispose() => Bunit.Dispose();
    }
}
