using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.UI;
using Sunfish.UICore.Wayfinder;
using Sunfish.UICore.Wayfinder.Widgets;
using Xunit;

namespace Sunfish.UICore.Tests;

/// <summary>
/// W#53 Phase 2 PR 2a — coverage for the four GlanceBand widgets
/// (<see cref="IdentityGlanceWidget"/> /
/// <see cref="SyncStateWidget"/> / <see cref="ActiveTeamWidget"/> /
/// <see cref="MissionEnvelopeSummaryWidget"/>) per ADR 0066 §1.4.
/// </summary>
public class HelmGlanceWidgetsTests
{
    private static MissionEnvelope SampleEnvelope(SyncState syncState = SyncState.Healthy) =>
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
            SyncState = new() { ProbeStatus = ProbeStatus.Healthy, State = syncState },
            VersionVector = new() { ProbeStatus = ProbeStatus.Healthy },
            SnapshotAt = DateTimeOffset.UtcNow,
        };

    private static HelmRenderContext SampleContext(
        SyncState syncState = SyncState.Healthy,
        Guid? activeTeamId = null) =>
        new(
            Envelope: SampleEnvelope(syncState),
            Tenant: new TenantId("tenant-a"),
            Actor: new ActorId("actor-a"),
            ActiveTeamId: activeTeamId,
            Now: DateTimeOffset.UtcNow);

    // ===== IdentityGlanceWidget =====

    [Fact]
    public void IdentityGlanceWidget_Metadata_PinsSlotAndOrderHint()
    {
        var w = new IdentityGlanceWidget();
        Assert.Equal("identity-glance", w.Metadata.WidgetId);
        Assert.Equal(HelmSlot.GlanceBand, w.Metadata.Slot);
        Assert.Equal(100, w.Metadata.OrderHint);
        Assert.Equal("Identity glance", w.Metadata.AccessibleName);
        Assert.Null(w.Metadata.CapabilityGateType);
    }

    [Fact]
    public async Task IdentityGlanceWidget_PlaceholderState_ShipsStaleAndTwoActions()
    {
        var w = new IdentityGlanceWidget();
        var view = await w.ComputeAsync(SampleContext());

        Assert.Equal(SyncState.Stale, view.State);
        Assert.Equal("Identity", view.PrimaryLabel);
        // i18n discipline: placeholder Phase-2a impl emits null
        // SecondaryLabel; SyncState.Stale carries the placeholder
        // semantics for the renderer.
        Assert.Null(view.SecondaryLabel);

        Assert.Equal(2, view.Actions.Count);
        Assert.Contains(view.Actions, a => a.ActionId == "rotate-key");
        Assert.Contains(view.Actions, a => a.ActionId == "recovery-contacts");
        Assert.All(view.Actions, a =>
            Assert.Equal(HelmActionInvocationKind.Navigate, a.Kind));
    }

    // ===== SyncStateWidget =====

    [Fact]
    public void SyncStateWidget_Metadata_PinsSlotAndOrderHint()
    {
        var w = new SyncStateWidget();
        Assert.Equal("sync-state", w.Metadata.WidgetId);
        Assert.Equal(HelmSlot.GlanceBand, w.Metadata.Slot);
        Assert.Equal(200, w.Metadata.OrderHint);
        Assert.Equal("Sync state", w.Metadata.AccessibleName);
    }

    [Theory]
    [InlineData(SyncState.Healthy, "healthy")]
    [InlineData(SyncState.Stale, "stale")]
    [InlineData(SyncState.Conflict, "conflict")]
    [InlineData(SyncState.Offline, "offline")]
    [InlineData(SyncState.Quarantine, "quarantine")]
    public async Task SyncStateWidget_RendersCanonicalLabel_ForAmbientEnvelope(
        SyncState ambientSync,
        string expectedLabel)
    {
        var w = new SyncStateWidget();
        var view = await w.ComputeAsync(SampleContext(syncState: ambientSync));

        Assert.Equal(ambientSync, view.State);
        Assert.Equal(expectedLabel, view.PrimaryLabel);
        Assert.Empty(view.Actions);
    }

    [Fact]
    public async Task SyncStateWidget_NullContext_Throws()
    {
        var w = new SyncStateWidget();
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await w.ComputeAsync(null!));
    }

    // ===== ActiveTeamWidget =====

    [Fact]
    public void ActiveTeamWidget_Metadata_PinsSlotAndOrderHint()
    {
        var w = new ActiveTeamWidget();
        Assert.Equal("active-team", w.Metadata.WidgetId);
        Assert.Equal(HelmSlot.GlanceBand, w.Metadata.Slot);
        Assert.Equal(300, w.Metadata.OrderHint);
        Assert.Equal("Active team", w.Metadata.AccessibleName);
    }

    [Fact]
    public async Task ActiveTeamWidget_NullActiveTeamId_RendersNoActiveTeamPlusSelectAffordance()
    {
        var w = new ActiveTeamWidget();
        var view = await w.ComputeAsync(SampleContext(activeTeamId: null));

        Assert.Equal(SyncState.Healthy, view.State);
        Assert.Equal("No active team", view.PrimaryLabel);
        Assert.Single(view.Actions);
        Assert.Equal("select-team", view.Actions[0].ActionId);
        Assert.Equal(HelmActionInvocationKind.Navigate, view.Actions[0].Kind);
    }

    [Fact]
    public async Task ActiveTeamWidget_WithActiveTeamId_RendersIdAndSwitchAffordance()
    {
        var teamId = Guid.NewGuid();
        var w = new ActiveTeamWidget();
        var view = await w.ComputeAsync(SampleContext(activeTeamId: teamId));

        Assert.Equal(SyncState.Healthy, view.State);
        Assert.Contains(teamId.ToString("N"), view.PrimaryLabel);
        Assert.Single(view.Actions);
        Assert.Equal("switch-team", view.Actions[0].ActionId);
    }

    // ===== MissionEnvelopeSummaryWidget =====

    [Fact]
    public void MissionEnvelopeSummaryWidget_Metadata_PinsSlotAndOrderHint()
    {
        var w = new MissionEnvelopeSummaryWidget();
        Assert.Equal("mission-envelope-summary", w.Metadata.WidgetId);
        Assert.Equal(HelmSlot.GlanceBand, w.Metadata.Slot);
        Assert.Equal(400, w.Metadata.OrderHint);
        Assert.Equal("Mission envelope", w.Metadata.AccessibleName);
    }

    [Fact]
    public async Task MissionEnvelopeSummaryWidget_AllDimensionsPresent_RendersHealthyWithCount()
    {
        var w = new MissionEnvelopeSummaryWidget();
        var view = await w.ComputeAsync(SampleContext());

        Assert.Equal(SyncState.Healthy, view.State);
        Assert.Equal("Mission envelope", view.PrimaryLabel);
        Assert.Contains("10 dimensions active", view.SecondaryLabel ?? "");
        Assert.Empty(view.Actions);
    }

    // ===== Cross-widget invariants =====

    [Fact]
    public void GlanceBand_Widgets_HaveDistinctOrderHints_AndAllRenderToGlanceBand()
    {
        IHelmWidget[] widgets =
        {
            new IdentityGlanceWidget(),
            new SyncStateWidget(),
            new ActiveTeamWidget(),
            new MissionEnvelopeSummaryWidget(),
        };

        Assert.All(widgets, w => Assert.Equal(HelmSlot.GlanceBand, w.Metadata.Slot));

        var orderHints = widgets.Select(w => w.Metadata.OrderHint).ToArray();
        Assert.Equal(orderHints.Length, orderHints.Distinct().Count());

        // Ascending sort matches the documented OrderHint sequence
        // (100, 200, 300, 400) so the GlanceBand renders in the
        // canonical order: Identity → Sync → Team → Envelope.
        Assert.Equal(orderHints.OrderBy(h => h), orderHints);
    }

    [Fact]
    public void GlanceBand_Widgets_AllHaveAccessibleName_PerWcag412()
    {
        IHelmWidget[] widgets =
        {
            new IdentityGlanceWidget(),
            new SyncStateWidget(),
            new ActiveTeamWidget(),
            new MissionEnvelopeSummaryWidget(),
        };

        Assert.All(widgets, w =>
            Assert.False(string.IsNullOrWhiteSpace(w.Metadata.AccessibleName)));
    }

    [Fact]
    public void GlanceBand_Widgets_NoneHaveCapabilityGate_InPhase2A()
    {
        // Phase 2 PR 2a ships unconditional widgets — Phase 3a +
        // ICapabilityGate<T> Hookup will add gating when the
        // generic gate type lands on origin/main.
        IHelmWidget[] widgets =
        {
            new IdentityGlanceWidget(),
            new SyncStateWidget(),
            new ActiveTeamWidget(),
            new MissionEnvelopeSummaryWidget(),
        };

        Assert.All(widgets, w => Assert.Null(w.Metadata.CapabilityGateType));
    }
}
