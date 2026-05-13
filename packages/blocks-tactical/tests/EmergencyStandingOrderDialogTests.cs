using System;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UICore.Primitives;
using Xunit;

namespace Sunfish.Blocks.Tactical.Tests;

/// <summary>
/// W#52 Phase 3b — EmergencyStandingOrderDialog bUnit tests per ADR 0081 §7.6
/// + hand-off acceptance gate. WCAG/a11y + security-engineering council review
/// MANDATORY before merge.
///
/// SunfishA11yAssertions patterns verified inline:
///   AlertDialogHasRoleModalLabelDescribedBy → role="alertdialog" + aria-modal + labelledby + describedby
///   DeliberationPauseAnnouncesEnablement → Confirm enabled at 2000ms + "Confirm available" announcement
///   DialogOutcomeAnnouncedOnClose → polite outcome announcement on confirm/cancel
/// </summary>
public class EmergencyStandingOrderDialogTests : BunitContext
{
    private sealed class NoopFocusTrap : IFocusTrap
    {
        public ValueTask EnterAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private void RegisterServices()
    {
        Services.AddSingleton<IFocusTrap>(new NoopFocusTrap());
    }

    [Fact]
    public void Dialog_uses_role_alertdialog_with_modal_label_described_by()
    {
        // SunfishA11yAssertions.AlertDialogHasRoleModalLabelDescribedBy:
        // Dialog MUST use role="alertdialog" (not "dialog" — signals urgency for
        // security-critical destructive confirmation per SC 3.3.4).
        // aria-modal="true" communicates containment to virtual-buffer screen readers.
        // aria-labelledby → <h2> title; aria-describedby → consequence paragraph.
        RegisterServices();
        var cut = Render<EmergencyStandingOrderDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.ConsequenceText, "Lockdown Zone A immediately."));

        var dialog = cut.Find("[data-test-id='eso-dialog']");
        Assert.Equal("alertdialog", dialog.GetAttribute("role"));
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal("eso-dialog-title", dialog.GetAttribute("aria-labelledby"));
        Assert.Equal("eso-dialog-consequence", dialog.GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Dialog_initial_focus_is_cancel_not_confirm()
    {
        // SunfishA11yAssertions — initial focus MUST be the Cancel button (§7.6 safe default).
        // Confirm is intentionally aria-disabled at open so it cannot receive intent-first focus.
        // DOM focus via ElementReference.FocusAsync is not verifiable in bUnit (JS interop stub);
        // this test verifies the structural preconditions: Cancel is present + focusable;
        // Confirm has aria-disabled="true" (non-focusable-by-intent pattern).
        RegisterServices();
        var cut = Render<EmergencyStandingOrderDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.ConsequenceText, "Trigger alert protocol."));

        var confirm = cut.Find("[data-test-id='eso-confirm-btn']");
        var cancel = cut.Find("[data-test-id='eso-cancel-btn']");

        // Confirm MUST be aria-disabled at open (prevents premature activation).
        Assert.Equal("true", confirm.GetAttribute("aria-disabled"));
        // Cancel MUST NOT be aria-disabled (is the safe default focus target).
        Assert.True(cancel.GetAttribute("aria-disabled") is null or "false");
        // Cancel MUST have id="eso-cancel-btn" for FocusAsync reference.
        Assert.Equal("eso-cancel-btn", cancel.GetAttribute("id"));
    }

    [Fact]
    public void Dialog_confirm_aria_disabled_on_open()
    {
        // SunfishA11yAssertions.DeliberationPauseAnnouncesEnablement (initial state):
        // Confirm MUST be aria-disabled immediately on open. The 2000ms deliberation
        // timer has not yet elapsed. aria-disabled (NOT native disabled) keeps the
        // button in tab order per SC 2.1.1 while preventing premature activation.
        RegisterServices();
        var cut = Render<EmergencyStandingOrderDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.ConsequenceText, "Deploy emergency protocol now."));

        var confirm = cut.Find("[data-test-id='eso-confirm-btn']");
        Assert.Equal("true", confirm.GetAttribute("aria-disabled"));
        Assert.Null(confirm.GetAttribute("disabled"));
    }

    [Fact]
    public async Task Dialog_confirm_enabled_at_2000ms_with_announcement()
    {
        // SunfishA11yAssertions.DeliberationPauseAnnouncesEnablement:
        // SC 3.3.4 deliberation pause: Confirm MUST be enabled exactly at t=2000ms.
        // "Confirm available" MUST be injected into the polite deliberation-announce
        // region so screen readers notify the user without interrupting current narration.
        RegisterServices();
        var cut = Render<EmergencyStandingOrderDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.ConsequenceText, "Issue emergency standing order."));

        // Wait up to 5 seconds for the 2000ms timer to fire.
        await cut.WaitForStateAsync(
            () => cut.Find("[data-test-id='eso-confirm-btn']").GetAttribute("aria-disabled") == "false",
            timeout: TimeSpan.FromSeconds(5));

        var confirm = cut.Find("[data-test-id='eso-confirm-btn']");
        Assert.Equal("false", confirm.GetAttribute("aria-disabled"));

        var deliberation = cut.Find("[data-test-id='eso-deliberation-announce']");
        Assert.Contains("Confirm available", deliberation.TextContent);
    }

    [Fact]
    public void Dialog_outcome_announced_on_close()
    {
        // SunfishA11yAssertions.DialogOutcomeAnnouncedOnClose:
        // Clicking Cancel MUST populate the outcome live region with "Cancelled" before
        // the dialog closes. This ensures screen readers announce the outcome even when
        // focus moves back to the triggering element.
        RegisterServices();
        var cancelled = false;
        var cut = Render<EmergencyStandingOrderDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.ConsequenceText, "Trigger lockdown.")
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => { cancelled = true; })));

        cut.Find("[data-test-id='eso-cancel-btn']").Click();

        Assert.True(cancelled);
        // Outcome announce region MUST contain "Cancelled".
        var outcome = cut.Find("[data-test-id='eso-outcome-announce']");
        Assert.Contains("Cancelled", outcome.TextContent);
    }

    [Fact]
    public void Dialog_consequence_text_shows_post_substitution_not_tokens()
    {
        // SunfishA11yAssertions (Security — consequence text preview):
        // Consequence text MUST be post-substitution. Raw template tokens (e.g., "{{zone}}")
        // MUST NOT appear in the consequence paragraph. The caller is responsible for
        // substitution; the dialog only renders what it receives.
        // Test verifies: a fully-substituted string displays verbatim;
        // a string with tokens (unfired substitution) would be a caller bug — this test
        // documents the expected contract, not the enforcement (enforcement is caller-side).
        RegisterServices();
        const string substituted = "Initiate evacuation of Zone Alpha immediately. All personnel to muster stations.";
        var cut = Render<EmergencyStandingOrderDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.ConsequenceText, substituted));

        var consequence = cut.Find("[data-test-id='eso-consequence']");
        Assert.Contains("Zone Alpha", consequence.TextContent);
        // Template token patterns MUST NOT appear.
        Assert.DoesNotContain("{{", consequence.TextContent);
        Assert.DoesNotContain("}}", consequence.TextContent);
    }
}
