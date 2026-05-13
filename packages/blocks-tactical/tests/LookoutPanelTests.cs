using System;
using System.Collections.Generic;
using Bunit;
using Sunfish.Foundation.Tactical;
using Xunit;

namespace Sunfish.Blocks.Tactical.Tests;

/// <summary>
/// W#52 Phase 3a — LookoutPanel bUnit tests per ADR 0081 §7.3 + hand-off
/// acceptance gate. WCAG/a11y subagent review MANDATORY before merge.
///
/// SunfishA11yAssertions patterns verified inline:
///   ReducedMotionDefaultsToPaused → default _isPaused=true checked via aria-pressed
///   AriaDisabledButtonRemainsInTabOrder → aria-disabled present, no native disabled
///   AriaDisabledSuppressesActivation → OnAcknowledge not invoked when aria-disabled
///   AssertiveRegionAnnouncesAdditionsOnly → aria-relevant="additions" on list
///   SubRoomsKeyboardReachable → id="lookout" + tabindex="-1" on section
/// </summary>
public class LookoutPanelTests : BunitContext
{
    private static TacticalAlert MakeAlert(
        string alertId = "rule:001",
        AlertStatus status = AlertStatus.Active,
        AlertSeverity severity = AlertSeverity.High) =>
        new(
            AlertId: alertId,
            TenantId: new Sunfish.Foundation.Assets.Common.TenantId("00000000-0000-0000-0000-000000000001"),
            RuleName: "sunfish.test.rule",
            Severity: severity,
            RoutingPolicy: AlertRoutingPolicy.HighPriorityLookout,
            Title: "Test lookout alert",
            Summary: string.Empty,
            DetectedAt: DateTimeOffset.UtcNow,
            Status: status,
            RequiresAcknowledgement: true,
            RunbookStepIds: Array.Empty<string>(),
            AcknowledgedBy: null,
            AcknowledgedAt: null);

    [Fact]
    public void Lookout_live_region_has_assertive_atomic_false_relevant_additions()
    {
        var cut = Render<LookoutPanel>(p => p
            .Add(c => c.CanAcknowledgeAlerts, true));

        var list = cut.Find("[aria-live='assertive']");
        Assert.Equal("false", list.GetAttribute("aria-atomic"));
        Assert.Equal("additions", list.GetAttribute("aria-relevant"));
        Assert.Equal("High-priority tactical alerts", list.GetAttribute("aria-label"));
    }

    [Fact]
    public void Lookout_pause_button_has_aria_pressed()
    {
        var cut = Render<LookoutPanel>(p => p
            .Add(c => c.CanAcknowledgeAlerts, true));

        var btn = cut.Find("[data-test-id='lookout-pause-btn']");
        var pressed = btn.GetAttribute("aria-pressed");
        Assert.True(pressed == "true" || pressed == "false");
        Assert.Equal("Pause Lookout ticker", btn.TextContent.Trim());
    }

    [Fact]
    public void Lookout_default_paused_under_reduced_motion()
    {
        // SunfishA11yAssertions.ReducedMotionDefaultsToPaused:
        // Component MUST default to paused state so no animation runs
        // under prefers-reduced-motion (SC 2.2.2). Default _isPaused = true.
        var cut = Render<LookoutPanel>(p => p
            .Add(c => c.CanAcknowledgeAlerts, true));

        var btn = cut.Find("[data-test-id='lookout-pause-btn']");
        Assert.Equal("true", btn.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Lookout_acknowledge_button_uses_aria_disabled_not_native_disabled()
    {
        // SunfishA11yAssertions.AriaDisabledButtonRemainsInTabOrder:
        // Button MUST use aria-disabled="true" NOT native disabled attribute,
        // so it stays in tab order and remains keyboard-reachable.
        var alerts = new List<TacticalAlert> { MakeAlert() };
        var cut = Render<LookoutPanel>(p => p
            .Add(c => c.LookoutAlerts, alerts)
            .Add(c => c.CanAcknowledgeAlerts, false));

        cut.Find("[data-test-id='lookout-pause-btn']").Click();

        var ackBtn = cut.Find("[data-test-id='ack-btn-rule:001']");
        Assert.Equal("true", ackBtn.GetAttribute("aria-disabled"));
        Assert.Null(ackBtn.GetAttribute("disabled"));
    }

    [Fact]
    public void Lookout_acknowledge_button_suppresses_click_when_aria_disabled()
    {
        // SunfishA11yAssertions.AriaDisabledSuppressesActivation:
        // Clicking the button when aria-disabled should NOT invoke OnAcknowledge.
        var invoked = false;
        var alerts = new List<TacticalAlert> { MakeAlert() };
        var cut = Render<LookoutPanel>(p => p
            .Add(c => c.LookoutAlerts, alerts)
            .Add(c => c.CanAcknowledgeAlerts, false)
            .Add(c => c.OnAcknowledge, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string>(
                this, _ => { invoked = true; })));

        cut.Find("[data-test-id='lookout-pause-btn']").Click();

        var ackBtn = cut.Find("[data-test-id='ack-btn-rule:001']");
        ackBtn.Click();

        Assert.False(invoked);
    }

    [Fact]
    public void Lookout_assertive_region_announces_additions_only()
    {
        // SunfishA11yAssertions.AssertiveRegionAnnouncesAdditionsOnly:
        // The assertive list MUST have aria-relevant="additions" (not "all")
        // so only new items announce, not removals or text changes.
        var cut = Render<LookoutPanel>(p => p
            .Add(c => c.CanAcknowledgeAlerts, true));

        var region = cut.Find("[aria-live='assertive']");
        Assert.Equal("additions", region.GetAttribute("aria-relevant"));
        Assert.Equal("false", region.GetAttribute("aria-atomic"));
    }

    [Fact]
    public void MainContent_skip_links_keyboard_reachable()
    {
        // SunfishA11yAssertions.SubRoomsKeyboardReachable:
        // Lookout section MUST have id="lookout" and tabindex="-1"
        // so skip links (<a href="#lookout">) can target it via keyboard.
        var cut = Render<LookoutPanel>(p => p
            .Add(c => c.CanAcknowledgeAlerts, true));

        var section = cut.Find("section");
        Assert.Equal("lookout", section.GetAttribute("id"));
        Assert.Equal("-1", section.GetAttribute("tabindex"));
    }
}
