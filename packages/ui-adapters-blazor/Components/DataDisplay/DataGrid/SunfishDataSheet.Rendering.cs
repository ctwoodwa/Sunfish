using System.Globalization;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Helpers;
using Sunfish.Foundation.Models.DataSheet;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// RenderTreeBuilder helpers, row/cell class computation for SunfishDataSheet.
/// </summary>
public partial class SunfishDataSheet<TItem>
{
    // ── Row Rendering ──────────────────────────────────────────────────

    internal RenderFragment RenderRow(TItem row) => builder =>
    {
        var isDirty = IsRowDirty(row);
        var isSelected = _selectedRows.Contains(row);
        var isDeleted = IsRowDeleted(row);
        var rowKey = GetRowKey(row);
        var rowClass = CssProvider.DataSheetRowClass(isDirty, isSelected, isDeleted);

        // V07.5 — aria-rowindex is 1-based and includes the header row, so
        // data rows start at 2. IndexOf is O(n) but acceptable for grid sizes
        // that don't already need Virtualize; the virtualized path pays the
        // same cost per rendered row regardless.
        var dataRowPosition = _displayRows.IndexOf(row);
        var ariaRowIndex = dataRowPosition >= 0 ? dataRowPosition + 2 : 2;

        builder.OpenElement(0, "tr");
        builder.AddAttribute(1, "class", rowClass);
        builder.AddAttribute(2, "role", "row");
        builder.AddAttribute(3, "aria-rowindex", ariaRowIndex);
        if (rowKey != null) builder.AddAttribute(4, "data-row-key", rowKey.ToString());
        if (isDeleted) builder.AddAttribute(5, "aria-hidden", "true");

        // V07.6 — aria-colindex is 1-based. When AllowDeleteRow adds a
        // leading select checkbox column, that column occupies colindex 1
        // and the first data column starts at 2; otherwise the first data
        // column is colindex 1.
        var nextAriaColIndex = 1;

        // Checkbox column
        if (AllowDeleteRow)
        {
            var cbRow = row;
            builder.OpenElement(10, "td");
            builder.AddAttribute(11, "role", "gridcell");
            builder.AddAttribute(12, "aria-colindex", nextAriaColIndex++);
            builder.AddAttribute(13, "class", CssProvider.DataSheetSelectCellClass());
            builder.OpenElement(14, "input");
            builder.AddAttribute(15, "type", "checkbox");
            builder.AddAttribute(16, "checked", isSelected);
            builder.AddAttribute(17, "aria-label", "Select row");
            builder.AddAttribute(18, "onchange", EventCallback.Factory.Create(this, () => ToggleRowSelection(cbRow)));
            builder.CloseElement(); // input
            builder.CloseElement(); // td
        }

        // Data cells
        foreach (var column in _columns)
        {
            var cellRow = row;
            var cellField = column.Field;
            var cellState = GetCellState(row, column.Field);
            var isActive = IsCellActive(row, column.Field);
            var isEditing = IsCellEditing(row, column.Field);
            var cellClass = CssProvider.DataSheetCellClass(cellState, isActive, column.Editable);
            var cellError = GetCellError(row, column.Field);
            var cellWidth = column.Width != null ? $"width:{column.Width};" :
                            column.MinWidth.HasValue ? $"min-width:{column.MinWidth}px;" : null;

            // V07.7 — Screen readers need a programmatic link between the
            // cell and its validation error, not just a `title` tooltip.
            // Emit a visually-hidden span with a deterministic ID and point
            // aria-describedby at it when the cell is invalid.
            //
            // V07.7 polish — rowKey is user-supplied and may contain spaces,
            // quotes, `#`, or other characters that are invalid in an HTML
            // `id` attribute or CSS id selector. Sanitize it so the id and
            // the cell's aria-describedby both produce the same safe value.
            // _gridId is a GUID ("N") and column.Field is a C# identifier;
            // neither needs sanitization.
            var cellErrorId = cellError != null && rowKey != null
                ? $"{_gridId}-err-{SanitizeIdPart(rowKey)}-{column.Field}"
                : null;

            builder.OpenElement(20, "td");
            builder.AddAttribute(21, "class", cellClass);
            builder.AddAttribute(22, "role", "gridcell");
            builder.AddAttribute(23, "aria-colindex", nextAriaColIndex++);
            if (cellWidth != null) builder.AddAttribute(24, "style", cellWidth);
            if (!column.Editable || column.ColumnType == DataSheetColumnType.Computed)
                builder.AddAttribute(25, "aria-readonly", "true");
            if (cellState == CellState.Invalid)
                builder.AddAttribute(26, "aria-invalid", "true");
            if (cellError != null)
                builder.AddAttribute(27, "title", cellError);
            if (cellErrorId != null)
                builder.AddAttribute(28, "aria-describedby", cellErrorId);
            builder.AddAttribute(29, "data-field", column.Field);

            // V04.4 — When the column defines a Format delegate, the cell's
            // textContent is the formatted display string (e.g. "$42.00"),
            // which would round-trip poorly through copy/paste. Emit
            // data-raw-value carrying the underlying property value formatted
            // via InvariantCulture so the JS copy handler can read the raw
            // value instead of the display string. When Format is null,
            // textContent already equals the raw value and the attribute is
            // omitted to avoid DOM bloat on every cell.
            if (column.Format != null && column.ColumnType != DataSheetColumnType.Computed)
            {
                var rawValue = GridReflectionHelper.GetValue(row, column.Field);
                var rawValueAttr = Convert.ToString(rawValue, CultureInfo.InvariantCulture) ?? "";
                builder.AddAttribute(30, "data-raw-value", rawValueAttr);
            }

            // Click handler
            builder.AddAttribute(31, "onclick",
                EventCallback.Factory.Create<MouseEventArgs>(this, (_) => OnCellClick(cellRow, cellField)));

            // SA-09 — Double-click enters edit mode directly per spec
            // editing-and-validation.md "Double-click on any cell".
            builder.AddAttribute(32, "ondblclick",
                EventCallback.Factory.Create<MouseEventArgs>(this, (_) => OnCellDoubleClick(cellRow, cellField)));

            // Cell content
            if (column.CellTemplate != null && !isEditing)
            {
                var context = new DataSheetCellContext<TItem>
                {
                    Item = row,
                    Field = column.Field,
                    Value = GridReflectionHelper.GetValue(row, column.Field),
                    IsEditing = isEditing,
                    IsDirty = cellState == CellState.Dirty,
                    ValidationError = cellError
                };
                builder.AddContent(35, column.CellTemplate(context));
            }
            else if (isEditing)
            {
                RenderCellEditor(builder, row, column);
            }
            else
            {
                RenderCellDisplay(builder, row, column);
            }

            // V07.7 — Render the error message in a visually-hidden span
            // inside the cell. The span's ID is the target of the cell's
            // aria-describedby attribute so screen readers associate the
            // error with the cell without requiring mouse hover.
            if (cellErrorId != null)
            {
                builder.OpenElement(36, "span");
                builder.AddAttribute(37, "id", cellErrorId);
                builder.AddAttribute(38, "class", CssProvider.DataSheetScreenReaderOnlyClass());
                builder.AddAttribute(39, "style",
                    "position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0;");
                builder.AddContent(40, cellError);
                builder.CloseElement(); // span
            }

            builder.CloseElement(); // td
        }

        // Delete action column
        if (AllowDeleteRow)
        {
            var delRow = row;
            builder.OpenElement(90, "td");
            builder.AddAttribute(91, "role", "gridcell");
            builder.AddAttribute(92, "aria-colindex", nextAriaColIndex++);
            builder.AddAttribute(93, "class", CssProvider.DataSheetActionsCellClass());
            builder.OpenElement(100, "button");
            builder.AddAttribute(101, "type", "button");
            builder.AddAttribute(102, "class", CssProvider.DataSheetDeleteButtonClass());
            builder.AddAttribute(103, "aria-label", "Delete row");
            builder.AddAttribute(104, "onclick",
                EventCallback.Factory.Create<MouseEventArgs>(this, (_) => MarkRowDeleted(delRow)));
            builder.AddContent(105, "\u2715"); // X symbol
            builder.CloseElement(); // button
            builder.CloseElement(); // td
        }

        builder.CloseElement(); // tr
    };

