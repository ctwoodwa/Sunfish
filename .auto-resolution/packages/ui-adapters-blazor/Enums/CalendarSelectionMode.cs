namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Determines how the user may select dates on a <c>SunfishCalendar</c>.
/// Mirrors the <c>CalendarSelectionMode</c> spec surface.
/// </summary>
public enum CalendarSelectionMode
{
    /// <summary>A single date may be selected via <c>Value</c> (default).</summary>
    Single,

    /// <summary>Multiple independent dates may be selected via <c>SelectedDates</c>.</summary>
    Multiple,

    /// <summary>A contiguous range may be selected via <c>RangeStart</c> / <c>RangeEnd</c>.</summary>
    Range
}
