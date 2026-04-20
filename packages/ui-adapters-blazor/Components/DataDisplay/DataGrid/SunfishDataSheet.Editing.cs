using System.Diagnostics;
using System.Globalization;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Active cell state, edit mode transitions, and keyboard handlers for SunfishDataSheet.
/// </summary>
public partial class SunfishDataSheet<TItem>
{
    // ── Active Cell State ──────────────────────────────────────────────

    internal TItem? _activeCellRow;
    internal string? _activeCellField;
    internal bool _isEditMode;
    internal object? _editValueBeforeEdit; // for undo/cancel

    // Track last committed value per cell for Ctrl+Z
    private readonly Dictionary<string, object?> _undoBuffer = [];

    // ── Cell Activation ────────────────────────────────────────────────

    internal void ActivateCell(TItem row, string field)
    {
        _activeCellRow = row;
        _activeCellField = field;
        _isEditMode = false;
        StateHasChanged();
    }

    /// <summary>Enters edit mode on the specified cell.</summary>
    public void EnterEditMode(TItem row, string field)
    {
        var column = _columns.FirstOrDefault(c => c.Field == field);
        if (column is null || !column.Editable || column.ColumnType == DataSheetColumnType.Computed)
            return;

        _activeCellRow = row;
        _activeCellField = field;
        _isEditMode = true;
        _editValueBeforeEdit = GridReflectionHelper.GetValue(row, field);
        StateHasChanged();
    }

    internal void ClearActiveCell()
    {
        _activeCellRow = default;
        _activeCellField = null;
        _isEditMode = false;
        _editValueBeforeEdit = null;
    }

    internal bool IsCellActive(TItem row, string field)
    {
        return _activeCellRow != null
               && EqualityComparer<TItem>.Default.Equals(_activeCellRow, row)
               && _activeCellField == field;
    }

    /// <summary>Whether a specific cell is currently in edit mode.</summary>
    public bool IsCellEditing(TItem row, string field)
    {
        return IsCellActive(row, field) && _isEditMode;
    }

    // ── Cell Click Handler ─────────────────────────────────────────────

    internal void OnCellClick(TItem row, string field)
    {
        var column = _columns.FirstOrDefault(c => c.Field == field);
        if (column is null) return;

        if (column.ColumnType == DataSheetColumnType.Computed || !column.Editable)
        {
            ActivateCell(row, field);
            return;
        }

        // Checkbox columns toggle immediately on click
        if (column.ColumnType == DataSheetColumnType.Checkbox)
        {
            var currentVal = GridReflectionHelper.GetValue(row, field);
            var newVal = currentVal is true ? (object)false : (object)true;
            _ = CommitCellEdit(row, field, newVal);
            return;
        }

        if (IsCellActive(row, field) && !_isEditMode)
        {
            EnterEditMode(row, field);
        }
        else
        {
            ActivateCell(row, field);
        }
    }

    // ── Cell Double-Click Handler (SA-09) ──────────────────────────────

    internal void OnCellDoubleClick(TItem row, string field)
    {
        var column = _columns.FirstOrDefault(c => c.Field == field);
        if (column is null) return;

        if (column.ColumnType == DataSheetColumnType.Computed || !column.Editable)
            return;

        ActivateCell(row, field);
        EnterEditMode(row, field);
    }

    // ── Cell Value Commit ──────────────────────────────────────────────

    internal async Task OnCellValueCommit(TItem row, string field, object? newValue)
    {
        // Store undo
        var undoKey = $"{GetRowKey(row)}:{field}";
        _undoBuffer[undoKey] = _editValueBeforeEdit;

        await CommitCellEdit(row, field, newValue);
        _isEditMode = false;
    }

    // ── Keyboard Handler (called from JS) ──────────────────────────────

