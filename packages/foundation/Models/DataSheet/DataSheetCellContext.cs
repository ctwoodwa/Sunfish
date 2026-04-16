namespace Sunfish.Foundation.Models.DataSheet;

/// <summary>
/// Context object passed to CellTemplate RenderFragment in SunfishDataSheetColumn.
/// </summary>
public class DataSheetCellContext<TItem>
{
    /// <summary>The row item.</summary>
    public TItem Item { get; init; } = default!;

    /// <summary>The field name of this cell's column.</summary>
    public string Field { get; init; } = "";

    /// <summary>The current cell value.</summary>
    public object? Value { get; init; }

    /// <summary>Whether the cell is currently in edit mode.</summary>
    public bool IsEditing { get; init; }

    /// <summary>Whether the cell's value has been modified.</summary>
    public bool IsDirty { get; init; }

    /// <summary>Validation error message, if any.</summary>
    public string? ValidationError { get; init; }
}
