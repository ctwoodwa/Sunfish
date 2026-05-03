namespace Sunfish.Foundation.Models.DataSheet;

/// <summary>
/// Event arguments for the OnSaveAll callback of SunfishDataSheet.
/// Contains the rows that have been modified and rows marked for deletion.
/// </summary>
public class DataSheetSaveArgs<TItem>
{
    /// <summary>Rows with at least one dirty field.</summary>
    public IReadOnlyList<TItem> DirtyRows { get; init; } = [];

    /// <summary>Rows that have been marked for deletion.</summary>
    public IReadOnlyList<TItem> DeletedRows { get; init; } = [];
}
