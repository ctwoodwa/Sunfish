using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Spreadsheet;

/// <summary>
/// Excel-style grid with per-cell text editing, basic formula evaluation, and
/// A1-style column/row headers. Virtualization, cell merging, named ranges,
/// and export are deferred beyond this MVP.
/// </summary>
public partial class SunfishSpreadsheet : SunfishComponentBase
{
    /// <summary>
    /// Initial data as a 2-D string matrix (<c>[row, col]</c>). When set, the
    /// matrix populates <see cref="RowCount"/>/<see cref="ColumnCount"/> and all cells.
    /// Mutually exclusive with <see cref="Rows"/>.
    /// </summary>
    [Parameter] public string[,]? Data { get; set; }

    /// <summary>
    /// Initial data as a list of <see cref="SpreadsheetRow"/>. Preferred when
    /// cells need types, formulas, or formatting.
    /// </summary>
    [Parameter] public List<SpreadsheetRow>? Rows { get; set; }

    /// <summary>Visible row count when <see cref="Data"/>/<see cref="Rows"/> is empty.</summary>
    [Parameter] public int RowCount { get; set; } = 10;

    /// <summary>Visible column count when <see cref="Data"/>/<see cref="Rows"/> is empty.</summary>
    [Parameter] public int ColumnCount { get; set; } = 6;

    /// <summary>Optional CSS height of the scroll container.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Optional CSS width of the scroll container.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Currently selected cell reference.</summary>
    [Parameter] public SpreadsheetCellRef SelectedCell { get; set; } = new(0, 0);

    /// <summary>Fired when the selected cell changes (two-way binding).</summary>
    [Parameter] public EventCallback<SpreadsheetCellRef> SelectedCellChanged { get; set; }

    /// <summary>Fired after a cell edit is committed (on input blur).</summary>
    [Parameter] public EventCallback<SpreadsheetCellChangedEventArgs> OnCellChanged { get; set; }

    /// <summary>When <c>true</c>, disables cell mutation.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Optional ARIA label for the spreadsheet. Defaults to <c>"Spreadsheet"</c>.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Internal cell storage, indexed (row, col).</summary>
    protected SpreadsheetCell[,] CellGrid { get; private set; } = new SpreadsheetCell[0, 0];

    /// <summary>Effective row count (derived from initial data if provided).</summary>
    protected int EffectiveRows { get; private set; }

    /// <summary>Effective column count (derived from initial data if provided).</summary>
    protected int EffectiveColumns { get; private set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        RebuildGrid();
    }

    private void RebuildGrid()
    {
        if (Data is not null)
        {
            EffectiveRows = Data.GetLength(0);
            EffectiveColumns = Data.GetLength(1);
            CellGrid = new SpreadsheetCell[EffectiveRows, EffectiveColumns];
            for (var r = 0; r < EffectiveRows; r++)
            {
                for (var c = 0; c < EffectiveColumns; c++)
                {
                    var raw = Data[r, c];
                    CellGrid[r, c] = CreateCell(raw);
                }
            }
        }
        else if (Rows is not null && Rows.Count > 0)
        {
            EffectiveRows = Rows.Count;
            EffectiveColumns = Rows.Max(r => r.Cells.Count);
            CellGrid = new SpreadsheetCell[EffectiveRows, EffectiveColumns];
            for (var r = 0; r < EffectiveRows; r++)
            {
                for (var c = 0; c < EffectiveColumns; c++)
                {
                    CellGrid[r, c] = c < Rows[r].Cells.Count
                        ? Rows[r].Cells[c]
                        : new SpreadsheetCell();
                }
            }
        }
        else
        {
            EffectiveRows = Math.Max(1, RowCount);
            EffectiveColumns = Math.Max(1, ColumnCount);
            CellGrid = new SpreadsheetCell[EffectiveRows, EffectiveColumns];
            for (var r = 0; r < EffectiveRows; r++)
            {
                for (var c = 0; c < EffectiveColumns; c++)
                {
                    CellGrid[r, c] = new SpreadsheetCell();
                }
            }
        }
    }

    private static SpreadsheetCell CreateCell(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return new SpreadsheetCell();

        if (raw.StartsWith("=", StringComparison.Ordinal))
        {
            return new SpreadsheetCell { Type = SpreadsheetCellType.Formula, Formula = raw, Value = raw };
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return new SpreadsheetCell { Type = SpreadsheetCellType.Number, Value = raw };
        }

        if (bool.TryParse(raw, out _))
        {
            return new SpreadsheetCell { Type = SpreadsheetCellType.Boolean, Value = raw };
        }

        return new SpreadsheetCell { Type = SpreadsheetCellType.Text, Value = raw };
    }

    /// <summary>Returns the <see cref="SpreadsheetCell"/> at (row,col).</summary>
    public SpreadsheetCell GetCell(int row, int column)
    {
        if (row < 0 || row >= EffectiveRows || column < 0 || column >= EffectiveColumns)
        {
            return new SpreadsheetCell();
        }
        return CellGrid[row, column] ?? new SpreadsheetCell();
    }

