namespace Sunfish.Foundation.Models.DataSheet;

/// <summary>
/// Event arguments for the OnValidate callback, fired before Save All.
/// Handlers may append errors to block the save.
/// </summary>
public class DataSheetValidateArgs<TItem>
{
    /// <summary>The rows that have pending changes.</summary>
    public IReadOnlyList<TItem> DirtyRows { get; init; } = [];

    /// <summary>
    /// Validation errors. If any errors are present after all handlers run,
    /// the Save All operation is blocked.
    /// </summary>
    public List<DataSheetValidationError<TItem>> Errors { get; } = [];
}
