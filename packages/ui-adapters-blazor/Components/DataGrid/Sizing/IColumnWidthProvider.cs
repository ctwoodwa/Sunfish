namespace Sunfish.Components.Blazor.Components.DataGrid.Sizing;

/// <summary>
/// Computes authoritative column widths for one DataGrid layout pass.
/// </summary>
public interface IColumnWidthProvider
{
    GridLayoutContract Resolve(IReadOnlyList<ColumnSizingEntry> columns);
}
