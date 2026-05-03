namespace Sunfish.Foundation.Models.DataSheet;

/// <summary>
/// Represents a validation error on a specific cell in SunfishDataSheet.
/// </summary>
public class DataSheetValidationError<TItem>
{
    /// <summary>The row that failed validation.</summary>
    public TItem Row { get; init; } = default!;

    /// <summary>The field (property name) that failed validation.</summary>
    public string Field { get; init; } = "";

    /// <summary>The human-readable error message.</summary>
    public string Message { get; init; } = "";
}