    [JSInvokable]
    public async Task HandleKeyDown(string key, bool ctrl, bool shift)
    {
        // Ctrl+S: Save All
        if (ctrl && key == "s")
        {
            await SaveAllAsync();
            return;
        }

        // Ctrl+Z: Undo last cell change
        if (ctrl && key == "z" && _activeCellRow != null && _activeCellField != null)
        {
            var undoKey = $"{GetRowKey(_activeCellRow)}:{_activeCellField}";
            if (_undoBuffer.TryGetValue(undoKey, out var prevValue))
            {
                await CommitCellEdit(_activeCellRow, _activeCellField, prevValue);
                _undoBuffer.Remove(undoKey);
            }
            return;
        }

        // Ctrl+C: Copy (handled in JS for clipboard access)
        // Ctrl+V: Paste (handled in JS, calls PasteFromClipboard)

        // Ctrl+D: Fill down
        if (ctrl && key == "d" && _activeCellRow != null && _activeCellField != null)
        {
            var value = GridReflectionHelper.GetValue(_activeCellRow, _activeCellField);
            var startIdx = _displayRows.IndexOf(_activeCellRow);
            if (startIdx >= 0)
            {
                foreach (var selectedRow in _selectedRows)
                {
                    var idx = _displayRows.IndexOf(selectedRow);
                    if (idx > startIdx)
                    {
                        await CommitCellEdit(selectedRow, _activeCellField, value);
                    }
                }
            }
            return;
        }

        // Escape: Cancel edit
        if (key == "Escape" && _isEditMode && _activeCellRow != null && _activeCellField != null)
        {
            GridReflectionHelper.SetValue(_activeCellRow, _activeCellField, _editValueBeforeEdit);
            _isEditMode = false;
            StateHasChanged();
            return;
        }

        // F2: Enter edit mode
        if (key == "F2" && !_isEditMode && _activeCellRow != null && _activeCellField != null)
        {
            EnterEditMode(_activeCellRow, _activeCellField);
            return;
        }

        // V07.1 — Enter key also enters edit mode when not currently editing.
        // Spec keyboard-and-accessibility.md:41 — "Enters edit mode on the
        // active cell (same as F2)." Must sit BEFORE the Tab/Enter edit-mode
        // navigation branch below so commit-then-move Enter behavior still
        // runs on the in-edit-mode path.
        if (key == "Enter" && !_isEditMode && _activeCellRow != null && _activeCellField != null)
        {
            EnterEditMode(_activeCellRow, _activeCellField);
            return;
        }

        // V07.3 — Space toggles checkbox cells without a separate edit mode.
        // Spec keyboard-and-accessibility.md:51 — checkbox cells do not have
        // an edit mode; Space reads the current value and commits the flip.
        if (key == " " && !_isEditMode && _activeCellRow != null && _activeCellField != null)
        {
            var spaceColumn = _columns.FirstOrDefault(c => c.Field == _activeCellField);
            if (spaceColumn != null
                && spaceColumn.Editable
                && spaceColumn.ColumnType == DataSheetColumnType.Checkbox)
            {
                var currentValue = GridReflectionHelper.GetValue(_activeCellRow, _activeCellField);
                var newValue = currentValue is true ? (object)false : (object)true;
                await CommitCellEdit(_activeCellRow, _activeCellField, newValue);
                return;
            }
        }

        // Delete: Clear selected cells
        if (key == "Delete" && _activeCellRow != null && _activeCellField != null)
        {
            var column = _columns.FirstOrDefault(c => c.Field == _activeCellField);
            if (column != null && column.Editable && column.ColumnType != DataSheetColumnType.Computed)
            {
                await CommitCellEdit(_activeCellRow, _activeCellField, GetDefaultValue(column));
            }
            return;
        }

        // Navigation: only when not in edit mode
        if (!_isEditMode && _activeCellRow != null && _activeCellField != null)
        {
            var rowIdx = _displayRows.IndexOf(_activeCellRow);
            var colIdx = _columns.FindIndex(c => c.Field == _activeCellField);

            switch (key)
            {
                case "ArrowDown" when rowIdx < _displayRows.Count - 1:
                    ActivateCell(_displayRows[rowIdx + 1], _activeCellField);
                    return;
                case "ArrowUp" when rowIdx > 0:
                    ActivateCell(_displayRows[rowIdx - 1], _activeCellField);
                    return;
                case "ArrowRight" when colIdx < _columns.Count - 1:
                    ActivateCell(_activeCellRow, _columns[colIdx + 1].Field);
                    return;
                case "ArrowLeft" when colIdx > 0:
                    ActivateCell(_activeCellRow, _columns[colIdx - 1].Field);
                    return;
            }
        }

        // Tab navigation — works in both edit and non-edit mode per spec
        // keyboard-and-accessibility.md:38-39 (Context = "Any"). Implements
        // row-wrap and grid-exit behavior per spec:100-108.
        if (key == "Tab" && _activeCellRow != null && _activeCellField != null)
        {
            var moved = MoveToNextEditableCell(shift);
            if (moved && _isEditMode)
            {
                _isEditMode = false;
                StateHasChanged();
            }
            else if (!moved)
            {
                // At first/last editable cell of the grid — clear the
                // active cell so the grid no longer visually indicates a
                // focused cell. The browser's native Tab handling then
                // moves focus to the next focusable element outside the
                // grid (button, link, etc.). Known limitation: if the
                // upstream JS keydown handler has already called
                // preventDefault on the event, the user sees the grid
                // lose its active-cell highlight but focus remains on the
                // grid's root <div>. The spec is explicit that a
                // grid-boundary Tab exits the grid, so the interop layer
                // should NOT preventDefault on Tab at the boundary.
                ClearActiveCell();
                StateHasChanged();
            }
            return;
        }

        // Enter in edit mode: commit and move down one row, same column.
        if (key == "Enter" && _isEditMode && _activeCellRow != null && _activeCellField != null)
        {
            var rowIdx = _displayRows.IndexOf(_activeCellRow);
            if (rowIdx >= 0 && rowIdx < _displayRows.Count - 1)
            {
                _isEditMode = false;
                ActivateCell(_displayRows[rowIdx + 1], _activeCellField);
            }
            else
            {
                // On the last row, commit without moving (spec: "If on the
                // last row, commits without moving.").
                _isEditMode = false;
                StateHasChanged();
            }
            return;
        }

        // V07.2 — Printable character → edit mode with typed char as the
        // initial value. Spec keyboard-and-accessibility.md:49: "Enters edit
        // mode and replaces the cell value with the typed character (text
        // and number columns only)." Must sit after all named-key branches
        // so "Escape"/"Tab"/etc. aren't misclassified. Space is handled by
        // the checkbox toggle branch above and is deliberately excluded
        // here — a leading space is not useful as a cell edit seed.
        if (!_isEditMode
            && !ctrl
            && key.Length == 1
            && key != " "
            && _activeCellRow != null
            && _activeCellField != null)
        {
            var column = _columns.FirstOrDefault(c => c.Field == _activeCellField);
            if (column == null || !column.Editable || column.ColumnType == DataSheetColumnType.Computed)
            {
                return;
            }

            if (column.ColumnType == DataSheetColumnType.Text)
            {
                EnterEditMode(_activeCellRow, _activeCellField);
                GridReflectionHelper.SetValue(_activeCellRow, _activeCellField, key);
                StateHasChanged();
                return;
            }

            if (column.ColumnType == DataSheetColumnType.Number)
            {
                var (success, parsed) = ParseNumberForCell(column, key);
                if (success)
                {
                    EnterEditMode(_activeCellRow, _activeCellField);
                    GridReflectionHelper.SetValue(_activeCellRow, _activeCellField, parsed);
                    StateHasChanged();
                }
                return;
            }
        }
    }

