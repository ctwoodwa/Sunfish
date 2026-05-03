namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Configures the Gantt timeline for year-level view where each slot is 1 month.
/// Main header shows the year, secondary header shows month names.
/// </summary>
public class GanttYearView : GanttViewBase
{
    /// <summary>Initializes the view with a default slot width of 30 pixels per month.</summary>
    public GanttYearView() { SlotWidth = 30; }

    /// <inheritdoc />
    public override GanttView ViewType => GanttView.Year;
}
