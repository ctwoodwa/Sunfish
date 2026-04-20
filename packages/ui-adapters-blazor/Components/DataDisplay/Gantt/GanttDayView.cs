namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Configures the Gantt timeline for day-level view where each slot is 1 hour.
/// Main header shows the day, secondary header shows hours.
/// </summary>
public class GanttDayView : GanttViewBase
{
    public GanttDayView() { SlotWidth = 40; }
    public override GanttView ViewType => GanttView.Day;
}
