namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Configures the Gantt timeline for month-level view where each slot is 1 week.
/// Main header shows the month, secondary header shows week numbers.
/// </summary>
public class GanttMonthView : GanttViewBase
{
    /// <summary>Initializes the view with a default slot width of 100 pixels per week.</summary>
    public GanttMonthView() { SlotWidth = 100; }

    /// <inheritdoc />
    public override GanttView ViewType => GanttView.Month;
}