    // V07 Tab wrap — Moves the active cell to the next/previous editable
    // cell, wrapping across rows per spec keyboard-and-accessibility.md:100-108.
    // Returns true if the active cell moved to another editable cell; false
    // if the caller should exit the grid (boundary reached).
    private bool MoveToNextEditableCell(bool reverse)
    {
        if (_activeCellRow is null || _activeCellField is null)
        {
            return false;
        }

        // Build the list of editable (non-computed) column indices. If
        // nothing is editable, there is no next cell to move to.
        var editableColIndexes = new List<int>();
        for (var i = 0; i < _columns.Count; i++)
        {
            var c = _columns[i];
            if (c.Editable && c.ColumnType != DataSheetColumnType.Computed)
            {
                editableColIndexes.Add(i);
            }
        }
        if (editableColIndexes.Count == 0)
        {
            return false;
        }

        var rowIdx = _displayRows.IndexOf(_activeCellRow);
        var colIdx = _columns.FindIndex(c => c.Field == _activeCellField);
        if (rowIdx < 0 || colIdx < 0)
        {
            return false;
        }

        // Find the current cell's position within the editable-only
        // traversal sequence, then step forward or backward.
        var editableColPos = editableColIndexes.IndexOf(colIdx);

        if (!reverse)
        {
            // Tab forward: next editable column in same row, else first
            // editable column of the next row.
            if (editableColPos >= 0 && editableColPos < editableColIndexes.Count - 1)
            {
                ActivateCell(_activeCellRow, _columns[editableColIndexes[editableColPos + 1]].Field);
                return true;
            }
            if (editableColPos < 0)
            {
                // Current column is not editable (e.g. user arrow-keyed
                // onto a Computed cell). Jump to the first editable column
                // after the current one in the same row; if none, fall to
                // next row.
                var nextCol = editableColIndexes.FirstOrDefault(i => i > colIdx, -1);
                if (nextCol >= 0)
                {
                    ActivateCell(_activeCellRow, _columns[nextCol].Field);
                    return true;
                }
            }
            if (rowIdx < _displayRows.Count - 1)
            {
                ActivateCell(_displayRows[rowIdx + 1], _columns[editableColIndexes[0]].Field);
                return true;
            }
            return false; // past last editable cell of the grid
        }
        else
        {
            // Shift+Tab reverse: previous editable column in same row, else
            // last editable column of the previous row.
            if (editableColPos > 0)
            {
                ActivateCell(_activeCellRow, _columns[editableColIndexes[editableColPos - 1]].Field);
                return true;
            }
            if (editableColPos < 0)
            {
                var prevCol = editableColIndexes.LastOrDefault(i => i < colIdx, -1);
                if (prevCol >= 0)
                {
                    ActivateCell(_activeCellRow, _columns[prevCol].Field);
                    return true;
                }
            }
            if (rowIdx > 0)
            {
                ActivateCell(_displayRows[rowIdx - 1], _columns[editableColIndexes[^1]].Field);
                return true;
            }
            return false; // before first editable cell of the grid
        }
    }

