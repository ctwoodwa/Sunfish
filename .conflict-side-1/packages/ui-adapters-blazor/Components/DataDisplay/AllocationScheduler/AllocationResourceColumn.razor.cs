using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Defines a resource metadata column in the AllocationScheduler's left-side resource grid.
/// Must be placed inside an AllocationResourceColumns RenderFragment.
/// </summary>
public partial class AllocationResourceColumn<TResource> : ComponentBase, IDisposable
{
    [CascadingParameter]
    private SunfishAllocationScheduler<TResource>? Parent { get; set; }

    /// <summary>Property name on TResource to display in this column.</summary>
    [Parameter] public string Field { get; set; } = string.Empty;

    /// <summary>Column header text.</summary>
    [Parameter] public string Title { get; set; } = string.Empty;

    /// <summary>Column width CSS value.</summary>
    [Parameter] public string Width { get; set; } = "auto";

    /// <summary>Custom cell template for this column.</summary>
    [Parameter] public RenderFragment<TResource>? Template { get; set; }

    /// <summary>Custom header template.</summary>
    [Parameter] public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>Enable sorting on this column.</summary>
    [Parameter] public bool Sortable { get; set; }

    /// <summary>Enable filtering on this column.</summary>
    [Parameter] public bool Filterable { get; set; }

    /// <summary>Show or hide this column.</summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>
    /// Pin column to left edge during horizontal scroll.
    /// Obsolete: AllocationScheduler now uses a splitter-based dual-pane layout
    /// where the left pane is the frozen region. This property is no longer honored.
    /// </summary>
    [Obsolete("Resource column pinning is obsolete. The left pane is the frozen region in the splitter-based layout.")]
    [Parameter] public bool Pinned { get; set; }

    /// <summary>Allow the user to resize this column via the splitter or column header drag. Defaults to true.</summary>
    [Parameter] public bool AllowResize { get; set; } = true;

    /// <summary>Minimum width in pixels when resizing. Defaults to 40.</summary>
    [Parameter] public double MinWidth { get; set; } = 40;

    /// <summary>Maximum width in pixels when resizing. Null means no upper limit.</summary>
    [Parameter] public double? MaxWidth { get; set; }

    /// <summary>Internal backing field for width, settable by parent scheduler during resize.</summary>
    internal string RuntimeWidth { get; set; } = string.Empty;

    /// <summary>Effective width: RuntimeWidth if set by resize, otherwise the declared Width parameter.</summary>
    internal string EffectiveWidth => !string.IsNullOrEmpty(RuntimeWidth) ? RuntimeWidth : Width;

    protected override void OnInitialized()
    {
        Parent?.AddColumn(this);
    }

    public void Dispose()
    {
        Parent?.RemoveColumn(this);
    }
}
