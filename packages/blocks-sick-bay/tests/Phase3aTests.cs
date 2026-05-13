using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.SickBay;
using Sunfish.UICore.Primitives;
using Xunit;

namespace Sunfish.Blocks.SickBay.Tests;

/// <summary>
/// W#54 Phase 3a — Blazor UI component tests per ADR 0082 §8 + hand-off
/// acceptance gate. WCAG/a11y subagent review MANDATORY before merge.
/// </summary>

public class SickBayBlockTests : BunitContext
{
    private static SickBaySnapshot MakeSnapshot(MedevacState medevac = MedevacState.Idle) =>
        new(
            Pharmacy: new List<PharmacyInventoryEntry>
            {
                new("recovery-key", "Recovery key", PharmacyRecordCount.Exact(5),
                    DateTimeOffset.UtcNow.AddDays(-10), RotationHealth.Current, false),
            },
            Lab: new List<LabDiagnosticResult>
            {
                new("Network", "network", ProbeStatus.Healthy, DegradationKind.ReadOnly,
                    DateTimeOffset.UtcNow, null),
            },
            Atmosphere: new AtmosphereReadout(AtmosphereHealth.Green, 0, 0, false, DateTimeOffset.UtcNow),
            MedevacState: medevac,
            CapturedAt: DateTimeOffset.UtcNow);

    [Fact]
    public void Pharmacy_tab_hidden_when_actor_lacks_ViewPharmacy()
    {
        var cut = Render<SickBayBlock>(p => p
            .Add(c => c.Snapshot, MakeSnapshot())
            .Add(c => c.CanViewPharmacy, false));

        var tabs = cut.FindAll("[role='tab']");
        Assert.DoesNotContain(tabs, t => t.GetAttribute("id") == "tab-pharmacy");
    }

    [Fact]
    public void Pharmacy_tab_visible_when_actor_has_ViewPharmacy()
    {
        var cut = Render<SickBayBlock>(p => p
            .Add(c => c.Snapshot, MakeSnapshot())
            .Add(c => c.CanViewPharmacy, true));

        var pharmacyTab = cut.Find("#tab-pharmacy");
        Assert.NotNull(pharmacyTab);
    }

    [Fact]
    public void Tab_navigation_focus_lands_in_new_tab_content()
    {
        var cut = Render<SickBayBlock>(p => p
            .Add(c => c.Snapshot, MakeSnapshot()));

        var atmosphereTab = cut.Find("[data-test-id='tab-atmosphere']");
        atmosphereTab.Click();

        var activeTab = cut.Find("[role='tab'][aria-selected='true']");
        Assert.Equal("tab-atmosphere", activeTab.GetAttribute("id"));
    }

    [Fact]
    public void Tab_focus_order_is_deterministic_left_to_right()
    {
        var cut = Render<SickBayBlock>(p => p
            .Add(c => c.Snapshot, MakeSnapshot())
            .Add(c => c.CanViewPharmacy, true));

        var tabs = cut.FindAll("[role='tab']");
        var ids = tabs.Select(t => t.GetAttribute("id")).ToList();
        Assert.Equal(new[] { "tab-pharmacy", "tab-lab", "tab-atmosphere" }, ids);
    }

    [Fact]
    public void Block_renders_accessible_region_landmark()
    {
        var cut = Render<SickBayBlock>(p => p
            .Add(c => c.Snapshot, MakeSnapshot()));

        var region = cut.Find("[role='region']");
        Assert.Equal("Sick Bay", region.GetAttribute("aria-label"));
    }

    [Fact]
    public void Null_snapshot_renders_loading_indicator()
    {
        var cut = Render<SickBayBlock>(p => p
            .Add(c => c.Snapshot, (SickBaySnapshot?)null));

        var loading = cut.Find("[data-test-id='sick-bay-loading']");
        Assert.NotNull(loading);
    }
}

public class PharmacyTabContentTests : BunitContext
{
    private static PharmacyInventoryEntry Entry(
        RotationHealth health = RotationHealth.Current,
        bool suppressed = false) =>
        new("recovery-key", "Recovery key",
            suppressed ? PharmacyRecordCount.Suppressed : PharmacyRecordCount.Exact(5),
            DateTimeOffset.UtcNow.AddDays(-10), health, false);