    // ── Paste Handler (called from JS) ─────────────────────────────────

    [JSInvokable]
    public async Task PasteFromClipboard(string tsvData)
    {
        if (!AllowBulkPaste || _activeCellRow is null || _activeCellField is null) return;
        if (IsSaving) return;  // SA-08: paste disabled during save

        var startRowIdx = _displayRows.IndexOf(_activeCellRow);
        var startColIdx = _columns.FindIndex(c => c.Field == _activeCellField);
        if (startRowIdx < 0 || startColIdx < 0) return;

        // V04.1 — Normalize line endings. Windows clipboards produce "\r\n";
        // splitting on '\n' alone leaves '\r' appended to the last cell of
        // each row and breaks decimal/DateTime parsing.
        var normalized = tsvData.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // V04.3 — Skip rows that are marked for deletion. The TSV cursor
        // advances independently from the display-row cursor so that a
        // pasted row lands on the next non-deleted row rather than being
        // silently dropped onto a deleted one.
        var rowCursor = startRowIdx;
        for (var r = 0; r < lines.Length; r++)
        {
            while (rowCursor < _displayRows.Count && IsRowDeleted(_displayRows[rowCursor]))
            {
                rowCursor++;
            }
            if (rowCursor >= _displayRows.Count) break;

            var row = _displayRows[rowCursor];
            var cells = lines[r].Split('\t');
            for (var c = 0; c < cells.Length && startColIdx + c < _columns.Count; c++)
            {
                var column = _columns[startColIdx + c];
                if (!column.Editable || column.ColumnType == DataSheetColumnType.Computed) continue;

                var (success, parsedValue, errorMessage) = TryParseCellValue(column, cells[c].Trim());
                if (success)
                {
                    await CommitCellEdit(row, column.Field, parsedValue);
                }
                else
                {
                    // V04.2 — Do NOT write the raw pasted string to the model.
                    // Mark the cell invalid with a type-specific message and
                    // leave the row's property at its pre-paste value.
                    MarkPasteCellInvalid(row, column.Field, errorMessage!);
                }
            }

            rowCursor++;
        }

        StateHasChanged();
    }

