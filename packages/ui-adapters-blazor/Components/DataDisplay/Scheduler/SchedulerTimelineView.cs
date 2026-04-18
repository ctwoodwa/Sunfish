using Sunfish.Foundation.Models;

namespace Sunfish.Components.Blazor.Components.DataDisplay.Scheduler;

/// <summary>
/// Configures the Timeline view for <see cref="SunfishScheduler"/>.
/// Renders a horizontal layout where time flows left-to-right.
/// Each row is a day. Time slots are columns.
/// Appointments render as horizontal bars spanning their time range.
/// </summary>
public class SchedulerTimelineView : SchedulerViewBase
{
    /// <summary>Duration of each time slot column. Default is 30 minutes.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public TimeSpan SlotDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>The start time of the visible range. Default is 08:00.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public TimeSpan StartTime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>The end time of the visible range. Default is 18:00.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public TimeSpan EndTime { get; set; } = TimeSpan.FromHours(18);

    /// <summary>Number of days to show as rows. Default is 1.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public int NumberOfDays { get; set; } = 1;

    /// <inheritdoc />
    public override SchedulerView ViewType => SchedulerView.Timeline;
}
