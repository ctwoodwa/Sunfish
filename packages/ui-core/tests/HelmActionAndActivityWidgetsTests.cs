using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.UI;
using Sunfish.UICore.Wayfinder;
using Sunfish.UICore.Wayfinder.Widgets;
using Xunit;

namespace Sunfish.UICore.Tests;

/// <summary>
/// W#53 Phase 2 PR 2b — coverage for the two ActionStack +
/// ActivityFeed widgets (<see cref="QuickTogglesWidget"/> /
/// <see cref="RecentStandingOrdersWidget"/>) per ADR 0066 §1.4.
/// </summary>
public class HelmActionAndActivityWidgetsTests
{
    private static MissionEnvelope SampleEnvelope() =>
        new MissionEnvelope
        {
            Hardware = new() { ProbeStatus = ProbeStatus.Healthy },
            User = new() { ProbeStatus = ProbeStatus.Healthy, IsSignedIn = false },
            Regulatory = new() { ProbeStatus = ProbeStatus.Healthy },
            Runtime = new() { ProbeStatus = ProbeStatus.Healthy },
            FormFactor = new() { ProbeStatus = ProbeStatus.Healthy },
            Edition = new() { ProbeStatus = ProbeStatus.Healthy },
            Network = new() { ProbeStatus = ProbeStatus.Healthy, IsOnline = true },
            TrustAnchor = new() { ProbeStatus = ProbeStatus.Healthy, HasIdentityKey = false },
            SyncState = new() { ProbeStatus = ProbeStatus.Healthy, State = SyncState.Healthy },
            VersionVector = new() { ProbeStatus = ProbeStatus.Healthy },
            SnapshotAt = DateTimeOffset.UtcNow,
        };

    private static HelmRenderContext SampleContext() =>
        new(
            Envelope: SampleEnvelope(),
            Tenant: new TenantId("tenant-a"),
            Actor: new ActorId("actor-a"),
            ActiveTeamId: null,
            Now: DateTimeOffset.UtcNow);

    // ===== QuickTogglesWidget =====

    [Fact]
    public void QuickTogglesWidget_Metadata_PinsSlotAndOrderHint()
    {
        var w = new QuickTogglesWidget();
        Assert.Equal("quick-toggles", w.Metadata.WidgetId);
        Assert.Equal(HelmSlot.ActionStack, w.Metadata.Slot);
        Assert.Equal(100, w.Metadata.OrderHint);
        Assert.Equal("Quick toggles", w.Metadata.AccessibleName);
        Assert.Null(w.Metadata.CapabilityGateType);
    }

    [Fact]
    public async Task QuickTogglesWidget_RendersThreeIssueStandingOrderActions()
    {
        var w = new QuickTogglesWidget();
        var view = await w.ComputeAsync(SampleContext());

        Assert.Equal(SyncState.Healthy, view.State);
        Assert.Equal(3, view.Actions.Count);
        Assert.All(view.Actions, a =>
            Assert.Equal(HelmActionInvocationKind.IssueStandingOrder, a.Kind));

        var ids = view.Actions.Select(a => a.ActionId).ToArray();
        Assert.Contains("offline-mode", ids);
        Assert.Contains("dnd-mode", ids);
        Assert.Contains("pause-sync", ids);
    }

    [Theory]
    [InlineData("offline-mode", "system.network.offline|Platform")]
    [InlineData("dnd-mode", "system.notifications.dnd|User")]
    [InlineData("pause-sync", "system.sync.paused|Platform")]
    public async Task QuickTogglesWidget_TargetEncodesPathAndScopeCorrectly(
        string actionId,
        string expectedTarget)
    {
        var w = new QuickTogglesWidget();
        var view = await w.ComputeAsync(SampleContext());
        var action = view.Actions.Single(a => a.ActionId == actionId);
        Assert.Equal(expectedTarget, action.Target);
    }

