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
        // aria-labelledby → <h2> title; aria-describedby → consequence div.
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
        // Confirm is aria-disabled + native disabled at open so it cannot receive intent-first focus.
        // DOM focus via ElementReference.FocusAsync is not verifiable in bUnit (JS interop stub);
        // this test verifies the structural preconditions: Cancel is present + focusable;
        // Confirm has aria-disabled="true" and native disabled (non-focusable pattern).
        RegisterServices();
        var cut = Render<EmergencyStandingOrderDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.ConsequenceText, "Trigger alert protocol."));

        var confirm = cut.Find("[data-test-id='eso-confirm-btn']");
        var cancel = cut.Find("[data-test-id='eso-cancel-btn']");

        // Confirm MUST be aria-disabled at open (prevents premature activation).
        Assert.Equal("true", confirm.GetAttribute("aria-disabled"));
        // Confirm MUST also be natively disabled (enforces deliberation gate at browser level).
        Assert.NotNull(confirm.GetAttribute("disabled"));
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
        // timer has not yet elapsed.
        // Security council amendment: native disabled is ALSO required to enforce the
        // deliberation gate at browser level (prevents keyboard bypass per SC 3.3.4).
        RegisterServices();
        var cut = Render<EmergencyStandingOrderDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.ConsequenceText, "Deploy emergency protocol now."));

        var confirm = cut.Find("[data-test-id='eso-confirm-btn']");
        Assert.Equal("true", confirm.GetAttribute("aria-disabled"));
        // Native disabled enforces deliberation gate (security council amendment).
        Assert.NotNull(confirm.GetAttribute("disabled"));
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
        // Native disabled MUST also be cleared at t=2000ms.
        Assert.Null(confirm.GetAttribute("disabled"));

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
        // substitution; if tokens are present, the component enters fail-close mode.
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

    [Fact]
    public void Dialog_fail_close_on_template_tokens_in_consequence_text()
    {
        // Security council (Blocking): template token validation at component boundary.
        // If consequence text contains unresolved tokens (e.g., "{{zone}}"), Confirm
        // MUST be permanently disabled for that open cycle (fail-close pattern).
        // The error is surfaced in the consequence area; the dialog can still be cancelled.
        RegisterServices();
        const string unsubstituted = "Initiate evacuation of {{zone}} immediately.";
        var cut = Render<EmergencyStandingOrderDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.ConsequenceText, unsubstituted));

        var confirm = cut.Find("[data-test-id='eso-confirm-btn']");
        // Confirm MUST remain disabled (aria-disabled + native disabled).
        Assert.Equal("true", confirm.GetAttribute("aria-disabled"));
        Assert.NotNull(confirm.GetAttribute("disabled"));

        // Error indicator MUST be present in consequence area.
        var consequence = cut.Find("[data-test-id='eso-consequence']");
        Assert.NotEmpty(consequence.TextContent.Trim());
        // Raw token text MUST NOT be the primary content (error message shown instead).
        Assert.Contains("unresolved template tokens", consequence.TextContent);
    }
}
