namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Classification of a <c>SunfishSpreadsheet</c> cell's content. Used to hint at
/// parsing, alignment, and formatting; the MVP renderer always uses a text input.
/// </summary>
public enum SpreadsheetCellType
{
    /// <summary>Plain text literal (default).</summary>
    Text,

    /// <summary>Numeric literal. Aligned right when rendered.</summary>
    Number,

    /// <summary>Date literal. Rendered using the cell's format string.</summary>
    Date,

    /// <summary>Boolean literal (true/false).</summary>
    Boolean,

    /// <summary>Formula expression beginning with <c>=</c> — evaluated at render time.</summary>
    Formula,
}
