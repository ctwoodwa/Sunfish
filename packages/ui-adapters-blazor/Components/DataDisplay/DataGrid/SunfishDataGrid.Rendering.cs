using Sunfish.Foundation.Data;
using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Row, cell, and fragment rendering logic for SunfishDataGrid.
/// </summary>
public partial class SunfishDataGrid<TItem>
{
    // ── Data Row Rendering ──────────────────────────────────────────────

    internal RenderFragment RenderDataRow(TItem item, int index) => builder =>
    {
        var isSelected = _selectedItems.Contains(item);
        var isStripedRow = Striped && index % 2 == 1;
        var isEditing = IsItemEditing(item);
        var rowClass = CssProvider.DataGridRowClass(isSelected, isStripedRow);
        var rowRenderArgs = GetRowRenderArgs(item);
        var finalRowClass = rowRenderArgs?.Class != null ? $"{rowClass} {rowRenderArgs.Class}" : rowClass;
        if (isEditing) finalRowClass += " mar-datagrid-row--editing";
        var rowStyle = rowRenderArgs?.Style;

        builder.OpenElement(0, "tr");
        builder.AddAttribute(1, "class", finalRowClass);
        builder.AddAttribute(2, "role", "row");
        if (rowStyle != null) builder.AddAttribute(3, "style", rowStyle);
        if (isSelected) builder.AddAttribute(4, "aria-selected", "true");
        builder.AddAttribute(5, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, (e) => HandleRowClick(item, e)));
        builder.AddAttribute(6, "ondblclick", EventCallback.Factory.Create<MouseEventArgs>(this, (e) => HandleRowDoubleClick(item, e)));
        builder.AddAttribute(7, "oncontextmenu", EventCallback.Factory.Create<MouseEventArgs>(this, (e) => HandleRowContextMenu(item, e)));

        // Drag handle cell
        if (RowDraggable)
        {
            builder.OpenElement(8, "td");
            builder.AddAttribute(9, "class", "mar-datagrid-drag-cell");
            builder.AddAttribute(10, "role", "gridcell");
            builder.AddAttribute(11, "draggable", "true");
            builder.AddAttribute(12, "data-row-index", index.ToString());
            builder.AddContent(13, "\u2807");
            builder.CloseElement(); // td
        }