    // V04.2 — Records a paste-time coercion failure on the dirty-row entry
    // without mutating the underlying TItem property. Mirrors the pattern
    // used by CommitCellEdit's ValidationErrors dictionary so the cell
    // surfaces CellState.Invalid through the existing GetCellState path.
    private void MarkPasteCellInvalid(TItem row, string field, string errorMessage)
    {
        var key = GetRowKey(row);
        if (key is null) return;

        if (!_dirtyRows.TryGetValue(key, out var entry))
        {
            entry = new DirtyRowEntry<TItem>
            {
                Original = GridReflectionHelper.DeepClone(row),
                Current = row
            };
            _dirtyRows[key] = entry;
        }

        entry.ValidationErrors[field] = errorMessage;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static object? GetDefaultValue(SunfishDataSheetColumn<TItem> column)
    {
        return column.ColumnType switch
        {
            DataSheetColumnType.Text => "",
            DataSheetColumnType.Number => 0m,
            DataSheetColumnType.Checkbox => false,
            DataSheetColumnType.Date => null,
            DataSheetColumnType.Select => "",
            _ => null
        };
    }

    // V04.2 — Returns (success, parsedValue, errorMessage). On success the
    // parsed value is type-correct for the column's CLR property; on
    // failure the error message matches the spec (bulk-paste-and-clipboard.md
    // "Type Coercion on Paste" table) and the paste loop will mark the cell
    // invalid without writing the raw string to the model.
    // F1.N4 — Restored to a switch-expression by extracting per-arm helpers
    // so every branch fits on a single line.
    private static (bool Success, object? Value, string? Error) TryParseCellValue(
        SunfishDataSheetColumn<TItem> column, string text) => column.ColumnType switch
    {
        DataSheetColumnType.Number => TryParseNumberCell(column, text),
        DataSheetColumnType.Date => TryParseDateCell(text),
        DataSheetColumnType.Checkbox => TryParseCheckboxCell(text),
        DataSheetColumnType.Select => TryParseSelectCell(column, text),
        DataSheetColumnType.Text or DataSheetColumnType.Computed => (true, text, null),
        _ => UnknownColumnTypeFallback(column.ColumnType, text),
    };

    // F1.N5 — Centralizes `typeof(TItem).GetProperty(column.Field)?.PropertyType`
    // for Number columns so V07.2 (printable-char path) and TryParseCellValue
    // (paste path) share a single lookup surface. Falls back to decimal when
    // the property is missing so downstream callers can rely on a non-null Type.
    private static Type GetColumnClrType(SunfishDataSheetColumn<TItem> column)
    {
        return typeof(TItem).GetProperty(column.Field)?.PropertyType ?? typeof(decimal);
    }

    // F1.N4 — Number-column parsing extracted so TryParseCellValue and the V07.2
    // printable-char branch share a single numeric-coercion path. Returns the
    // (bool Success, object? Value) contract expected by V07.2. Paste callers
    // use TryParseNumberCell below which wraps the same logic in the three-tuple
    // paste contract.
    private static (bool Success, object? Value) ParseNumberForCell(
        SunfishDataSheetColumn<TItem> column, string text)
    {
        return ParseNumericValue(text, GetColumnClrType(column));
    }

    // F1.N4 — Paste-path wrapper around ParseNumberForCell that adds the
    // spec-required "Invalid number" error string on failure.
    private static (bool Success, object? Value, string? Error) TryParseNumberCell(
        SunfishDataSheetColumn<TItem> column, string text)
    {
        var (success, value) = ParseNumberForCell(column, text);
        return success ? (true, value, null) : (false, null, "Invalid number");
    }

    // F1.N4 — Date branch extracted so TryParseCellValue can stay a
    // switch-expression. V04.4 copy path emits dates via InvariantCulture into
    // data-raw-value; parsing with the same culture here lets paste round-trip
    // on non-en-US systems (e.g. de-DE, where default DateTime.TryParse would
    // misread "4/10/2026" as d.m.y).
    private static (bool Success, object? Value, string? Error) TryParseDateCell(string text)
    {
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? (true, dt, null)
            : (false, null, "Invalid date");
    }

    // F1.N4 — Checkbox branch extracted so TryParseCellValue's switch
    // expression stays one line per arm. Matches V04.2 spec: "true"
    // (case-insensitive) or "1" both coerce to true, everything else false.
    private static (bool Success, object? Value, string? Error) TryParseCheckboxCell(string text)
    {
        return (true,
                text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1",
                null);
    }

    // F1.N4 — Select branch extracted. Pasted value must exist in the column's
    // Options collection (ordinal, case-sensitive) or the cell is marked invalid
    // with the spec-required "Value not in options" error string.
    private static (bool Success, object? Value, string? Error) TryParseSelectCell(
        SunfishDataSheetColumn<TItem> column, string text)
    {
        if (column.Options != null
            && column.Options.Any(o => string.Equals(o.Value, text, StringComparison.Ordinal)))
        {
            return (true, text, null);
        }
        return (false, null, "Value not in options");
    }

    // F1.N4 — Default-arm helper for TryParseCellValue's switch expression.
    // Keeps Debug.Fail on the unreachable branch while letting the switch
    // stay expression-form.
    private static (bool Success, object? Value, string? Error) UnknownColumnTypeFallback(
        DataSheetColumnType columnType, string text)
    {
        Debug.Fail($"Unknown DataSheetColumnType: {columnType}");
        return (true, text, null);
    }

    /// <summary>
    /// Parses a string into the requested numeric target type, handling all
    /// primitive numeric types (int, long, short, byte, decimal, double, float)
    /// and their nullable counterparts. Returns (true, parsedValue) on success,
    /// (false, defaultOfTarget) on failure. Empty/null input returns
    /// (true, null) for nullable targets and (false, default) otherwise.
    /// </summary>
    internal static (bool Success, object? Value) ParseNumericValue(string? input, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        var isNullable = underlying != null;
        var effectiveType = underlying ?? targetType;

        if (string.IsNullOrEmpty(input))
        {
            return isNullable
                ? (true, null)
                : (false, GetDefaultForType(effectiveType));
        }

        // Numeric parse uses CurrentCulture because user-typed numeric input
        // respects local formatting (e.g. "3,14" on de-DE). The Date branch in
        // TryParseCellValue above uses InvariantCulture because pasted dates
        // come from the V04.4 code-formatted data-raw-value attribute which
        // is also emitted with InvariantCulture, so paste must round-trip in
        // that culture regardless of the user's locale.
        if (!decimal.TryParse(input, NumberStyles.Any,
                              CultureInfo.CurrentCulture, out var parsed))
        {
            return (false, GetDefaultForType(effectiveType));
        }

        try
        {
            var converted = Convert.ChangeType(parsed, effectiveType);
            return (true, converted);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return (false, GetDefaultForType(effectiveType));
        }
    }

    private static object? GetDefaultForType(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