    // ── Cell Display ───────────────────────────────────────────────────

    private void RenderCellDisplay(RenderTreeBuilder builder, TItem row, SunfishDataSheetColumn<TItem> column)
    {
        var value = GridReflectionHelper.GetValue(row, column.Field);

        switch (column.ColumnType)
        {
            case DataSheetColumnType.Checkbox:
                builder.OpenElement(40, "input");
                builder.AddAttribute(41, "type", "checkbox");
                builder.AddAttribute(42, "checked", value is true);
                builder.AddAttribute(43, "disabled", !column.Editable);
                builder.AddAttribute(44, "aria-label", column.DisplayTitle);
                builder.CloseElement();
                break;

            case DataSheetColumnType.Computed:
                var formatted = column.Format != null ? column.Format(row) : value?.ToString() ?? "";
                builder.AddContent(40, formatted);
                break;

            default:
                var display = column.Format != null ? column.Format(row) : value?.ToString() ?? "";
                builder.OpenElement(40, "span");
                builder.AddAttribute(41, "class", CssProvider.DataSheetCellTextClass());
                builder.AddContent(42, display);
                builder.CloseElement();
                break;
        }
    }

    // ── Cell Editor ────────────────────────────────────────────────────

    private void RenderCellEditor(RenderTreeBuilder builder, TItem row, SunfishDataSheetColumn<TItem> column)
    {
        var value = GridReflectionHelper.GetValue(row, column.Field);
        var editRow = row;
        var editField = column.Field;

        switch (column.ColumnType)
        {
            case DataSheetColumnType.Text:
                builder.OpenElement(50, "input");
                builder.AddAttribute(51, "type", "text");
                builder.AddAttribute(52, "class", CssProvider.DataSheetEditorInputClass());
                builder.AddAttribute(53, "value", value?.ToString() ?? "");
                builder.AddAttribute(54, "aria-label", $"Edit {column.DisplayTitle}");
                builder.AddAttribute(55, "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this,
                        (e) => OnCellValueCommit(editRow, editField, e.Value)));
                builder.CloseElement();
                break;

            case DataSheetColumnType.Number:
                var numberTargetType = typeof(TItem).GetProperty(column.Field)?.PropertyType
                                       ?? typeof(decimal);
                builder.OpenElement(50, "input");
                builder.AddAttribute(51, "type", "number");
                builder.AddAttribute(52, "class", CssProvider.DataSheetEditorInputClass());
                builder.AddAttribute(53, "value", value?.ToString() ?? "0");
                builder.AddAttribute(54, "aria-label", $"Edit {column.DisplayTitle}");
                builder.AddAttribute(55, "step", "any");
                builder.AddAttribute(56, "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this,
                        (e) =>
                        {
                            var (_, parsed) = ParseNumericValue(e.Value?.ToString(), numberTargetType);
                            return OnCellValueCommit(editRow, editField, parsed);
                        }));
                builder.CloseElement();
                break;