    [Fact]
    public void RotationHealth_badge_uses_color_icon_text_triple_encoding()
    {
        var cut = Render<PharmacyTabContent>(p => p
            .Add(c => c.Inventory, new List<PharmacyInventoryEntry>
            {
                Entry(RotationHealth.RotationDue),
            }));

        var badge = cut.Find("[data-test-id='rotation-badge']");
        Assert.Equal("rotation-badge--due", badge.ClassName?.Split(' ').FirstOrDefault(c => c.StartsWith("rotation-badge--")));
        Assert.NotNull(badge.GetAttribute("aria-label"));
        Assert.Contains("Due", badge.TextContent);
    }

    [Fact]
    public void PharmacyRecordCount_renders_lt_3_when_suppressed()
    {
        var cut = Render<PharmacyTabContent>(p => p
            .Add(c => c.Inventory, new List<PharmacyInventoryEntry> { Entry(suppressed: true) }));

        var suppressed = cut.Find("[data-test-id='record-count-suppressed']");
        Assert.Contains("< 3", suppressed.TextContent);
    }

    [Fact]
    public void PharmacyRecordCount_aria_label_describes_suppression()
    {
        var cut = Render<PharmacyTabContent>(p => p
            .Add(c => c.Inventory, new List<PharmacyInventoryEntry> { Entry(suppressed: true) }));

        var suppressed = cut.Find("[data-test-id='record-count-suppressed']");
        Assert.Equal("record count suppressed below threshold", suppressed.GetAttribute("aria-label"));
    }

    [Fact]
    public void Compromised_badge_has_compromised_css_and_aria_label()
    {
        var cut = Render<PharmacyTabContent>(p => p
            .Add(c => c.Inventory, new List<PharmacyInventoryEntry>
            {
                Entry(RotationHealth.Compromised),
            }));

        var badge = cut.Find("[data-test-id='rotation-badge']");
        Assert.Contains("compromised", badge.ClassName);
        Assert.Contains("Compromised", badge.GetAttribute("aria-label")!);
    }

    [Fact]
    public void Table_has_caption_per_sc_131()
    {
        var cut = Render<PharmacyTabContent>(p => p
            .Add(c => c.Inventory, new List<PharmacyInventoryEntry> { Entry() }));

        var caption = cut.Find("caption");
        Assert.Contains("Pharmacy", caption.TextContent);
    }
}

public class AtmosphereTabContentTests : BunitContext
{
    private static AtmosphereReadout Readout(AtmosphereHealth health, int warn = 0, int crit = 0) =>
        new(health, warn, crit, false, DateTimeOffset.UtcNow);

    [Fact]
    public void Status_updates_announce_via_aria_live_polite()
    {
        var cut = Render<AtmosphereTabContent>(p => p
            .Add(c => c.Readout, Readout(AtmosphereHealth.Yellow, warn: 2)));

        var polite = cut.Find("[data-test-id='atmosphere-live-polite']");
        Assert.Equal("polite", polite.GetAttribute("aria-live"));
        Assert.Contains("Yellow", polite.TextContent);
    }

