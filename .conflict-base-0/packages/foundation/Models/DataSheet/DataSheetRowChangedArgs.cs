namespace Sunfish.Foundation.Models.DataSheet;

/// <summary>
/// Event arguments fired after a cell value is committed in SunfishDataSheet.
/// </summary>
public class DataSheetRowChangedArgs<TItem>
{
    /// <summary>The row containing the changed cell.</summary>
    public TItem Row { get; init; } = default!;

    /// <summary>The property name of the changed field.</summary>
    public string Field { get; init; } = "";

    /// <summary>The value before the edit.</summary>
    public object? OldValue { get; init; }

    /// <summary>The new committed value.</summary>
    public object? NewValue { get; init; }
}
