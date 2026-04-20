using Sunfish.Foundation.Models;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Scheduler;

/// <summary>
/// Configures the Week view for <see cref="SunfishScheduler"/>.
/// Shows 7 day columns with hourly time slots.
/// </summary>
public class SchedulerWeekView : SchedulerViewBase
{
    /// <summary>The start time of the visible day range. Default is 08:00.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public TimeSpan StartTime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>The end time of the visible day range. Default is 18:00.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public TimeSpan EndTime { get; set; } = TimeSpan.FromHours(18);

    /// <summary>Duration of each time slot. Default is 60 minutes.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public TimeSpan SlotDuration { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>First day of the week. Default is Sunday.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

    /// <inheritdoc />
    public override SchedulerView ViewType => SchedulerView.Week;
}
