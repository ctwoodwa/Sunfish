using System;
using System.Linq;
using Bunit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.UI;
using Sunfish.UIAdapters.Blazor.Wayfinder;
using Sunfish.UICore.Wayfinder;
using Sunfish.UICore.Wayfinder.Widgets;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests;

/// <summary>
/// W#53 Phase 2 PR 2c — Blazor HelmRenderer WCAG + parity tests
/// per ADR 0066 §1.4 + hand-off line 685-694.
/// </summary>
public class HelmRendererTests : BunitContext
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

    private sealed class TestRegistry : IHelmWidgetRegistry
    {
        private readonly System.Collections.Generic.List<IHelmWidget> _widgets;
        public TestRegistry(System.Collections.Generic.IEnumerable<IHelmWidget> widgets)
        {
            _widgets = widgets
                .OrderBy(w => (int)w.Metadata.Slot)
                .ThenBy(w => w.Metadata.OrderHint)
                .ThenBy(w => w.Metadata.WidgetId, System.StringComparer.Ordinal)
                .ToList();
        }
        public System.Collections.Generic.IReadOnlyList<IHelmWidget> Widgets => _widgets;
        public System.Collections.Generic.IReadOnlyList<IHelmWidget> GetSlot(HelmSlot slot) =>
            _widgets.Where(w => w.Metadata.Slot == slot).ToList();
    }

    private static IHelmWidgetRegistry CanonicalRegistry() =>
        new TestRegistry(new IHelmWidget[]
        {
            new IdentityGlanceWidget(),
            new SyncStateWidget(),
            new ActiveTeamWidget(),
            new MissionEnvelopeSummaryWidget(),
            new QuickTogglesWidget(),
            new RecentStandingOrdersWidget(),
        });

    [Fact]
    public void HelmRenderer_RendersNavLandmark_WithDefaultAriaLabel()
    {
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext()));

        var nav = cut.Find("nav.sunfish-helm");
        Assert.Equal("Helm", nav.GetAttribute("aria-label"));
    }

    [Fact]
    public void HelmRenderer_RendersThreeSlotGroups()
    {
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext()));

        var slots = cut.FindAll(".sunfish-helm > div[role='group']");
        Assert.Equal(3, slots.Count);

        var labels = slots.Select(s => s.GetAttribute("aria-label")).ToArray();
        // Slot aria-labels are humans-friendly defaults per council
        // M1 amendment (not the raw enum names "GlanceBand" /
        // "ActionStack" / "ActivityFeed"). Hosts override via
        // [Parameter] for localization.
        Assert.Contains("Status", labels);
        Assert.Contains("Actions", labels);
        Assert.Contains("Activity", labels);
    }

    [Fact]
    public void HelmRenderer_AllWidgets_HaveAccessibleNameAriaLabel_PerWcag412()
    {
        // Hand-off Phase 2 acceptance gate row 1: every widget region
        // has aria-label = AccessibleName.
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext()));

        var sections = cut.FindAll("section.sunfish-helm-widget");
        Assert.Equal(6, sections.Count); // 4 GlanceBand + 1 ActionStack + 1 ActivityFeed

        Assert.All(sections, section =>
        {
            var ariaLabel = section.GetAttribute("aria-label");
            Assert.False(string.IsNullOrWhiteSpace(ariaLabel));
        });

        // Verify each canonical widget id is present + aria-label
        // matches the widget's documented AccessibleName.
        var idToLabel = sections.ToDictionary(
            s => s.GetAttribute("data-widget-id")!,
            s => s.GetAttribute("aria-label")!);

        Assert.Equal("Identity glance", idToLabel["identity-glance"]);
        Assert.Equal("Sync state", idToLabel["sync-state"]);
        Assert.Equal("Active team", idToLabel["active-team"]);
        Assert.Equal("Mission envelope", idToLabel["mission-envelope-summary"]);
        Assert.Equal("Quick toggles", idToLabel["quick-toggles"]);
        Assert.Equal("Recent standing orders", idToLabel["recent-standing-orders"]);
    }

    [Fact]
    public void HelmRenderer_SyncStateWidget_HasAriaLivePolite_PerWcag413()
    {
        // Hand-off Phase 2 acceptance gate row 2: SyncState
        // transitions fire aria-live="polite" (NOT assertive).
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext()));

        var syncSection = cut.Find("section[data-widget-id='sync-state']");
        Assert.Equal("polite", syncSection.GetAttribute("aria-live"));
    }

    [Fact]
    public void HelmRenderer_NonSyncStateWidgets_DoNotHaveAriaLive_AvoidingScreenReaderNoise()
    {
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext()));

        var nonSync = cut.FindAll("section.sunfish-helm-widget")
            .Where(s => s.GetAttribute("data-widget-id") != "sync-state");

        Assert.All(nonSync, s =>
            Assert.True(string.IsNullOrEmpty(s.GetAttribute("aria-live"))));
    }

    [Fact]
    public void HelmRenderer_QuickToggles_RendersThreeButtons_WithCorrectActionAttributes()
    {
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext()));

        var quickToggles = cut.Find("section[data-widget-id='quick-toggles']");
        var buttons = quickToggles.QuerySelectorAll("button.sunfish-helm-action");
        Assert.Equal(3, buttons.Length);

        var actionIds = buttons.Select(b => b.GetAttribute("data-action-id")).ToArray();
        Assert.Contains("offline-mode", actionIds);
        Assert.Contains("dnd-mode", actionIds);
        Assert.Contains("pause-sync", actionIds);

        // All toggle buttons carry data-action-kind=IssueStandingOrder.
        Assert.All(buttons, b =>
            Assert.Equal("IssueStandingOrder", b.GetAttribute("data-action-kind")));

        // Target encodes "{Path}|{Scope}" per the IHelmWidget xmldoc.
        var targets = buttons.Select(b => b.GetAttribute("data-action-target")).ToArray();
        Assert.Contains("system.network.offline|Platform", targets);
        Assert.Contains("system.notifications.dnd|User", targets);
        Assert.Contains("system.sync.paused|Platform", targets);

        // Per WCAG 2.5.3 (Label in Name): visible button text IS the
        // accessible name; aria-label deliberately omitted per
        // council M2 amendment.
        Assert.All(buttons, b =>
            Assert.True(string.IsNullOrEmpty(b.GetAttribute("aria-label"))));
    }

    [Fact]
    public void HelmRenderer_IdentityGlance_RendersTwoNavigateActions()
    {
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext()));

        var section = cut.Find("section[data-widget-id='identity-glance']");
        var buttons = section.QuerySelectorAll("button.sunfish-helm-action");
        Assert.Equal(2, buttons.Length);
        Assert.All(buttons, b =>
            Assert.Equal("Navigate", b.GetAttribute("data-action-kind")));
    }

    [Fact]
    public void HelmRenderer_RecentStandingOrders_NoSource_RendersNoActions()
    {
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext()));

        var section = cut.Find("section[data-widget-id='recent-standing-orders']");
        var buttons = section.QuerySelectorAll("button.sunfish-helm-action");
        Assert.Empty(buttons);
        // Secondary label conveys empty state.
        var secondary = section.QuerySelector(".sunfish-helm-widget-secondary");
        Assert.NotNull(secondary);
        Assert.Equal("No recent orders", secondary!.TextContent.Trim());
    }

    [Fact]
    public void HelmRenderer_GlanceBandSlot_RendersWidgetsInOrderHintOrder()
    {
        // Cohort-invariant: GlanceBand widgets are 100/200/300/400 →
        // Identity / Sync / Team / Envelope in that order.
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext()));

        var glanceBand = cut.FindAll(".sunfish-helm > div[role='group']")
            .Single(d => d.GetAttribute("aria-label") == "Status");
        var ids = glanceBand
            .QuerySelectorAll("section.sunfish-helm-widget")
            .Select(s => s.GetAttribute("data-widget-id")).ToArray();
        Assert.Equal(new[]
        {
            "identity-glance",
            "sync-state",
            "active-team",
            "mission-envelope-summary",
        }, ids);
    }

    [Fact]
    public void HelmRenderer_NoRegistry_RendersEmptyNav()
    {
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, null!)
            .Add(c => c.Context, SampleContext()));

        var nav = cut.Find("nav.sunfish-helm");
        Assert.Empty(nav.QuerySelectorAll("section"));
    }

    [Fact]
    public void HelmRenderer_CustomSlotLabels_OverrideHumansFriendlyDefaults()
    {
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext())
            .Add(c => c.GlanceBandSlotLabel, "État")
            .Add(c => c.ActionStackSlotLabel, "Actions rapides")
            .Add(c => c.ActivityFeedSlotLabel, "Activité"));

        var labels = cut.FindAll(".sunfish-helm > div[role='group']")
            .Select(s => s.GetAttribute("aria-label")).ToArray();
        Assert.Contains("État", labels);
        Assert.Contains("Actions rapides", labels);
        Assert.Contains("Activité", labels);
    }

    [Fact]
    public void HelmRenderer_CustomAriaLabel_ReplacesDefault()
    {
        var cut = Render<HelmRenderer>(p => p
            .Add(c => c.Registry, CanonicalRegistry())
            .Add(c => c.Context, SampleContext())
            .Add(c => c.HelmAriaLabel, "Operator Helm — tenant-a"));

        var nav = cut.Find("nav.sunfish-helm");
        Assert.Equal("Operator Helm — tenant-a", nav.GetAttribute("aria-label"));
    }
}