    [Fact]
    public async Task QuickTogglesWidget_NullContext_Throws()
    {
        var w = new QuickTogglesWidget();
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await w.ComputeAsync(null!));
    }

    // ===== RecentStandingOrdersWidget =====

    [Fact]
    public void RecentStandingOrdersWidget_Metadata_PinsSlotAndOrderHint()
    {
        var w = new RecentStandingOrdersWidget();
        Assert.Equal("recent-standing-orders", w.Metadata.WidgetId);
        Assert.Equal(HelmSlot.ActivityFeed, w.Metadata.Slot);
        Assert.Equal(100, w.Metadata.OrderHint);
        Assert.Equal("Recent standing orders", w.Metadata.AccessibleName);
    }

    [Fact]
    public async Task RecentStandingOrdersWidget_NullSource_RendersNoRecentOrders()
    {
        var w = new RecentStandingOrdersWidget(source: null);
        var view = await w.ComputeAsync(SampleContext());

        Assert.Equal(SyncState.Healthy, view.State);
        Assert.Equal("Recent standing orders", view.PrimaryLabel);
        Assert.Equal("No recent orders", view.SecondaryLabel);
        Assert.Empty(view.Actions);
    }

    [Fact]
    public async Task RecentStandingOrdersWidget_EmptySource_RendersNoRecentOrders()
    {
        var source = Substitute.For<IRecentStandingOrdersSource>();
        source.GetRecentAsync(
                Arg.Any<TenantId>(),
                Arg.Any<ActorId>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecentStandingOrderEntry>>(Array.Empty<RecentStandingOrderEntry>()));

        var w = new RecentStandingOrdersWidget(source);
        var view = await w.ComputeAsync(SampleContext());

        Assert.Equal(SyncState.Healthy, view.State);
        Assert.Equal("No recent orders", view.SecondaryLabel);
        Assert.Empty(view.Actions);
    }

    [Fact]
    public async Task RecentStandingOrdersWidget_PopulatedSource_RendersOneActionPerEntry()
    {
        var entries = new[]
        {
            new RecentStandingOrderEntry(
                new StandingOrderId(Guid.NewGuid()),
                "system.network.offline",
                DateTimeOffset.UtcNow.AddMinutes(-1),
                "Captain Reyes"),
            new RecentStandingOrderEntry(
                new StandingOrderId(Guid.NewGuid()),
                "system.sync.paused",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                "XO Vega"),
        };
        var source = Substitute.For<IRecentStandingOrdersSource>();
        source.GetRecentAsync(
                Arg.Any<TenantId>(),
                Arg.Any<ActorId>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecentStandingOrderEntry>>(entries));

        var w = new RecentStandingOrdersWidget(source);
        var view = await w.ComputeAsync(SampleContext());

        Assert.Equal(SyncState.Healthy, view.State);
        Assert.Equal("2 recent", view.SecondaryLabel);
        Assert.Equal(2, view.Actions.Count);
        Assert.All(view.Actions, a =>
            Assert.Equal(HelmActionInvocationKind.Navigate, a.Kind));
        Assert.Contains(view.Actions, a => a.AccessibleLabel.Contains("Captain Reyes"));
        Assert.Contains(view.Actions, a => a.AccessibleLabel.Contains("XO Vega"));
    }

    [Fact]
    public async Task RecentStandingOrdersWidget_RequestsMaxFiveEntries()
    {
        var captured = 0;
        var source = Substitute.For<IRecentStandingOrdersSource>();
        source.GetRecentAsync(
                Arg.Any<TenantId>(),
                Arg.Any<ActorId>(),
                Arg.Do<int>(n => captured = n),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecentStandingOrderEntry>>(Array.Empty<RecentStandingOrderEntry>()));

        var w = new RecentStandingOrdersWidget(source);
        await w.ComputeAsync(SampleContext());

        Assert.Equal(RecentStandingOrdersWidget.MaxEntries, captured);
        Assert.Equal(5, captured);
    }

    [Fact]
    public async Task RecentStandingOrdersWidget_SourceFaults_DegradesToStale()
    {
        var source = Substitute.For<IRecentStandingOrdersSource>();
        source.GetRecentAsync(
                Arg.Any<TenantId>(),
                Arg.Any<ActorId>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<RecentStandingOrderEntry>>>(_ =>
                throw new InvalidOperationException("repository unavailable"));

        var w = new RecentStandingOrdersWidget(source);
        var view = await w.ComputeAsync(SampleContext());

        Assert.Equal(SyncState.Stale, view.State);
        Assert.Equal("Source unavailable", view.SecondaryLabel);
        Assert.Empty(view.Actions);
    }

    [Fact]
    public async Task RecentStandingOrdersWidget_SourceCancellation_PropagatesOperationCanceled()
    {
        var source = Substitute.For<IRecentStandingOrdersSource>();
        source.GetRecentAsync(
                Arg.Any<TenantId>(),
                Arg.Any<ActorId>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<RecentStandingOrderEntry>>>(_ =>
                throw new OperationCanceledException());

        var w = new RecentStandingOrdersWidget(source);
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await w.ComputeAsync(SampleContext()));
    }

    // ===== Cross-widget invariants =====

    [Fact]
    public void ActionStack_And_ActivityFeed_Widgets_HaveExpectedSlots()
    {
        Assert.Equal(HelmSlot.ActionStack, new QuickTogglesWidget().Metadata.Slot);
        Assert.Equal(HelmSlot.ActivityFeed, new RecentStandingOrdersWidget().Metadata.Slot);
    }

    [Fact]
    public void Phase2B_Widgets_HaveAccessibleName_PerWcag412()
    {
        IHelmWidget[] widgets =
        {
            new QuickTogglesWidget(),
            new RecentStandingOrdersWidget(),
        };
        Assert.All(widgets, w =>
            Assert.False(string.IsNullOrWhiteSpace(w.Metadata.AccessibleName)));
    }
}
