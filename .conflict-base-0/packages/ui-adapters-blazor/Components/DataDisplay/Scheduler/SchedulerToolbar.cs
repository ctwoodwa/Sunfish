using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Scheduler;

/// <summary>
/// Child component for the SunfishScheduler that provides custom toolbar content
/// alongside the default navigation controls. Place inside SunfishScheduler's ChildContent.
/// </summary>
public class SchedulerToolbar : ComponentBase, IDisposable
{
    [CascadingParameter] private ISchedulerViewHost? ViewHost { get; set; }

    /// <summary>Custom content to render inside the toolbar, alongside default navigation.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The rendered content, consumed by the parent scheduler.</summary>
    internal RenderFragment? Content => ChildContent;

    protected override void OnInitialized()
    {
        if (ViewHost is ISchedulerToolbarHost toolbarHost)
        {
            toolbarHost.RegisterToolbar(this);
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) { }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && ViewHost is ISchedulerToolbarHost toolbarHost)
        {
            toolbarHost.UnregisterToolbar(this);
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
