using Sunfish.Foundation.Models;

namespace Sunfish.Components.Blazor.Components.DataDisplay.Scheduler;

/// <summary>
/// Configures the Multi-Day view for <see cref="SunfishScheduler"/>.
/// Shows a configurable number of day columns with hourly time slots.
/// </summary>
public class SchedulerMultiDayView : SchedulerViewBase
{
    /// <summary>Number of days to display. Default is 3.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public int NumberOfDays { get; set; } = 3;

    /// <summary>The start time of the visible day range. Default is 08:00.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public TimeSpan StartTime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>The end time of the visible day range. Default is 18:00.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public TimeSpan EndTime { get; set; } = TimeSpan.FromHours(18);

    /// <summary>Duration of each time slot. Default is 60 minutes.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public TimeSpan SlotDuration { get; set; } = TimeSpan.FromMinutes(60);

    /// <inheritdoc />
    public override SchedulerView ViewType => SchedulerView.MultiDay;
}
