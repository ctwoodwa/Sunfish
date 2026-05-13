using System;
using System.Collections.Generic;
using Bunit;
using Sunfish.Foundation.Tactical;
using Xunit;

namespace Sunfish.Blocks.Tactical.Tests;

/// <summary>
/// W#52 Phase 3a — SonarRoomPanel bUnit tests per ADR 0081 §7.3 + hand-off
/// acceptance gate. WCAG/a11y subagent review MANDATORY before merge.
/// </summary>
public class SonarRoomPanelTests : BunitContext
{
    private static TacticalAlert MakeAlert(
        string alertId = "rule:001",
        AlertSeverity severity = AlertSeverity.Medium,
        AlertRoutingPolicy routing = AlertRoutingPolicy.InformationalSonar) =>
        new(
            AlertId: alertId,
            TenantId: new Sunfish.Foundation.Assets.Common.TenantId("00000000-0000-0000-0000-000000000001"),
            RuleName: "sunfish.test.rule",
            Severity: severity,
            RoutingPolicy: routing,
            Title: "Test alert",
            Summary: string.Empty,
            DetectedAt: DateTimeOffset.UtcNow,
            Status: AlertStatus.Active,
            RequiresAcknowledgement: false,
            RunbookStepIds: Array.Empty<string>(),
            AcknowledgedBy: null,
            AcknowledgedAt: null);

    [Fact]
    public void SonarRoom_has_section_role_region_with_labelledby()
    {
        var cut = Render<SonarRoomPanel>(p => p
            .Add(c => c.SignalRatePerMinute, 0));

        var section = cut.Find("section[role='region']");
        var headingId = section.GetAttribute("aria-labelledby");
        Assert.Equal("sonar-room-heading", headingId);

        var heading = cut.Find($"#{headingId}");
        Assert.Equal("Sonar Room", heading.TextContent.Trim());

        Assert.Equal("sonar-room", section.GetAttribute("id"));
        Assert.Equal("-1", section.GetAttribute("tabindex"));
    }

    [Fact]
    public void SonarRoom_gauge_has_role_meter_with_aria_attributes()
    {
        var cut = Render<SonarRoomPanel>(p => p
            .Add(c => c.SignalRatePerMinute, 25)
            .Add(c => c.MaxAlertsPerMinutePerRule, 60));

        var gauge = cut.Find("[role='meter']");
        Assert.Equal("0", gauge.GetAttribute("aria-valuemin"));
        Assert.Equal("60", gauge.GetAttribute("aria-valuemax"));
        Assert.Equal("25", gauge.GetAttribute("aria-valuenow"));
        Assert.Contains("25 signals per minute", gauge.GetAttribute("aria-valuetext"));

        // Rate number visible as text; numeric span is aria-hidden.
        var hiddenSpan = cut.Find("[role='meter'] [aria-hidden='true']");
        Assert.Equal("25", hiddenSpan.TextContent.Trim());
    }

    [Fact]
    public void SonarRoom_polite_live_region_present()
    {
        var cut = Render<SonarRoomPanel>(p => p
            .Add(c => c.SignalRatePerMinute, 0));

        var polite = cut.Find("[aria-live='polite'][aria-atomic='true']");
        Assert.NotNull(polite);
    }
}