    /// <summary>Updates the value of the cell at (row,col) and fires <see cref="OnCellChanged"/>.</summary>
    public async Task SetCellAsync(int row, int column, string? newValue)
    {
        if (ReadOnly) return;
        if (row < 0 || row >= EffectiveRows || column < 0 || column >= EffectiveColumns) return;

        var cell = CellGrid[row, column] ??= new SpreadsheetCell();
        var oldValue = RenderCell(row, column);

        if (!string.IsNullOrEmpty(newValue) && newValue.StartsWith("=", StringComparison.Ordinal))
        {
            cell.Type = SpreadsheetCellType.Formula;
            cell.Formula = newValue;
            cell.Value = newValue;
        }
        else
        {
            cell.Formula = null;
            cell.Value = newValue;
            cell.Type = InferType(newValue);
        }

        if (OnCellChanged.HasDelegate)
        {
            await OnCellChanged.InvokeAsync(new SpreadsheetCellChangedEventArgs
            {
                Row = row,
                Column = column,
                OldValue = oldValue,
                NewValue = RenderCell(row, column),
                Formula = cell.Formula,
            });
        }
    }

    /// <summary>Selects a cell by (row,col) and fires <see cref="SelectedCellChanged"/>.</summary>
    public async Task SelectAsync(int row, int column)
    {
        var cellRef = new SpreadsheetCellRef(row, column);
        if (SelectedCell == cellRef) return;
        SelectedCell = cellRef;
        if (SelectedCellChanged.HasDelegate)
        {
            await SelectedCellChanged.InvokeAsync(cellRef);
        }
    }

    private static SpreadsheetCellType InferType(string? value)
    {
        if (string.IsNullOrEmpty(value)) return SpreadsheetCellType.Text;
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return SpreadsheetCellType.Number;
        if (bool.TryParse(value, out _)) return SpreadsheetCellType.Boolean;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return SpreadsheetCellType.Date;
        return SpreadsheetCellType.Text;
    }

    // ── Rendering ──────────────────────────────────────────────────────

    /// <summary>Returns the user-facing rendered value for the cell at (row,col).</summary>
    public string RenderCell(int row, int column) => RenderCell(row, column, new HashSet<(int, int)>());

