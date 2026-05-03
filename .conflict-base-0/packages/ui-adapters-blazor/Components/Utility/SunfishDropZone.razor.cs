using Sunfish.UIAdapters.Blazor.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.UIAdapters.Blazor.Components.Utility;

/// <summary>
/// A generic file drop target. Renders its <see cref="ChildContent"/> inside a wrapper div wired
/// with the four HTML drag events (<c>dragenter</c>, <c>dragleave</c>, <c>dragover</c>,
/// <c>drop</c>) and suppresses the browser's default "open file" behaviour so the drop event
/// actually fires.
/// </summary>
/// <remarks>
/// The component exposes <see cref="ElementId"/> so consumers can pair it with an existing
/// <c>SunfishFileUpload</c> / <c>SunfishUpload</c> by passing the same id to that component's
/// <c>DropZoneElement</c> parameter — the shared <c>IDropZoneService</c> then bridges dropped
/// files into the hidden file input. When the drop zone is used on its own, handle the drop
/// via <see cref="OnDrop"/>.
/// </remarks>
public partial class SunfishDropZone : SunfishComponentBase
{
    // ── Parameters ─────────────────────────────────────────────────────────

    /// <summary>
    /// The DOM id rendered on the wrapper element. When omitted a stable GUID-based id is
    /// generated. Pass this value to a file-input component's <c>DropZoneElement</c> parameter
    /// to forward dropped files through the shared IDropZoneService.
    /// </summary>
    [Parameter] public string ElementId { get; set; } = $"sf-dropzone-{Guid.NewGuid():N}";

    /// <summary>The content rendered inside the drop target.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Comma-separated list of MIME types and/or extensions used to advertise which files the
    /// zone accepts. Mirrors the <c>accept</c> attribute on <c>&lt;input type="file"&gt;</c>.
    /// Not enforced client-side — filtering is the consumer's responsibility.
    /// </summary>
    [Parameter] public string? Accept { get; set; }

    /// <summary>When <c>true</c>, drag/drop events are ignored and the zone renders as disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Fires on <c>drop</c> with the list of files extracted from the browser's DataTransfer.</summary>
    [Parameter] public EventCallback<DropZoneDropEventArgs> OnDrop { get; set; }

    /// <summary>Fires on <c>dragenter</c>.</summary>
    [Parameter] public EventCallback OnDragEnter { get; set; }

    /// <summary>Fires on <c>dragleave</c>.</summary>
    [Parameter] public EventCallback OnDragLeave { get; set; }

    /// <summary>Fires on <c>dragover</c>.</summary>
    [Parameter] public EventCallback OnDragOver { get; set; }

    // ── State ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the pointer is currently dragging over the zone. Exposed so consumers can read the
    /// value in their own ChildContent via <c>@ref</c> if needed.
    /// </summary>
    public bool IsDragOver { get; private set; }

    // Track enter/leave depth so nested children don't flicker the dragover state.
    private int _dragDepth;

    // ── Handlers ───────────────────────────────────────────────────────────

    private async Task HandleDragEnter(DragEventArgs e)
    {
        if (Disabled) return;
        _dragDepth++;
        if (!IsDragOver)
        {
            IsDragOver = true;
            StateHasChanged();
        }
        await OnDragEnter.InvokeAsync();
    }

    private async Task HandleDragLeave(DragEventArgs e)
    {
        if (Disabled) return;
        _dragDepth = Math.Max(0, _dragDepth - 1);
        if (_dragDepth == 0 && IsDragOver)
        {
            IsDragOver = false;
            StateHasChanged();
        }
        await OnDragLeave.InvokeAsync();
    }

    private async Task HandleDragOver(DragEventArgs e)
    {
        if (Disabled) return;
        await OnDragOver.InvokeAsync();
    }

    private async Task HandleDrop(DragEventArgs e)
    {
        if (Disabled) return;

        _dragDepth = 0;
        IsDragOver = false;

        var files = new List<DropZoneFileInfo>();
        if (e.DataTransfer?.Files is { Length: > 0 } fileNames)
        {
            // Blazor's DragEventArgs.DataTransfer.Files surfaces the filenames (and Items carry
            // metadata). Name is always populated; Size/ContentType may be zero/empty depending
            // on browser.
            foreach (var name in fileNames)
            {
                files.Add(new DropZoneFileInfo(name, 0, string.Empty));
            }
        }

        if (e.DataTransfer?.Items is { } items)
        {
            for (var i = 0; i < items.Length && i < files.Count; i++)
            {
                if (!string.IsNullOrEmpty(items[i].Type))
                {
                    var existing = files[i];
                    files[i] = existing with { ContentType = items[i].Type };
                }
            }
        }

        StateHasChanged();

        await OnDrop.InvokeAsync(new DropZoneDropEventArgs { Files = files });
    }

    private string GetRootClass() =>
        "sf-dropzone"
        + (IsDragOver ? " sf-dropzone--dragover" : string.Empty)
        + (Disabled ? " sf-dropzone--disabled" : string.Empty);
}
