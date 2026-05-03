using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Scheduling;

/// <summary>
/// Accessibility helpers for the MVP <see cref="SunfishScheduler{TEvent}"/>:
/// - WCAG 4.1.3 Status Messages: a polite ARIA live region (<see cref="_liveAnnouncement"/>)
///   updated by <see cref="Announce(string)"/> for navigation and view-change events.
/// - WCAG 4.1.2 Name, Role, Value: stable accessible labels for the prev/next nav buttons
///   that change with the active view (e.g. "Previous week" vs "Previous month").
///
/// The drag-to-create / drag-to-resize flows are not implemented in the MVP, so WCAG 2.5.7
/// (Dragging Movements) is not in scope here.
/// </summary>
public partial class SunfishScheduler<TEvent>
{
    /// <summary>
    /// Backing field for the polite live region. Updated via <see cref="Announce(string)"/>;
    /// read by assistive technology.
    /// </summary>
    internal string _liveAnnouncement = string.Empty;

    /// <summary>
    /// Pushes a message into the scheduler's polite ARIA live region. Pass an empty string
    /// to clear. The empty-then-set pattern lets AT re-read identical messages on
    /// consecutive state changes.
    /// </summary>
    /// <param name="message">User-facing text to announce. Should be short and complete.</param>
    internal void Announce(string message)
    {
        _liveAnnouncement = string.IsNullOrEmpty(message) ? string.Empty : message;
        StateHasChanged();
    }

    /// <summary>Stable accessible name for the "previous" navigation button.</summary>
    internal string GetPreviousNavLabel() => View switch
    {
        SchedulerView.Day      => "Previous day",
        SchedulerView.Week     => "Previous week",
        SchedulerView.WorkWeek => "Previous work week",
        SchedulerView.Month    => "Previous month",
        SchedulerView.Agenda   => "Previous period",
        _                      => "Previous",
    };

    /// <summary>Stable accessible name for the "next" navigation button.</summary>
    internal string GetNextNavLabel() => View switch
    {
        SchedulerView.Day      => "Next day",
        SchedulerView.Week     => "Next week",
        SchedulerView.WorkWeek => "Next work week",
        SchedulerView.Month    => "Next month",
        SchedulerView.Agenda   => "Next period",
        _                      => "Next",
    };
}
