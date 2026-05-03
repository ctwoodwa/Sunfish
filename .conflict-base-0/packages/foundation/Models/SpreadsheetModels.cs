namespace Sunfish.Foundation.Models;

/// <summary>
/// Event arguments raised when a cell value changes in <c>SunfishSpreadsheet</c>.
/// </summary>
public class SpreadsheetCellEditEventArgs : EventArgs
{
    /// <summary>Zero-based row index of the edited cell.</summary>
    public int Row { get; set; }

    /// <summary>Zero-based column index of the edited cell.</summary>
    public int Column { get; set; }

    /// <summary>Previous cell value.</summary>
    public string? OldValue { get; set; }

    /// <summary>New cell value.</summary>
    public string? NewValue { get; set; }
}
