using System;
using System.Collections.Generic;
using Bunit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Tactical;
using Xunit;

namespace Sunfish.Blocks.Tactical.Tests;

/// <summary>
/// W#52 Phase 3b — FireControlPanel bUnit tests per ADR 0081 §7.3 + hand-off
/// acceptance gate. WCAG/a11y + security-engineering council review MANDATORY before merge.
///
/// SunfishA11yAssertions patterns verified inline:
///   IncidentStateTransitionAnnounced → polite region announces status change
///   SubRoomsKeyboardReachable → id="fire-control" + tabindex="-1"
/// </summary>
public class FireControlPanelTests : BunitContext
{
    private static readonly TenantId TestTenant =
        new("00000000-0000-0000-0000-000000000001");
    private static readonly ActorId TestActor =
        new("dGVzdC11c2VyLTAx");

    private static IncidentRecord MakeIncident(
        string incidentId = "incident:001",
        IncidentStatus status = IncidentStatus.Open,
        IReadOnlyList<string>? runbookStepIds = null) =>
        new(
            IncidentId: incidentId,
            TenantId: TestTenant,
            Title: "Test incident",
            RootAlertId: "alert:001",
            Status: status,
            OpenedAt: DateTimeOffset.UtcNow,
            LastUpdatedAt: DateTimeOffset.UtcNow,
            ClosedAt: null,
            OpenedBy: TestActor,
            ClosedBy: null,
            ResolutionNote: null,
            RunbookStepIds: runbookStepIds ?? Array.Empty<string>(),
            LinkedAlertIds: Array.Empty<string>());

    [Fact]
    public void FireControl_has_section_role_region_with_labelledby()
    {
        // SunfishA11yAssertions.SubRoomsKeyboardReachable:
        // Section MUST have id="fire-control" + tabindex="-1" for skip-link targeting.
        var cut = Render<FireControlPanel>(p => p
            .Add(c => c.CanIssueOrders, true));

        var section = cut.Find("section");
        Assert.Equal("fire-control", section.GetAttribute("id"));
        Assert.Equal("-1", section.GetAttribute("tabindex"));
        Assert.Equal("region", section.GetAttribute("role"));
        Assert.Equal("fire-control-heading", section.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void FireControl_incidents_list_is_ol_with_aria_label()
    {
        var incidents = new List<IncidentRecord> { MakeIncident() };
        var cut = Render<FireControlPanel>(p => p
            .Add(c => c.ActiveIncidents, incidents)
            .Add(c => c.CanIssueOrders, true));

        var list = cut.Find("[data-test-id='fire-control-incidents']");
        Assert.Equal("ol", list.TagName.ToLowerInvariant());
        Assert.Equal("Active incidents", list.GetAttribute("aria-label"));
    }

    [Fact]
    public void FireControl_runbook_steps_use_aria_labelledby_not_aria_label()
    {
        // SunfishA11yAssertions — runbook steps MUST use aria-labelledby referencing
        // both the step-number span and the step-title span. aria-label is NOT permitted
        // on <li> elements because it suppresses visible text from the accessibility tree.
        // Use a colon-free ID so generated step IDs don't trip CSS selector parsing.
        var incidents = new List<IncidentRecord>
        {
            MakeIncident(incidentId: "inc001", runbookStepIds: new[] { "runbook.step.reboot", "runbook.step.notify" })
        };
        var cut = Render<FireControlPanel>(p => p
            .Add(c => c.ActiveIncidents, incidents)
            .Add(c => c.CanIssueOrders, true));

        var steps = cut.FindAll("[data-test-id^='runbook-step-']");
        Assert.True(steps.Count > 0);

        foreach (var step in steps)
        {
            // MUST have aria-labelledby (not aria-label).
            var labelledBy = step.GetAttribute("aria-labelledby");
            Assert.NotNull(labelledBy);
            Assert.Null(step.GetAttribute("aria-label"));

            // aria-labelledby MUST reference two IDs (step number + step title).
            var ids = labelledBy!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, ids.Length);

            // Both referenced spans MUST exist in the DOM.
            Assert.NotNull(cut.Find($"#{ids[0]}"));
            Assert.NotNull(cut.Find($"#{ids[1]}"));
        }
    }

    [Fact]
    public void FireControl_incident_transition_announced_in_polite_region()
    {
        // SunfishA11yAssertions.IncidentStateTransitionAnnounced:
        // When an incident's Status changes, the polite region MUST be updated
        // with a descriptive announcement. aria-live="polite" aria-atomic="true"
        // ensures screen readers announce the transition without interrupting
        // current narration.
        var incident = MakeIncident(status: IncidentStatus.Open);
        var cut = Render<FireControlPanel>(p => p
            .Add(c => c.ActiveIncidents, new List<IncidentRecord> { incident })
            .Add(c => c.CanIssueOrders, true));

        var announceRegion = cut.Find("[data-test-id='fire-control-announce']");
        Assert.Equal("polite", announceRegion.GetAttribute("aria-live"));
        Assert.Equal("true", announceRegion.GetAttribute("aria-atomic"));

        // Simulate status transition: Open → Resolved.
        var resolved = MakeIncident(status: IncidentStatus.Resolved);
        cut.Render(p => p
            .Add(c => c.ActiveIncidents, new List<IncidentRecord> { resolved })
            .Add(c => c.CanIssueOrders, true));

        announceRegion = cut.Find("[data-test-id='fire-control-announce']");
        Assert.Contains("Resolved", announceRegion.TextContent);
    }
}
