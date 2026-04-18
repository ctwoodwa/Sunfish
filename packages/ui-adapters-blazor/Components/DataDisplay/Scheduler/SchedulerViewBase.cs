using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Sunfish.Components.Blazor.Components.DataDisplay.Scheduler;

/// <summary>
/// Abstract base class for Scheduler view configuration components (Day, Week, Month).
/// These are configuration-only components that produce no rendered output.
/// They register with their parent <see cref="SunfishScheduler"/> via a cascading
/// <see cref="ISchedulerViewHost"/> parameter.
/// </summary>
public abstract class SchedulerViewBase : ComponentBase, IDisposable
{
    [CascadingParameter] private ISchedulerViewHost? ViewHost { get; set; }

    /// <summary>The view type this component represents.</summary>
    public abstract SchedulerView ViewType { get; }

    /// <summary>Display label for the toolbar button. Defaults to the view name (Day, Week, Month).</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>The effective label used in the toolbar button.</summary>
    public string EffectiveLabel => Label ?? ViewType.ToString();

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
