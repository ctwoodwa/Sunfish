namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Configures the Gantt timeline for month-level view where each slot is 1 week.
/// Main header shows the month, secondary header shows week numbers.
/// </summary>
public class GanttMonthView : GanttViewBase
{
    public GanttMonthView() { SlotWidth = 100; }
    public override GanttView ViewType => GanttView.Month;
}
