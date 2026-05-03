using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Scheduling;

/// <summary>
/// Cancellable event arguments for <c>SunfishScheduler&lt;TEvent&gt;</c> event-level
/// callbacks (<c>OnEventClick</c>, <c>OnEventCreate</c>, <c>OnEventUpdate</c>,
/// <c>OnEventDelete</c>).
/// </summary>
/// <typeparam name="TEvent">The calendar event item type bound to the scheduler.</typeparam>
public class SchedulerEventArgs<TEvent>
    where TEvent : class
{
    /// <summary>The event item the callback refers to.</summary>
    public TEvent Item { get; init; } = default!;

    /// <summary>
    /// <c>true</c> when the event is a freshly created item not yet appended to <c>Data</c>.
    /// Only meaningful for <c>OnEventCreate</c>.
    /// </summary>
    public bool IsNew { get; init; }

    /// <summary>
    /// Set to <c>true</c> inside a handler to prevent the default action
    /// (create, update, or delete).
    /// </summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Cancellable event arguments for the <c>SunfishScheduler&lt;TEvent&gt;</c>
/// <c>OnViewChange</c> callback. Fires when the user clicks a view-switch toolbar button.
/// </summary>
public class SchedulerViewChangeEventArgs
{
    /// <summary>The view that was active when the user requested the change.</summary>
    public SchedulerView FromView { get; init; }

    /// <summary>The view the scheduler is about to switch to.</summary>
    public SchedulerView ToView { get; init; }

    /// <summary>
    /// Set to <c>true</c> inside a handler to block the transition; the scheduler keeps
    /// <see cref="FromView"/> as its active view.
    /// </summary>
    public bool IsCancelled { get; set; }
}
