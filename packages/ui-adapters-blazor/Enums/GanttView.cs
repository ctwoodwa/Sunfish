namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Timeline granularity for <c>Sunfish.UIAdapters.Blazor.Components.Scheduling.SunfishGantt&lt;TItem&gt;</c>.
/// Controls the zoom level of the right-hand timeline columns.
/// </summary>
/// <remarks>
/// This is the canonical MVP enum used by the Scheduling-namespace <c>SunfishGantt</c>.
/// A separate <c>Sunfish.UIAdapters.Blazor.Components.DataDisplay.GanttView</c> exists for
/// the legacy DataDisplay <c>SunfishGantt</c> surface; the two enums are independent and
/// live in different namespaces by design (ADR 0022 Tier 3 scheduling family).
/// </remarks>
public enum GanttView
{
    /// <summary>One column per hour within each day.</summary>
    Day,

    /// <summary>One column per day across a 7-day span.</summary>
    Week,

    /// <summary>One column per day across the calendar month.</summary>
    Month,

    /// <summary>One column per month across the calendar year.</summary>
    Year,
}