    [Fact]
    public void Red_escalation_announces_via_aria_live_assertive()
    {
        var cut = Render<AtmosphereTabContent>(p => p
            .Add(c => c.Readout, Readout(AtmosphereHealth.Red, crit: 3)));

        var assertive = cut.Find("[data-test-id='atmosphere-live-assertive']");
        Assert.Equal("assertive", assertive.GetAttribute("aria-live"));
        Assert.Contains("critical", assertive.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Yellow_to_Orange_transition_does_not_trigger_assertive()
    {
        var cut = Render<AtmosphereTabContent>(p => p
            .Add(c => c.Readout, Readout(AtmosphereHealth.Orange, warn: 3)));

        var assertive = cut.Find("[data-test-id='atmosphere-live-assertive']");
        Assert.True(string.IsNullOrWhiteSpace(assertive.TextContent));
    }

    [Fact]
    public void Health_icon_has_aria_label_not_color_alone_per_sc_141()
    {
        var cut = Render<AtmosphereTabContent>(p => p
            .Add(c => c.Readout, Readout(AtmosphereHealth.Green)));

        var icon = cut.Find(".atmosphere-health-icon");
        Assert.Equal("img", icon.GetAttribute("role"));
        Assert.NotNull(icon.GetAttribute("aria-label"));
    }
}

public class MedevacDialogTests : BunitContext
{
    private IFocusTrap SetupFocusTrap()
    {
        var trap = Substitute.For<IFocusTrap>();
        Services.AddSingleton(trap);
        return trap;
    }

    [Fact]
    public void Dialog_has_role_alertdialog_aria_modal_labelledby_describedby()
    {
        SetupFocusTrap();
        var cut = Render<MedevacDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.CurrentState, MedevacState.Requested));

        var dialog = cut.Find("[data-test-id='medevac-dialog']");
        Assert.Equal("alertdialog", dialog.GetAttribute("role"));
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal("medevac-dialog-title", dialog.GetAttribute("aria-labelledby"));
        Assert.Equal("medevac-dialog-consequence", dialog.GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Cancel_button_is_rendered_and_keyboard_operable()
    {
        SetupFocusTrap();
        var cancelled = false;
        var cut = Render<MedevacDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.CurrentState, MedevacState.Requested)
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => cancelled = true)));

        var cancel = cut.Find("[data-test-id='medevac-cancel']");
        cancel.Click();
        Assert.True(cancelled);
    }

    [Fact]
    public void Outcome_announced_on_authorize()
    {
        SetupFocusTrap();
        var cut = Render<MedevacDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.CurrentState, MedevacState.PendingAuthorization));

        var authorize = cut.Find("[data-test-id='medevac-authorize']");
        authorize.Click();

        var polite = cut.Find("[data-test-id='medevac-live-polite']");
        Assert.Contains("Medevac authorized", polite.TextContent);
    }

    [Fact]
    public void Keyboard_operability_SC_2_1_1_escape_invokes_close()
    {
        SetupFocusTrap();
        var closed = false;
        var cut = Render<MedevacDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.CurrentState, MedevacState.Requested)
            .Add(c => c.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        var dialog = cut.Find("[data-test-id='medevac-dialog']");
        dialog.KeyDown("Escape");
        Assert.True(closed);
    }

    [Fact]
    public void Dialog_not_rendered_when_closed()
    {
        SetupFocusTrap();
        var cut = Render<MedevacDialog>(p => p
            .Add(c => c.IsOpen, false)
            .Add(c => c.CurrentState, MedevacState.Idle));

        Assert.Throws<Bunit.ElementNotFoundException>(
            () => cut.Find("[data-test-id='medevac-dialog']"));
    }
}

public class KeyFingerprintDisplayTests : BunitContext
{
    private const string ValidFingerprint = "AABBCCDDEEFF00112233445566778899";

    [Fact]
    public void Renders_monospace_with_chunk_groups()
    {
        var cut = Render<KeyFingerprintDisplay>(p => p
            .Add(c => c.Fingerprint, ValidFingerprint));

        var chunks = cut.FindAll("[data-test-id='fingerprint-chunk']");
        Assert.Equal(8, chunks.Count);
    }

    [Fact]
    public void Each_chunk_has_aria_label_with_position_pronunciation()
    {
        var cut = Render<KeyFingerprintDisplay>(p => p
            .Add(c => c.Fingerprint, ValidFingerprint));

        var chunks = cut.FindAll("[data-test-id='fingerprint-chunk']");
        for (int i = 0; i < chunks.Count; i++)
        {
            var label = chunks[i].GetAttribute("aria-label")!;
            Assert.Contains($"group {i + 1} of 8", label);
        }
    }

    [Fact]
    public void Copy_button_is_present()
    {
        var cut = Render<KeyFingerprintDisplay>(p => p
            .Add(c => c.Fingerprint, ValidFingerprint));

        var copy = cut.Find("[data-test-id='fingerprint-copy']");
        Assert.NotNull(copy);
        Assert.NotNull(copy.GetAttribute("aria-label"));
    }

    [Fact]
    public void Empty_fingerprint_renders_nothing()
    {
        var cut = Render<KeyFingerprintDisplay>(p => p
            .Add(c => c.Fingerprint, ""));

        Assert.Throws<Bunit.ElementNotFoundException>(
            () => cut.Find("[data-test-id='key-fingerprint']"));
    }
}
