namespace Sunfish.Foundation.Models;

public class CalendarCellRenderEventArgs
{
    public DateTime Date { get; set; }
    public string? CssClass { get; set; }
    public bool IsDisabled { get; set; }
}
