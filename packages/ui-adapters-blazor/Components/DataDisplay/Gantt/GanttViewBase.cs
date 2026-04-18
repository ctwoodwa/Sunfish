using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Abstract base class for Gantt view configuration components (Day, Week, Month, Year).
/// These are configuration-only components that produce no rendered output.
/// </summary>
public abstract class GanttViewBase : ComponentBase, IDisposable
{
    [CascadingParameter] private IGanttViewHost? ViewHost { get; set; }

    /// <summary>The pixel width of one timeline slot in this view.</summary>
    [Parameter] public double SlotWidth { get; set; }

    /// <summary>Optional explicit start of the visible range. If null, auto-calculated from data.</summary>
    [Parameter] public DateTime? RangeStart { get; set; }

    /// <summary>Optional explicit end of the visible range. If null, auto-calculated from data.</summary>
    [Parameter] public DateTime? RangeEnd { get; set; }

    /// <summary>Custom template for the major (main) header cells. Receives the slot start DateTime as context.</summary>
    [Parameter] public RenderFragment<DateTime>? MainHeaderTemplate { get; set; }

    /// <summary>Custom template for the minor (sub) header cells. Receives the slot start DateTime as context.</summary>
    [Parameter] public RenderFragment<DateTime>? SubHeaderTemplate { get; set; }

    /// <summary>Format string for main header labels (e.g. "MMMM yyyy"). Overridden by MainHeaderTemplate.</summary>
    [Parameter] public string? MainHeaderDateFormat { get; set; }

    /// <summary>Format string for sub header labels (e.g. "ddd d"). Overridden by SubHeaderTemplate.</summary>
    [Parameter] public string? SubHeaderDateFormat { get; set; }

    /// <summary>The view type this component represents.</summary>
    public abstract GanttView ViewType { get; }

    protected override void OnInitialized()
    {
        ViewHost?.RegisterView(this);
    }

    /// <summary>No rendered output — configuration-only component.</summary>
    protected override void BuildRenderTree(RenderTreeBuilder builder) { }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) ViewHost?.UnregisterView(this);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
