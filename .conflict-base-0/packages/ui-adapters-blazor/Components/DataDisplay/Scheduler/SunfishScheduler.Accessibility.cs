using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Models;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Accessibility helpers for <see cref="SunfishScheduler"/> covering the rich
/// (DataDisplay-namespace) surface. Companion to the razor partial — defines:
/// - WCAG 2.1.1 Keyboard / 4.1.2 Name, Role, Value: keyboard activation for
///   day cells, time slots, agenda items, and appointment blocks (Enter / Space).
/// - WCAG 2.5.7 Dragging Movements: keyboard alternatives for the otherwise
///   mouse-only drag-to-create / drag-to-reschedule / drag-resize flows
///   (Enter on a focused slot opens the editor pre-populated with that slot;
///   Enter on a focused appointment opens the editor for move/resize edits).
/// - WCAG 2.1.2 No Keyboard Trap on the popup edit dialog: Escape dismisses.
/// - WCAG 4.1.3 Status Messages: a polite ARIA live region for view-change,
///   navigation, create / update / delete, and selection announcements.
/// </summary>
public partial class SunfishScheduler
{
    /// <summary>
    /// Backing field for the polite live region rendered at the top of the
    /// scheduler root. Updated via <see cref="Announce(string)"/>.
    /// </summary>
    internal string _liveAnnouncement = string.Empty;

    /// <summary>
    /// Pushes a message into the scheduler's polite ARIA live region. Pass an
    /// empty string to clear. The empty-then-set pattern lets assistive tech
    /// re-read identical messages on consecutive state changes.
    /// </summary>
    /// <param name="message">User-facing text to announce. Should be short and complete.</param>
    internal void Announce(string message)
    {
        _liveAnnouncement = string.IsNullOrEmpty(message) ? string.Empty : message;
        StateHasChanged();
    }

    // ── Activation keyboard handlers (WCAG 2.1.1, 4.1.2) ────────────────

    /// <summary>
    /// Keyboard equivalent of clicking a day cell in the month view. Enter or
    /// Space invokes <see cref="OnDateClick"/> with the cell's date.
    /// </summary>
    internal async Task HandleDayCellKeyDown(DateOnly date, KeyboardEventArgs e)
    {
        if (e is null) return;
        if (e.Key != "Enter" && e.Key != " " && e.Key != "Spacebar") return;
        await OnDateClick.InvokeAsync(date.ToDateTime(TimeOnly.MinValue));
    }

    /// <summary>
    /// Keyboard equivalent of clicking a time slot. Enter or Space on a focused
    /// slot raises the create flow — provides the WCAG 2.5.7 alternative to
    /// drag-to-create. Pre-populates a one-hour appointment starting at the slot.
    /// </summary>
    internal async Task HandleSlotKeyDown(DateOnly date, int hour, KeyboardEventArgs e)
    {
        if (!Editable) return;
        if (e is null) return;
        if (e.Key != "Enter" && e.Key != " " && e.Key != "Spacebar") return;

        var newAppt = new SchedulerAppointment
        {
            Title = "New Appointment",
            Start = date.ToDateTime(new TimeOnly(hour, 0)),
            End = date.ToDateTime(new TimeOnly(Math.Min(23, hour + 1), 0))
        };
        Announce($"New appointment requested at {date:MMM d}, {hour:D2}:00.");
        await OnAppointmentCreate.InvokeAsync(newAppt);
    }

    /// <summary>
    /// Keyboard equivalent of click / double-click on an appointment block.
    /// Enter or Space invokes <see cref="OnAppointmentClick"/>; when Editable
    /// the block also opens the inline / popup editor (mirrors the existing
    /// double-click behaviour, providing a keyboard route to edit / move /
    /// resize — WCAG 2.5.7 alternative for drag-to-reschedule and drag-resize).
    /// </summary>
    internal async Task HandleAppointmentKeyDown(SchedulerAppointment appt, KeyboardEventArgs e)
    {
        if (e is null) return;
        if (e.Key != "Enter" && e.Key != " " && e.Key != "Spacebar") return;

        await OnAppointmentClick.InvokeAsync(appt);
        if (Editable)
        {
            HandleAppointmentDoubleClick(appt);
        }
    }

    // ── Edit dialog Escape (WCAG 2.1.1, 2.1.2) ──────────────────────────

    /// <summary>
    /// Allows keyboard users to dismiss the popup / inline edit form via the
    /// Escape key, matching the click-on-backdrop behaviour available to mouse
    /// users. Without this handler, keyboard-only users could become trapped
    /// inside the modal (no visible Cancel focus, no escape route).
    /// </summary>
    internal async Task HandleEditDialogKeyDown(KeyboardEventArgs e)
    {
        if (e is null) return;
        if (e.Key != "Escape" && e.Key != "Esc") return;
        await CancelEdit();
    }

    // ── Navigation announcements (WCAG 4.1.3) ───────────────────────────

    /// <summary>
    /// Announces the current header title after a navigation. Called from the
    /// existing <c>NavigatePrevious</c> / <c>NavigateNext</c> wrappers.
    /// </summary>
    internal void AnnounceNavigation()
    {
        Announce($"Showing {GetHeaderTitle()}.");
    }

    /// <summary>
    /// Announces the new active view after a view switch.
    /// </summary>
    internal void AnnounceViewChange(SchedulerView view)
    {
        Announce($"View changed to {view}.");
    }

    // ── Helpers for icon button labels (WCAG 4.1.2) ─────────────────────

    /// <summary>Stable accessible name for the "previous" navigation button.</summary>
    internal string GetPreviousNavLabel() => View switch
    {
        SchedulerView.Day => "Previous day",
        SchedulerView.Week => "Previous week",
        SchedulerView.MultiDay => "Previous period",
        SchedulerView.Month => "Previous month",
        SchedulerView.Timeline => "Previous day",
        SchedulerView.Agenda => "Previous period",
        _ => "Previous",
    };

    /// <summary>Stable accessible name for the "next" navigation button.</summary>
    internal string GetNextNavLabel() => View switch
    {
        SchedulerView.Day => "Next day",
        SchedulerView.Week => "Next week",
        SchedulerView.MultiDay => "Next period",
        SchedulerView.Month => "Next month",
        SchedulerView.Timeline => "Next day",
        SchedulerView.Agenda => "Next period",
        _ => "Next",
    };

    /// <summary>Stable accessible name for an appointment block.</summary>
    internal string GetAppointmentAriaLabel(SchedulerAppointment a)
    {
        if (a.IsAllDay)
            return $"{a.Title}, all day {a.Start:MMM d}.";
        return $"{a.Title}, {a.Start:MMM d HH:mm} to {a.End:HH:mm}.";
    }

    /// <summary>Stable accessible name for a day cell in the month view.</summary>
    internal string GetDayCellAriaLabel(DateOnly date) =>
        $"{date.ToDateTime(TimeOnly.MinValue):dddd, MMMM d, yyyy}";

    /// <summary>Stable accessible name for a time-grid slot.</summary>
    internal string GetSlotAriaLabel(DateOnly date, int hour) =>
        $"{date.ToDateTime(TimeOnly.MinValue):dddd, MMMM d}, {hour:D2}:00";
}
