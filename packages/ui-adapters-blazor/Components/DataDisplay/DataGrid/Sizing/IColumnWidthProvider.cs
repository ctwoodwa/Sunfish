namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Computes authoritative column widths for one DataGrid layout pass.
/// </summary>
public interface IColumnWidthProvider
{
    GridLayoutContract Resolve(IReadOnlyList<ColumnSizingEntry> columns);
}