            case DataSheetColumnType.Date:
                builder.OpenElement(50, "input");
                builder.AddAttribute(51, "type", "date");
                builder.AddAttribute(52, "class", CssProvider.DataSheetEditorInputClass());
                builder.AddAttribute(53, "value", value is DateTime dt ? dt.ToString("yyyy-MM-dd") : "");
                builder.AddAttribute(54, "aria-label", $"Edit {column.DisplayTitle}");
                builder.AddAttribute(55, "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this,
                        (e) =>
                        {
                            DateTime.TryParse(e.Value?.ToString(), out var parsed);
                            return OnCellValueCommit(editRow, editField, parsed == default ? null : (object)parsed);
                        }));
                builder.CloseElement();
                break;

            case DataSheetColumnType.Select:
                builder.OpenElement(50, "select");
                builder.AddAttribute(51, "class", CssProvider.DataSheetEditorSelectClass());
                builder.AddAttribute(52, "value", value?.ToString() ?? "");
                builder.AddAttribute(53, "aria-label", $"Edit {column.DisplayTitle}");
                builder.AddAttribute(54, "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this,
                        (e) => OnCellValueCommit(editRow, editField, e.Value)));

                if (column.Options != null)
                {
                    foreach (var option in column.Options)
                    {
                        builder.OpenElement(60, "option");
                        builder.AddAttribute(61, "value", option.Value);
                        builder.AddContent(62, option.Label);
                        builder.CloseElement();
                    }
                }
                builder.CloseElement();
                break;

            case DataSheetColumnType.Checkbox:
                builder.OpenElement(50, "input");
                builder.AddAttribute(51, "type", "checkbox");
                builder.AddAttribute(52, "checked", value is true);
                builder.AddAttribute(53, "aria-label", $"Edit {column.DisplayTitle}");
                builder.AddAttribute(54, "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this,
                        (e) => OnCellValueCommit(editRow, editField, e.Value is true or "true")));
                builder.CloseElement();
                break;
        }
    }

    // ── ID Sanitization ────────────────────────────────────────────────

    /// <summary>
    /// Sanitizes a value for safe inclusion in an HTML <c>id</c> attribute
    /// and CSS id selector. Any character outside <c>[A-Za-z0-9_-]</c> is
    /// replaced with <c>_</c>. Null becomes <c>"null"</c> and empty becomes
    /// <c>"empty"</c> so the result is always non-empty. Used to sanitize
    /// user-supplied row keys when building <c>aria-describedby</c> targets
    /// for invalid cells (V07.7 polish).
    /// </summary>
    private static string SanitizeIdPart(object? value)
    {
        if (value is null) return "null";
        var s = value.ToString();
        if (string.IsNullOrEmpty(s)) return "empty";

        var buffer = new char[s.Length];
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            var isSafe = (c >= 'A' && c <= 'Z')
                      || (c >= 'a' && c <= 'z')
                      || (c >= '0' && c <= '9')
                      || c == '_'
                      || c == '-';
            buffer[i] = isSafe ? c : '_';
        }
        return new string(buffer);
    }
}
