namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Configures the Gantt timeline for week-level view where each slot is 1 day.
/// Main header shows the week range, secondary header shows day names.
/// </summary>
public class GanttWeekView : GanttViewBase
{
    public GanttWeekView() { SlotWidth = 100; }
    public override GanttView ViewType => GanttView.Week;
}
