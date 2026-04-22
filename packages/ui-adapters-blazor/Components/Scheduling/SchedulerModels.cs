namespace Sunfish.UIAdapters.Blazor.Components.Scheduling;

/// <summary>
/// A resource column definition for <c>SunfishScheduler&lt;TEvent&gt;</c> Day view.
/// Optional: when <c>Resources</c> is non-empty the Day view splits the hour grid
/// into one column per resource and places each event under the column whose
/// <see cref="Id"/> matches the event's <c>ResourceField</c> value.
/// </summary>
/// <remarks>
/// This MVP type is scoped to the Scheduling-namespace <c>SunfishScheduler</c>. A
/// distinct <c>Sunfish.Foundation.Models.SchedulerResource</c> exists for the legacy
/// DataDisplay surface and is not referenced here.
/// </remarks>
public sealed class SchedulerResource
{
    /// <summary>
    /// Unique identifier for this resource. Matched against the value returned by
    /// the event's <c>ResourceField</c> accessor to group events into their column.
    /// </summary>
    public object Id { get; set; } = default!;

    /// <summary>Display text for the resource column header.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional CSS color. When set, events that resolve to this resource get a matching
    /// accent strip and, if their own <c>ColorField</c> yields no value, use this as a fallback.
    /// </summary>
    public string? Color { get; set; }
}
