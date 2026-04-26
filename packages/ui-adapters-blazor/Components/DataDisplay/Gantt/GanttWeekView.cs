namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Configures the Gantt timeline for week-level view where each slot is 1 day.
/// Main header shows the week range, secondary header shows day names.
/// </summary>
public class GanttWeekView : GanttViewBase
{
    /// <summary>Initializes the view with a default slot width of 100 pixels per day.</summary>
    public GanttWeekView() { SlotWidth = 100; }

    /// <inheritdoc />
    public override GanttView ViewType => GanttView.Week;
}