        // Detail expand/collapse button
        if (DetailTemplate != null)
        {
            var detailItem = item;
            var isExpanded = _expandedDetailItems.Contains(item);
            builder.OpenElement(20, "td");
            builder.AddAttribute(21, "class", "mar-datagrid-detail-cell");
            builder.AddAttribute(22, "role", "gridcell");
            builder.OpenElement(23, "button");
            builder.AddAttribute(24, "type", "button");
            builder.AddAttribute(25, "class", "mar-datagrid-detail-btn");
            builder.AddAttribute(26, "aria-label", isExpanded ? "Collapse detail" : "Expand detail");
            builder.AddAttribute(27, "aria-expanded", isExpanded.ToString().ToLower());
            builder.AddAttribute(28, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await ToggleDetailRow(detailItem)));
            builder.AddEventStopPropagationAttribute(29, "onclick", true);
            builder.AddContent(30, isExpanded ? "\u25BC" : "\u25B6");
            builder.CloseElement(); // button
            builder.CloseElement(); // td
        }

        // Checkbox cell
        if (ShowCheckboxColumn)
        {
            var cbItem = item;
            builder.OpenElement(30, "td");
            builder.AddAttribute(31, "class", "mar-datagrid-checkbox-cell");
            builder.AddAttribute(32, "role", "gridcell");
            builder.OpenElement(33, "input");
            builder.AddAttribute(34, "type", "checkbox");
            builder.AddAttribute(35, "checked", isSelected);
            builder.AddAttribute(36, "aria-label", "Select row");
            builder.AddEventStopPropagationAttribute(37, "onclick", true);
            builder.AddAttribute(38, "onchange", EventCallback.Factory.Create(this, () => OnCheckboxToggle(cbItem)));
            builder.CloseElement(); // input
            builder.CloseElement(); // td
        }

        foreach (var column in _visibleColumns)
        {
            var cellRenderArgs = GetCellRenderArgs(column, item);
            var cellClass = CssProvider.DataGridCellClass();
            if (cellRenderArgs?.Class != null) cellClass = $"{cellClass} {cellRenderArgs.Class}";
            if (IsCellSelected(index, column.Field)) cellClass = $"{cellClass} mar-datagrid-cell--selected";
            var frozenCellStyle = GetFrozenCellStyle(column);
            var combinedExtra = string.Join("", new[] { cellRenderArgs?.Style, frozenCellStyle }.Where(s => !string.IsNullOrEmpty(s)));
            var finalCellStyle = GetColumnCellStyle(column, string.IsNullOrEmpty(combinedExtra) ? null : combinedExtra);

            builder.OpenElement(50, "td");
            builder.AddAttribute(51, "class", cellClass);
            builder.AddAttribute(52, "role", "gridcell");
            if (finalCellStyle != null) builder.AddAttribute(53, "style", finalCellStyle);

            // Cell selection: click handler (stop propagation so row click doesn't also fire)
            if (SelectionUnit == GridSelectionUnit.Cell && SelectionMode != GridSelectionMode.None)
            {
                var cellSelCol = column;
                var cellSelItem = item;
                var cellSelIndex = index;
                builder.AddAttribute(54, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await HandleCellClick(cellSelItem, cellSelCol.Field, cellSelIndex)));
                builder.AddEventStopPropagationAttribute(55, "onclick", true);
            }

            // InCell: click to edit a specific cell
            if (EditMode == GridEditMode.InCell && !isEditing && column.Editable)
            {
                var cellCol = column;
                var cellItem = item;
                builder.AddAttribute(56, "ondblclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await BeginCellEdit(cellItem, cellCol.Field)));
                builder.AddEventStopPropagationAttribute(57, "ondblclick", true);
            }

            // Determine what to render in the cell
            if (EditMode == GridEditMode.InCell && IsCellEditing(item, column.Field) && column.Editable && column.EditorTemplate != null)
            {
                // InCell: only the focused cell shows editor
                builder.AddContent(60, column.EditorTemplate(item));
                // InCell: inline save/cancel buttons
                builder.OpenElement(61, "div");
                builder.AddAttribute(62, "class", "mar-datagrid-incell-actions");
                builder.OpenElement(63, "button");
                builder.AddAttribute(64, "type", "button");
                builder.AddAttribute(65, "class", "mar-datagrid-cmd-btn mar-datagrid-cmd-btn--sm");
                builder.AddAttribute(66, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await SaveEdit()));
                builder.AddEventStopPropagationAttribute(67, "onclick", true);
                builder.AddContent(68, "\u2713"); // checkmark
                builder.CloseElement();
                builder.OpenElement(69, "button");
                builder.AddAttribute(70, "type", "button");
                builder.AddAttribute(71, "class", "mar-datagrid-cmd-btn mar-datagrid-cmd-btn--sm");
                builder.AddAttribute(72, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await CancelEdit()));
                builder.AddEventStopPropagationAttribute(73, "onclick", true);
                builder.AddContent(74, "\u2717"); // X mark
                builder.CloseElement();
                builder.CloseElement(); // div
            }
            else if (isEditing && EditMode == GridEditMode.Inline && column.Editable && column.EditorTemplate != null)
            {
                // Inline: all cells in row show editors
                builder.AddContent(60, column.EditorTemplate(item));
            }
            else if (column.Template != null)
            {
                builder.AddContent(60, column.Template(item));
            }
            else
            {
                builder.AddContent(60, column.GetDisplayValue(item));
            }

            builder.CloseElement(); // td
        }

        // Command cell (Inline and Popup modes only; InCell handles per-cell)
        if (EditMode != GridEditMode.None && EditMode != GridEditMode.InCell)
        {
            builder.OpenElement(80, "td");
            builder.AddAttribute(81, "class", "mar-datagrid-command-cell");
            builder.AddAttribute(82, "role", "gridcell");

            if (isEditing && EditMode == GridEditMode.Inline)
            {
                builder.OpenElement(82, "button");
                builder.AddAttribute(83, "type", "button");
                builder.AddAttribute(84, "class", "mar-datagrid-cmd-btn");
                builder.AddAttribute(85, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await SaveEdit()));
                builder.AddContent(87, "Save");
                builder.CloseElement();

                builder.OpenElement(88, "button");
                builder.AddAttribute(89, "type", "button");
                builder.AddAttribute(90, "class", "mar-datagrid-cmd-btn");
                builder.AddAttribute(91, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await CancelEdit()));
                builder.AddEventStopPropagationAttribute(92, "onclick", true);
                builder.AddContent(93, "Cancel");
                builder.CloseElement();
            }
            else
            {
                // Not editing: Edit/Delete buttons
                var editItem = item;
                builder.OpenElement(82, "button");
                builder.AddAttribute(83, "type", "button");
                builder.AddAttribute(84, "class", "mar-datagrid-cmd-btn");
                builder.AddAttribute(85, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await BeginEdit(editItem)));
                builder.AddEventStopPropagationAttribute(86, "onclick", true);
                builder.AddContent(87, "Edit");
                builder.CloseElement();

                builder.OpenElement(88, "button");
                builder.AddAttribute(89, "type", "button");
                builder.AddAttribute(90, "class", "mar-datagrid-cmd-btn mar-datagrid-cmd-btn--delete");
                builder.AddAttribute(91, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await DeleteItem(editItem)));
                builder.AddEventStopPropagationAttribute(92, "onclick", true);
                builder.AddContent(93, "Delete");
                builder.CloseElement();
            }

            builder.CloseElement(); // td
        }

        builder.CloseElement(); // tr
    };

    // ── Inline Edit Row (for new items in Inline mode) ──────────────────

    internal RenderFragment RenderEditRow(TItem item) => builder =>
    {
        builder.OpenElement(0, "tr");
        builder.AddAttribute(1, "class", "mar-datagrid-row--editing mar-datagrid-row--new");
        builder.AddAttribute(2, "role", "row");

        if (RowDraggable)
        {
            builder.OpenElement(3, "td");
            builder.AddAttribute(4, "role", "gridcell");
            builder.CloseElement();
        }

        if (DetailTemplate != null)
        {
            builder.OpenElement(3, "td");
            builder.AddAttribute(4, "role", "gridcell");
            builder.CloseElement();
        }

        if (ShowCheckboxColumn)
        {
            builder.OpenElement(5, "td");
            builder.AddAttribute(6, "role", "gridcell");
            builder.CloseElement();
        }

        foreach (var column in _visibleColumns)
        {
            builder.OpenElement(10, "td");
            builder.AddAttribute(11, "class", CssProvider.DataGridCellClass());
            builder.AddAttribute(12, "role", "gridcell");
            var frozenStyle = GetFrozenCellStyle(column);
            var editRowCombinedStyle = GetColumnCellStyle(column, frozenStyle);
            if (!string.IsNullOrEmpty(editRowCombinedStyle)) builder.AddAttribute(13, "style", editRowCombinedStyle);
            if (column.EditorTemplate != null)
            {
                builder.AddContent(14, column.EditorTemplate(item));
            }
            else
            {
                builder.AddContent(14, column.GetDisplayValue(item));
            }
            builder.CloseElement();
        }

        // Command cell
        builder.OpenElement(20, "td");
        builder.AddAttribute(21, "class", "mar-datagrid-command-cell");
        builder.AddAttribute(22, "role", "gridcell");

        builder.OpenElement(23, "button");
        builder.AddAttribute(24, "type", "button");
        builder.AddAttribute(25, "class", "mar-datagrid-cmd-btn");
        builder.AddAttribute(26, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await SaveEdit()));
        builder.AddContent(27, "Save");
        builder.CloseElement();

        builder.OpenElement(28, "button");
        builder.AddAttribute(29, "type", "button");
        builder.AddAttribute(30, "class", "mar-datagrid-cmd-btn");
        builder.AddAttribute(31, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await CancelEdit()));
        builder.AddContent(32, "Cancel");
        builder.CloseElement();

        builder.CloseElement(); // td
        builder.CloseElement(); // tr
    };

    // ── FilterMenu Rendering ────────────────────────────────────────────

    internal RenderFragment RenderFilterMenu() => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "mar-datagrid-filter-menu");

        // Operator dropdown
        builder.OpenElement(10, "label");
        builder.AddContent(11, "Operator:");
        builder.CloseElement();

        builder.OpenElement(12, "select");
        builder.AddAttribute(13, "class", "mar-datagrid-filter-menu-operator");
        builder.AddAttribute(14, "value", _filterMenuOperator.ToString());
        builder.AddAttribute(15, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, (e) =>
        {
            if (Enum.TryParse<FilterOperator>(e.Value?.ToString(), out var op))
                _filterMenuOperator = op;
        }));

        var operators = new[]
        {
            (FilterOperator.Contains, "Contains"),
            (FilterOperator.Equals, "Equals"),
            (FilterOperator.NotEquals, "Not Equals"),
            (FilterOperator.StartsWith, "Starts With"),
            (FilterOperator.EndsWith, "Ends With"),
            (FilterOperator.GreaterThan, "Greater Than"),
            (FilterOperator.GreaterThanOrEqual, "Greater or Equal"),
            (FilterOperator.LessThan, "Less Than"),
            (FilterOperator.LessThanOrEqual, "Less or Equal"),
            (FilterOperator.IsNull, "Is Null"),
            (FilterOperator.IsNotNull, "Is Not Null")
        };

        foreach (var (op, label) in operators)
        {
            builder.OpenElement(16, "option");
            builder.AddAttribute(17, "value", op.ToString());
            if (op == _filterMenuOperator) builder.AddAttribute(18, "selected", true);
            builder.AddContent(19, label);
            builder.CloseElement();
        }
        builder.CloseElement(); // select

        // Value input (hidden for IsNull/IsNotNull)
        if (_filterMenuOperator != FilterOperator.IsNull && _filterMenuOperator != FilterOperator.IsNotNull)
        {
            builder.OpenElement(20, "input");
            builder.AddAttribute(21, "type", "text");
            builder.AddAttribute(22, "class", "mar-datagrid-filter-menu-value");
            builder.AddAttribute(23, "placeholder", "Filter value...");
            builder.AddAttribute(24, "value", _filterMenuValue);
            builder.AddAttribute(25, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, (e) =>
            {
                _filterMenuValue = e.Value?.ToString() ?? "";
            }));
            builder.CloseElement();
        }

        // Apply / Clear buttons
        builder.OpenElement(30, "div");
        builder.AddAttribute(31, "class", "mar-datagrid-filter-menu-actions");

        builder.OpenElement(32, "button");
        builder.AddAttribute(33, "type", "button");
        builder.AddAttribute(34, "class", "mar-datagrid-cmd-btn");
        builder.AddAttribute(35, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await ApplyFilterMenu()));
        builder.AddContent(36, "Apply");
        builder.CloseElement();

        builder.OpenElement(37, "button");
        builder.AddAttribute(38, "type", "button");
        builder.AddAttribute(39, "class", "mar-datagrid-cmd-btn");
        builder.AddAttribute(40, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await ClearFilterMenu()));
        builder.AddContent(41, "Clear");
        builder.CloseElement();

        builder.CloseElement(); // div actions
        builder.CloseElement(); // div filter-menu
    };

    internal RenderFragment RenderCheckBoxFilterMenu() => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "mar-datagrid-filter-menu mar-datagrid-filter-checkbox");

        // Select All / Deselect All
        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "mar-datagrid-filter-checkbox-actions");

        builder.OpenElement(4, "button");
        builder.AddAttribute(5, "type", "button");
        builder.AddAttribute(6, "class", "mar-datagrid-cmd-btn mar-datagrid-cmd-btn--sm");
        builder.AddAttribute(7, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, (_) =>
        {
            _checkBoxFilterSelected = new HashSet<string>(_checkBoxFilterDistinct);
            StateHasChanged();
        }));
        builder.AddContent(8, "All");
        builder.CloseElement();

        builder.OpenElement(9, "button");
        builder.AddAttribute(10, "type", "button");
        builder.AddAttribute(11, "class", "mar-datagrid-cmd-btn mar-datagrid-cmd-btn--sm");
        builder.AddAttribute(12, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, (_) =>
        {
            _checkBoxFilterSelected.Clear();
            StateHasChanged();
        }));
        builder.AddContent(13, "None");
        builder.CloseElement();

        builder.CloseElement(); // actions div

        // Checkbox list
        builder.OpenElement(20, "div");
        builder.AddAttribute(21, "class", "mar-datagrid-filter-checkbox-list");
        builder.AddAttribute(22, "role", "listbox");
        builder.AddAttribute(23, "aria-label", "Filter values");

        foreach (var value in _checkBoxFilterDistinct)
        {
            var capturedValue = value;
            var isChecked = _checkBoxFilterSelected.Contains(value);

            builder.OpenElement(30, "label");
            builder.AddAttribute(31, "class", "mar-datagrid-filter-checkbox-item");
            builder.AddAttribute(32, "role", "option");
            builder.AddAttribute(33, "aria-selected", isChecked.ToString().ToLowerInvariant());

            builder.OpenElement(34, "input");
            builder.AddAttribute(35, "type", "checkbox");
            builder.AddAttribute(36, "checked", isChecked);
            builder.AddAttribute(37, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, (_) =>
            {
                ToggleCheckBoxValue(capturedValue);
                StateHasChanged();
            }));
            builder.CloseElement();

            builder.AddContent(38, value);
            builder.CloseElement(); // label
        }

        builder.CloseElement(); // checkbox-list div

        // Apply / Clear buttons
        builder.OpenElement(40, "div");
        builder.AddAttribute(41, "class", "mar-datagrid-filter-menu-actions");

        builder.OpenElement(42, "button");
        builder.AddAttribute(43, "type", "button");
        builder.AddAttribute(44, "class", "mar-datagrid-cmd-btn");
        builder.AddAttribute(45, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await ApplyCheckBoxFilter()));
        builder.AddContent(46, "Apply");
        builder.CloseElement();

        builder.OpenElement(47, "button");
        builder.AddAttribute(48, "type", "button");
        builder.AddAttribute(49, "class", "mar-datagrid-cmd-btn");
        builder.AddAttribute(50, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await ClearCheckBoxFilter()));
        builder.AddContent(51, "Clear");
        builder.CloseElement();

        builder.CloseElement(); // actions div
        builder.CloseElement(); // filter-menu div
    };

    // ── Group Row Rendering ───────────────────────────────────────────────

    internal RenderFragment RenderGroupHeaderRow(GridGroupRow<TItem> group) => builder =>
    {
        var isCollapsed = IsGroupCollapsed(group.GroupKey);
        var column = _visibleColumns.FirstOrDefault(c => c.Field == group.Field);
        var displayField = column?.DisplayTitle ?? group.Field;
        var indent = group.Depth * 20;

        builder.OpenElement(0, "tr");
        builder.AddAttribute(1, "class", $"{CssProvider.DataGridGroupHeaderClass()} mar-datagrid-group-depth-{group.Depth}");
        builder.AddAttribute(2, "role", "row");

        builder.OpenElement(3, "td");
        builder.AddAttribute(4, "colspan", TotalColumnCount.ToString());
        builder.AddAttribute(5, "class", "mar-datagrid-group-cell");
        builder.AddAttribute(6, "role", "gridcell");
        if (indent > 0) builder.AddAttribute(7, "style", $"padding-left:{indent}px;");

        // Expand/collapse toggle
        var gk = group.GroupKey;
        builder.OpenElement(10, "button");
        builder.AddAttribute(11, "type", "button");
        builder.AddAttribute(12, "class", "mar-datagrid-group-toggle");
        builder.AddAttribute(13, "aria-expanded", (!isCollapsed).ToString().ToLower());
        builder.AddAttribute(14, "aria-label", isCollapsed ? $"Expand group {group.KeyText}" : $"Collapse group {group.KeyText}");
        builder.AddAttribute(15, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (_) => await ToggleGroup(gk)));
        builder.AddContent(16, isCollapsed ? "\u25B6" : "\u25BC");
        builder.CloseElement(); // button

        // Group content
        if (GroupHeaderTemplate != null)
        {
            var context = new GridGroupHeaderContext<TItem>
            {
                Field = group.Field,
                Value = group.Key,
                Items = group.Items,
                Depth = group.Depth,
                IsCollapsed = isCollapsed
            };
            builder.AddContent(20, GroupHeaderTemplate(context));
        }
        else
        {
            builder.OpenElement(20, "span");
            builder.AddAttribute(21, "class", "mar-datagrid-group-text");
            builder.AddContent(22, $"{displayField}: {group.KeyText} ({group.Count} {(group.Count == 1 ? "item" : "items")})");
            builder.CloseElement();
        }

        builder.CloseElement(); // td
        builder.CloseElement(); // tr
    };

    internal RenderFragment RenderGroupFooterRow(GridGroupRow<TItem> group) => builder =>
    {
        if (GroupFooterTemplate is null) return;

        var context = new GridGroupHeaderContext<TItem>
        {
            Field = group.Field,
            Value = group.Key,
            Items = group.Items,
            Depth = group.Depth,
            IsCollapsed = IsGroupCollapsed(group.GroupKey)
        };

        builder.OpenElement(0, "tr");
        builder.AddAttribute(1, "class", "mar-datagrid-group-footer mar-datagrid-group-depth-" + group.Depth);
        builder.AddAttribute(2, "role", "row");

        builder.OpenElement(3, "td");
        builder.AddAttribute(4, "colspan", TotalColumnCount.ToString());
        builder.AddAttribute(5, "class", "mar-datagrid-group-footer-cell");
        builder.AddAttribute(6, "role", "gridcell");
        builder.AddContent(7, GroupFooterTemplate(context));
        builder.CloseElement(); // td
        builder.CloseElement(); // tr
    };

    internal RenderFragment RenderGroupRows(List<GridGroupRow<TItem>> groups) => builder =>
    {
        foreach (var group in groups)
        {
            // Render group header
            builder.AddContent(0, RenderGroupHeaderRow(group));

            var isCollapsed = IsGroupCollapsed(group.GroupKey);
            if (!isCollapsed)
            {
                if (group.HasChildGroups)
                {
                    // Nested groups
                    builder.AddContent(1, RenderGroupRows(group.ChildGroups));
                }
                else
                {
                    // Leaf group: render data rows
                    for (var i = 0; i < group.Items.Count; i++)
                    {
                        var item = group.Items[i];
                        builder.AddContent(2, RenderDataRow(item, i));

                        // Detail row
                        if (DetailTemplate != null && _expandedDetailItems.Contains(item))
                        {
                            builder.OpenElement(3, "tr");
                            builder.AddAttribute(4, "class", "mar-datagrid-detail-row");
                            builder.AddAttribute(5, "role", "row");
                            builder.OpenElement(6, "td");
                            builder.AddAttribute(7, "colspan", TotalColumnCount.ToString());
                            builder.AddAttribute(8, "role", "gridcell");
                            builder.AddContent(9, DetailTemplate(item));
                            builder.CloseElement();
                            builder.CloseElement();
                        }
                    }
                }

                // Render group footer
                builder.AddContent(10, RenderGroupFooterRow(group));
            }
        }
    };

    // ── Row/Cell Render Callbacks ────────────────────────────────────────

    private GridRowRenderEventArgs<TItem>? GetRowRenderArgs(TItem item)
    {
        if (OnRowRender is null) return null;
        var args = new GridRowRenderEventArgs<TItem> { Item = item };
        OnRowRender(args);
        return args;
    }

    private GridCellRenderEventArgs<TItem>? GetCellRenderArgs(SunfishGridColumn<TItem> column, TItem item)
    {
        if (column.OnCellRender is null) return null;
        var args = new GridCellRenderEventArgs<TItem>
        {
            Item = item,
            Field = column.Field,
            Value = column.GetValue(item)
        };
        column.OnCellRender(args);
        return args;
    }
}