    private string RenderCell(int row, int column, HashSet<(int, int)> inFlight)
    {
        var cell = GetCell(row, column);
        if (cell.Type == SpreadsheetCellType.Formula && !string.IsNullOrEmpty(cell.Formula))
        {
            if (!inFlight.Add((row, column))) return "#REF!";
            try
            {
                var result = EvaluateFormula(cell.Formula, inFlight);
                return FormatResult(result, cell.Format);
            }
            catch
            {
                return "#ERR!";
            }
            finally
            {
                inFlight.Remove((row, column));
            }
        }

        if (!string.IsNullOrEmpty(cell.Format) && cell.Type == SpreadsheetCellType.Number
            && double.TryParse(cell.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            return num.ToString(cell.Format, CultureInfo.CurrentCulture);
        }

        return cell.Value ?? string.Empty;
    }

    /// <summary>Returns the raw edit-mode value for the cell — shows the formula expression when present.</summary>
    public string EditValue(int row, int column)
    {
        var cell = GetCell(row, column);
        if (cell.Type == SpreadsheetCellType.Formula && !string.IsNullOrEmpty(cell.Formula))
        {
            return cell.Formula;
        }
        return cell.Value ?? string.Empty;
    }

    // ── Formula engine ─────────────────────────────────────────────────

    private static readonly Regex FunctionRegex = new(@"^=\s*(SUM|AVG|AVERAGE|MIN|MAX|COUNT)\s*\(\s*([^)]+)\s*\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RangeRegex = new(@"^([A-Z]+\d+)\s*:\s*([A-Z]+\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex A1Regex = new(@"[A-Z]+\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private double? EvaluateFormula(string formula, HashSet<(int, int)> inFlight)
    {
        var trimmed = formula.TrimStart('=').Trim();
        if (trimmed.Length == 0) return null;

        // Function call: SUM(A1:A3), AVG(A1:B2), etc.
        var funcMatch = FunctionRegex.Match(formula);
        if (funcMatch.Success)
        {
            var fn = funcMatch.Groups[1].Value.ToUpperInvariant();
            var argText = funcMatch.Groups[2].Value;
            var values = ResolveRange(argText, inFlight).ToList();
            if (values.Count == 0) return null;
            return fn switch
            {
                "SUM" => values.Sum(),
                "AVG" or "AVERAGE" => values.Average(),
                "MIN" => values.Min(),
                "MAX" => values.Max(),
                "COUNT" => values.Count,
                _ => null,
            };
        }

        // Arithmetic expression (+, -, *, /) with cell references and literals.
        var substituted = A1Regex.Replace(trimmed, m =>
        {
            if (!SpreadsheetAddress.TryParseA1(m.Value, out var r, out var c)) return "0";
            var rendered = RenderCell(r, c, inFlight);
            return double.TryParse(rendered, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed.ToString(CultureInfo.InvariantCulture)
                : "0";
        });

        return EvaluateArithmetic(substituted);
    }

    private IEnumerable<double> ResolveRange(string expr, HashSet<(int, int)> inFlight)
    {
        foreach (var part in expr.Split(','))
        {
            var token = part.Trim();
            var rangeMatch = RangeRegex.Match(token);
            if (rangeMatch.Success)
            {
                if (SpreadsheetAddress.TryParseA1(rangeMatch.Groups[1].Value, out var r1, out var c1) &&
                    SpreadsheetAddress.TryParseA1(rangeMatch.Groups[2].Value, out var r2, out var c2))
                {
                    var rStart = Math.Min(r1, r2);
                    var rEnd = Math.Max(r1, r2);
                    var cStart = Math.Min(c1, c2);
                    var cEnd = Math.Max(c1, c2);
                    for (var r = rStart; r <= rEnd; r++)
                    {
                        for (var c = cStart; c <= cEnd; c++)
                        {
                            var rendered = RenderCell(r, c, inFlight);
                            if (double.TryParse(rendered, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                            {
                                yield return parsed;
                            }
                        }
                    }
                }
            }
            else if (SpreadsheetAddress.TryParseA1(token, out var r, out var c))
            {
                var rendered = RenderCell(r, c, inFlight);
                if (double.TryParse(rendered, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    yield return parsed;
                }
            }
            else if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var literal))
            {
                yield return literal;
            }
        }
    }

    /// <summary>Minimal +/-/*/ recursive-descent evaluator with no parentheses support.</summary>
    private static double? EvaluateArithmetic(string expr)
    {
        // Additive pass
        var terms = SplitTopLevel(expr, '+', '-');
        if (terms.Count == 0) return null;

        double? total = null;
        foreach (var term in terms)
        {
            var (sign, body) = term;
            var factorValue = EvaluateMultiplicative(body);
            if (factorValue is null) return null;
            if (total is null)
            {
                total = sign == '-' ? -factorValue : factorValue;
            }
            else
            {
                total = sign == '-' ? total - factorValue : total + factorValue;
            }
        }
        return total;
    }

    private static double? EvaluateMultiplicative(string expr)
    {
        var factors = SplitTopLevel(expr, '*', '/');
        if (factors.Count == 0) return null;

        double? total = null;
        foreach (var factor in factors)
        {
            var (op, body) = factor;
            var value = double.TryParse(body.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (double?)null;
            if (value is null) return null;
            if (total is null)
            {
                total = value;
            }
            else
            {
                total = op == '/' ? (value == 0 ? null : total / value) : total * value;
            }
        }
        return total;
    }

    /// <summary>Splits an expression on the given additive/multiplicative operators, keeping their signs.</summary>
    private static List<(char op, string body)> SplitTopLevel(string expr, char op1, char op2)
    {
        var results = new List<(char, string)>();
        var buffer = new System.Text.StringBuilder();
        var currentOp = '+'; // For additive; multiplicative uses '*' as default via caller convention.

        // Normalize leading +/- when splitting additively.
        if (op1 == '+' || op1 == '-')
        {
            // additive
        }
        else
        {
            currentOp = '*';
        }

        for (var i = 0; i < expr.Length; i++)
        {
            var ch = expr[i];
            if ((ch == op1 || ch == op2) && buffer.Length > 0)
            {
                results.Add((currentOp, buffer.ToString()));
                buffer.Clear();
                currentOp = ch;
            }
            else if (ch == op1 || ch == op2)
            {
                // Leading unary sign
                currentOp = ch;
            }
            else
            {
                buffer.Append(ch);
            }
        }
        if (buffer.Length > 0) results.Add((currentOp, buffer.ToString()));
        return results;
    }

    private static string FormatResult(double? value, string? format)
    {
        if (value is null) return string.Empty;
        if (!string.IsNullOrEmpty(format))
        {
            return value.Value.ToString(format, CultureInfo.CurrentCulture);
        }
        return value.Value == Math.Floor(value.Value)
            ? value.Value.ToString("0", CultureInfo.CurrentCulture)
            : value.Value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    /// <summary>Gets a composed size style for the outer container.</summary>
    protected string SizeStyle()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Width)) parts.Add($"width:{Width}");
        if (!string.IsNullOrEmpty(Height)) parts.Add($"height:{Height};overflow:auto");
        return string.Join(";", parts);
    }

    /// <summary>Returns the letter header label for a column (0 → A, 27 → AB).</summary>
    protected static string ColumnLetter(int column) => SpreadsheetAddress.ColumnLetter(column);

    /// <summary>Returns <c>true</c> when the given cell reference equals the current selection.</summary>
    protected bool IsSelected(int row, int column) => SelectedCell.Row == row && SelectedCell.Column == column;

    /// <summary>Razor event glue — handles the input blur commit.</summary>
    protected Task HandleCellCommitted(int row, int column, string? value) => SetCellAsync(row, column, value);

    /// <summary>Razor event glue — handles focus for selection tracking.</summary>
    protected Task HandleCellFocused(int row, int column) => SelectAsync(row, column);
}
