using Microsoft.AspNetCore.Components;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Spreadsheet;

/// <summary>
/// A single spreadsheet cell: its raw value, optional formula expression, and
/// simple type classification. Formatting is optional.
/// </summary>
public sealed class SpreadsheetCell
{
    /// <summary>Classification of the cell contents.</summary>
    public SpreadsheetCellType Type { get; set; } = SpreadsheetCellType.Text;

    /// <summary>Raw value. For formulas this is the last evaluated value; for literals it is the text.</summary>
    public string? Value { get; set; }

    /// <summary>Formula expression beginning with <c>=</c> (null for literal cells).</summary>
    public string? Formula { get; set; }

    /// <summary>Optional .NET format string applied when rendering numbers / dates.</summary>
    public string? Format { get; set; }

    /// <summary>Optional text alignment override. When null, alignment follows <see cref="Type"/>.</summary>
    public string? Align { get; set; }

    /// <summary>Returns a new <see cref="SpreadsheetCell"/> containing a literal text value.</summary>
    public static SpreadsheetCell Text(string? value)
        => new() { Type = SpreadsheetCellType.Text, Value = value };

    /// <summary>Returns a new <see cref="SpreadsheetCell"/> containing a numeric value.</summary>
    public static SpreadsheetCell Number(double value, string? format = null)
        => new() { Type = SpreadsheetCellType.Number, Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture), Format = format };

    /// <summary>Returns a new <see cref="SpreadsheetCell"/> containing a formula expression.</summary>
    public static SpreadsheetCell FormulaExpr(string expression)
        => new() { Type = SpreadsheetCellType.Formula, Formula = expression.StartsWith('=') ? expression : "=" + expression };
}

/// <summary>A single row in a spreadsheet's ordered cell grid.</summary>
public sealed class SpreadsheetRow
{
    /// <summary>Cells in this row, ordered left-to-right.</summary>
    public List<SpreadsheetCell> Cells { get; set; } = new();
}

/// <summary>Reference to a single cell by zero-based row and column indexes.</summary>
public readonly record struct SpreadsheetCellRef(int Row, int Column)
{
    /// <summary>Returns the human-readable A1-style address (e.g. <c>"B3"</c>).</summary>
    public string ToA1() => SpreadsheetAddress.ToA1(Row, Column);
}

/// <summary>Event args for <c>SunfishSpreadsheet.OnCellChanged</c>.</summary>
public sealed class SpreadsheetCellChangedEventArgs : EventArgs
{
    /// <summary>Zero-based row index.</summary>
    public int Row { get; init; }

    /// <summary>Zero-based column index.</summary>
    public int Column { get; init; }

    /// <summary>Previous rendered value.</summary>
    public string? OldValue { get; init; }

    /// <summary>New rendered value.</summary>
    public string? NewValue { get; init; }

    /// <summary>Formula expression (when applicable).</summary>
    public string? Formula { get; init; }
}

/// <summary>
/// Static helpers for translating between zero-based (row,col) and A1 addresses.
/// </summary>
public static class SpreadsheetAddress
{
    /// <summary>Converts a zero-based column index into the Excel-style letter header (0 → A, 27 → AB).</summary>
    public static string ColumnLetter(int column)
    {
        if (column < 0) return string.Empty;
        var result = string.Empty;
        column += 1;
        while (column > 0)
        {
            var rem = (column - 1) % 26;
            result = (char)('A' + rem) + result;
            column = (column - 1) / 26;
        }
        return result;
    }

    /// <summary>Converts a (row,col) pair into an A1-style address.</summary>
    public static string ToA1(int row, int column) => $"{ColumnLetter(column)}{row + 1}";

    /// <summary>Parses an A1-style address into zero-based (row,col). Returns <c>false</c> on malformed input.</summary>
    public static bool TryParseA1(string a1, out int row, out int column)
    {
        row = column = -1;
        if (string.IsNullOrWhiteSpace(a1)) return false;

        var i = 0;
        var col = 0;
        while (i < a1.Length && char.IsLetter(a1[i]))
        {
            col = col * 26 + (char.ToUpperInvariant(a1[i]) - 'A' + 1);
            i++;
        }
        if (i == 0 || i >= a1.Length) return false;

        if (!int.TryParse(a1.AsSpan(i), out var parsedRow) || parsedRow <= 0) return false;

        column = col - 1;
        row = parsedRow - 1;
        return true;
    }
}
