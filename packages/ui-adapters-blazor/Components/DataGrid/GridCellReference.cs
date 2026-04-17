namespace Sunfish.Components.Blazor.Components.DataGrid;

/// <summary>
/// Represents a selected cell in the data grid.
/// </summary>
public class GridCellReference<TItem>
{
    /// <summary>The data item (row) containing this cell.</summary>
    public TItem Item { get; init; } = default!;

    /// <summary>The field name of the column.</summary>
    public string Field { get; init; } = "";

    /// <summary>The value of the cell.</summary>
    public object? Value { get; init; }

    /// <summary>The row index in the current display.</summary>
    public int RowIndex { get; init; }
}
