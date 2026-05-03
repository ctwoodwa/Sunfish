using Sunfish.Foundation.Models;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Scheduler;

/// <summary>
/// Configures the Month view for <see cref="SunfishScheduler"/>.
/// Shows a calendar grid of the entire month.
/// </summary>
public class SchedulerMonthView : SchedulerViewBase
{
    /// <summary>First day of the week. Default is Sunday.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

    /// <inheritdoc />
    public override SchedulerView ViewType => SchedulerView.Month;
}
