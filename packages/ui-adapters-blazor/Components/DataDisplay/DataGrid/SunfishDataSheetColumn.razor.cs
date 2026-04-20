using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models.DataSheet;
using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

public partial class SunfishDataSheetColumn<TItem> : SunfishComponentBase
{
    [CascadingParameter] private SunfishDataSheet<TItem>? ParentGrid { get; set; }

    /// <summary>The property name on TItem to bind to this column.</summary>
    [Parameter] public string Field { get; set; } = "";

    /// <summary>Header text. Defaults to Field if not set.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Cell editor type. Defaults to Text.</summary>
    [Parameter] public DataSheetColumnType ColumnType { get; set; } = DataSheetColumnType.Text;

    /// <summary>Whether cells are user-editable. Defaults to true.</summary>
    [Parameter] public bool Editable { get; set; } = true;

    /// <summary>Blocks save if cell is empty. Defaults to false.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Minimum column width in px.</summary>
    [Parameter] public int? MinWidth { get; set; }

    /// <summary>Display formatter for read/computed cells.</summary>
    [Parameter] public Func<TItem, string?>? Format { get; set; }

    /// <summary>Returns null if valid, error string if invalid.</summary>
    [Parameter] public Func<TItem, string?>? Validate { get; set; }

    /// <summary>For Select type columns — value/label pairs.</summary>
    [Parameter] public IEnumerable<DataSheetSelectOption>? Options { get; set; }

    /// <summary>Full custom cell override template.</summary>
    [Parameter] public RenderFragment<DataSheetCellContext<TItem>>? CellTemplate { get; set; }

    /// <summary>Column width (CSS value, e.g. "120px").</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Gets the display title for the column header.</summary>
    internal string DisplayTitle => Title ?? Field;

    protected override void OnInitialized()
    {
        ParentGrid?.RegisterColumn(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ParentGrid?.UnregisterColumn(this);
        base.Dispose(disposing);
    }
}
