using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.UI;
using Sunfish.UICore.Wayfinder;
using Xunit;

namespace Sunfish.UICore.Tests;

/// <summary>
/// W#53 P1a — Helm widget registry + DI extensions per ADR 0066 §1.1.
/// </summary>
public class HelmWidgetRegistryTests
{
    [Fact]
    public void DefaultRegistry_OrdersBy_SlotThenOrderHint()
    {
        var registry = new DefaultHelmWidgetRegistry(new IHelmWidget[]
        {
            new StubWidget("z-late-action", HelmSlot.ActionStack, orderHint: 100),
            new StubWidget("a-early-glance", HelmSlot.GlanceBand, orderHint: 1),
            new StubWidget("m-mid-action", HelmSlot.ActionStack, orderHint: 50),
            new StubWidget("b-late-glance", HelmSlot.GlanceBand, orderHint: 99),
            new StubWidget("activity-only", HelmSlot.ActivityFeed, orderHint: 5),
        });

        var ids = registry.Widgets.Select(w => w.Metadata.WidgetId).ToList();
        Assert.Equal(new[]
        {
            "a-early-glance",
            "b-late-glance",
            "m-mid-action",
            "z-late-action",
            "activity-only",
        }, ids);
    }

    [Fact]
    public void GetSlot_ReturnsOnlyMatchingWidgets()
    {
        var registry = new DefaultHelmWidgetRegistry(new IHelmWidget[]
        {
            new StubWidget("g1", HelmSlot.GlanceBand, 1),
            new StubWidget("g2", HelmSlot.GlanceBand, 2),
            new StubWidget("a1", HelmSlot.ActionStack, 1),
        });

        var glance = registry.GetSlot(HelmSlot.GlanceBand);
        Assert.Equal(2, glance.Count);
        Assert.All(glance, w => Assert.Equal(HelmSlot.GlanceBand, w.Metadata.Slot));

        var feed = registry.GetSlot(HelmSlot.ActivityFeed);
        Assert.Empty(feed);
    }

    [Fact]
    public void DefaultRegistry_TieOnOrderHint_PreservesRegistrationOrder()
    {
        // LINQ OrderBy is stable; ties resolve to registration order.
        var registry = new DefaultHelmWidgetRegistry(new IHelmWidget[]
        {
            new StubWidget("first-tie", HelmSlot.GlanceBand, 5),
            new StubWidget("second-tie", HelmSlot.GlanceBand, 5),
            new StubWidget("third-tie", HelmSlot.GlanceBand, 5),
        });

        var ids = registry.GetSlot(HelmSlot.GlanceBand).Select(w => w.Metadata.WidgetId).ToList();
        Assert.Equal(new[] { "first-tie", "second-tie", "third-tie" }, ids);
    }

    [Fact]
    public void AddSunfishHelm_NoOverloadConfig_BindsDefaults()
    {
        var services = new ServiceCollection();
        services.AddSunfishHelm();
        var provider = services.BuildServiceProvider();

        var opts = provider.GetRequiredService<IOptions<HelmOptions>>().Value;
        Assert.Equal(TimeSpan.FromMinutes(1), opts.PeriodicRefreshInterval);
        // Registry is registered even with no widgets.
        var registry = provider.GetRequiredService<IHelmWidgetRegistry>();
        Assert.Empty(registry.Widgets);
    }

    [Fact]
    public void AddSunfishHelm_ConfigureCallback_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddSunfishHelm(opts => opts.PeriodicRefreshInterval = TimeSpan.FromSeconds(30));
        var provider = services.BuildServiceProvider();

        var opts = provider.GetRequiredService<IOptions<HelmOptions>>().Value;
        Assert.Equal(TimeSpan.FromSeconds(30), opts.PeriodicRefreshInterval);
    }

    [Fact]
    public void AddHelmWidget_RegistersWidgetIntoRegistry()
    {
        var services = new ServiceCollection();
        services.AddSunfishHelm();
        services.AddHelmWidget<RegisteredStubWidget>();
        var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IHelmWidgetRegistry>();
        Assert.Single(registry.Widgets);
        Assert.Equal("registered-stub", registry.Widgets[0].Metadata.WidgetId);
    }

    [Fact]
    public void AddSunfishHelm_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HelmServiceCollectionExtensions.AddSunfishHelm(null!));
    }

    [Fact]
    public void AddSunfishHelm_NullConfigure_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddSunfishHelm(configure: null!));
    }

    [Fact]
    public void HelmSlot_HasExactlyThreeValues()
    {
        var values = Enum.GetValues<HelmSlot>();
        Assert.Equal(3, values.Length);
        Assert.Contains(HelmSlot.GlanceBand, values);
        Assert.Contains(HelmSlot.ActionStack, values);
        Assert.Contains(HelmSlot.ActivityFeed, values);
    }

    [Fact]
    public void HelmActionInvocationKind_HasExactlyThreeValues()
    {
        var values = Enum.GetValues<HelmActionInvocationKind>();
        Assert.Equal(3, values.Length);
        Assert.Contains(HelmActionInvocationKind.Navigate, values);
        Assert.Contains(HelmActionInvocationKind.IssueStandingOrder, values);
        Assert.Contains(HelmActionInvocationKind.RunLocalCommand, values);
    }

    [Fact]
    public void IAtlasProvider_GenericConstraint_RequiresReferenceType()
    {
        // Verify the where TView : class constraint holds at type-system level.
        var t = typeof(IAtlasProvider<>);
        var typeParam = t.GetGenericArguments()[0];
        Assert.True(typeParam.GenericParameterAttributes.HasFlag(
            System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint));
    }

    private sealed class StubWidget : IHelmWidget
    {
        public StubWidget(string id, HelmSlot slot, int orderHint)
        {
            Metadata = new HelmWidgetMetadata(
                WidgetId: id,
                Slot: slot,
                OrderHint: orderHint,
                AccessibleName: id,
                CapabilityGateType: null);
        }

        public HelmWidgetMetadata Metadata { get; }

        public ValueTask<HelmWidgetViewState> ComputeAsync(
            HelmRenderContext context, CancellationToken ct = default) =>
            ValueTask.FromResult(new HelmWidgetViewState(
                State: SyncState.Healthy,
                PrimaryLabel: Metadata.WidgetId,
                SecondaryLabel: null,
                Actions: System.Array.Empty<HelmWidgetAction>()));
    }

    private sealed class RegisteredStubWidget : IHelmWidget
    {
        public HelmWidgetMetadata Metadata { get; } = new HelmWidgetMetadata(
            WidgetId: "registered-stub",
            Slot: HelmSlot.GlanceBand,
            OrderHint: 0,
            AccessibleName: "Registered stub",
            CapabilityGateType: null);

        public ValueTask<HelmWidgetViewState> ComputeAsync(
            HelmRenderContext context, CancellationToken ct = default) =>
            ValueTask.FromResult(new HelmWidgetViewState(
                State: SyncState.Healthy,
                PrimaryLabel: "stub",
                SecondaryLabel: null,
                Actions: System.Array.Empty<HelmWidgetAction>()));
    }
}
