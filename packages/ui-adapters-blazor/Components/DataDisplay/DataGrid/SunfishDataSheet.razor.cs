using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models.DataSheet;
using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// A spreadsheet-like, inline-editable data grid with bulk-save, cell-level
/// dirty tracking, and Excel-style keyboard/paste UX.
/// </summary>
public partial class SunfishDataSheet<TItem> : SunfishComponentBase
{
    // ── Internal state ─────────────────────────────────────────────────
    internal readonly List<SunfishDataSheetColumn<TItem>> _columns = [];
    internal List<TItem> _displayRows = [];
    internal readonly HashSet<TItem> _selectedRows = [];
    internal string _ariaAnnouncement = "";

    // ── Parameters: Data Binding ────────────────────────────────────────

    /// <summary>Client-side data source.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>Property name used as row key for dirty tracking. Defaults to "Id".</summary>
    [Parameter] public string KeyField { get; set; } = "Id";

    // ── Parameters: Events ──────────────────────────────────────────────

    /// <summary>Fires with dirty rows when Save All is confirmed.</summary>
    [Parameter] public EventCallback<DataSheetSaveArgs<TItem>> OnSaveAll { get; set; }

    /// <summary>Fires after each cell commit.</summary>
    [Parameter] public EventCallback<DataSheetRowChangedArgs<TItem>> OnRowChanged { get; set; }

    /// <summary>Fires before Save All; handler may add errors to args to block save.</summary>
    [Parameter] public EventCallback<DataSheetValidateArgs<TItem>> OnValidate { get; set; }

    // ── Parameters: State ───────────────────────────────────────────────

    /// <summary>Shows saving spinner; disables Save All button.</summary>
    [Parameter] public bool IsSaving { get; set; }

    /// <summary>Shows "+ Add Row" button in toolbar.</summary>
    [Parameter] public bool AllowAddRow { get; set; }

    /// <summary>Shows delete button per row and in bulk action bar.</summary>
    [Parameter] public bool AllowDeleteRow { get; set; }

    /// <summary>Enables Ctrl+V TSV paste into cell range.</summary>
    [Parameter] public bool AllowBulkPaste { get; set; } = true;

    /// <summary>Text shown when Data is empty.</summary>
    [Parameter] public string EmptyStateMessage { get; set; } = "No data.";

    /// <summary>Container height. Enables vertical scroll + sticky header.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Shows loading skeleton rows.</summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>Use Virtualize for row rendering. Defaults to true.</summary>
    [Parameter] public bool EnableVirtualization { get; set; } = true;

    /// <summary>Accessible label on role="grid" element.</summary>
    [Parameter] public string AriaLabel { get; set; } = "Editable data grid";

    // ── Parameters: Templates ───────────────────────────────────────────

    /// <summary>SunfishDataSheetColumn definitions.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Additional toolbar content (renders inside toolbar area).</summary>
    [Parameter] public RenderFragment? ToolbarTemplate { get; set; }

    // ── Column Registry ─────────────────────────────────────────────────

    internal void RegisterColumn(SunfishDataSheetColumn<TItem> column)
    {
        if (!_columns.Contains(column))
        {
            _columns.Add(column);
            StateHasChanged();
        }
    }

    internal void UnregisterColumn(SunfishDataSheetColumn<TItem> column)
    {
        _columns.Remove(column);
        StateHasChanged();
    }

    // ── Computed ────────────────────────────────────────────────────────

    private string? _rootStyle => Height != null ? null : null;

    private string? _contentStyle => Height != null ? $"max-height:{Height};overflow:auto;" : null;

    internal int _totalColumnCount =>
        _columns.Count
        + (AllowDeleteRow ? 2 : 0); // checkbox + actions columns

    // ── Lifecycle ───────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (Data != null)
        {
            _displayRows = Data.ToList();
        }
        else
        {
            _displayRows = [];
        }
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Returns the current dirty row snapshot.</summary>
    public IReadOnlyList<TItem> GetDirtyRows()
    {
        return _dirtyRows
            .Where(kv => kv.Value.DirtyFields.Count > 0 && !kv.Value.IsDeleted)
            .Select(kv => kv.Value.Current)
            .ToList();
    }

    /// <summary>Replaces the dataset without full re-render.</summary>
    public Task SetDataAsync(IEnumerable<TItem> data)
    {
        _displayRows = data.ToList();
        _dirtyRows.Clear();
        ClearActiveCell();
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ── Selection ───────────────────────────────────────────────────────

    internal void OnSelectAllChanged(ChangeEventArgs e)
    {
        var selectAll = e.Value is true or "true";
        if (selectAll)
        {
            foreach (var row in _displayRows)
                _selectedRows.Add(row);
        }
        else
        {
            _selectedRows.Clear();
        }
    }

    internal void ToggleRowSelection(TItem row)
    {
        if (!_selectedRows.Remove(row))
            _selectedRows.Add(row);
    }

    internal async Task BulkDeleteAsync()
    {
        foreach (var row in _selectedRows.ToList())
        {
            MarkRowDeleted(row);
        }
        _selectedRows.Clear();
        StateHasChanged();
        await Task.CompletedTask;
    }

    internal async Task BulkResetAsync()
    {
        // V02.2 / V05.1 — Re-entrancy guard. BulkResetAsync mutates the
        // same _dirtyRows dictionary that SaveAllAsync walks during its
        // Step 7 Saved-indicator window; interleaving the two would stomp
        // on rows the user is in the middle of saving. If a save is in
        // progress, drop the reset silently — the caller can retry once
        // the save settles.
        if (_isSaving) return;

        // V05.5 — Restore each selected row's dirty fields to their
        // original values before dropping the dirty entry. The previous
        // implementation removed the entry from _dirtyRows but left the
        // edited property values on the live TItem instance, so the UI
        // reported Pristine while the data still held the edits.
        // F2.M1 — Restore/remove body shared with ResetAsync via the
        // RestoreEntryOrRemoveNewRow helper on the Data partial.
        foreach (var row in _selectedRows.ToList())
        {
            var key = GetRowKey(row);
            if (key == null || !_dirtyRows.TryGetValue(key, out var entry))
            {
                continue;
            }

            RestoreEntryOrRemoveNewRow(entry);
            _dirtyRows.Remove(key);
        }
        _selectedRows.Clear();
        StateHasChanged();
        await Task.CompletedTask;
    }

    internal async Task AddRowAsync()
    {
        if (!AllowAddRow) return;
        var newItem = Activator.CreateInstance<TItem>();
        _displayRows.Insert(0, newItem);

        // V05.3 — Track the new row as dirty so it survives Save All and
        // shows up in DataSheetSaveArgs.DirtyRows. Without this, calling
        // Save All after Add Row loses the new row entirely. The
        // IsNewlyAdded flag lets ResetAsync distinguish "remove the row"
        // from "revert the row's fields".
        var key = GetRowKey(newItem);
        if (key != null)
        {
            var entry = new DirtyRowEntry<TItem>
            {
                Original = Sunfish.Foundation.Helpers.GridReflectionHelper.DeepClone(newItem),
                Current = newItem,
                IsNewlyAdded = true
            };

            // Seed DirtyFields with every editable, non-computed column so
            // GetDirtyRows()/SaveAllAsync treat the new row as dirty.
            // F2.M2 — Invariant: this seed is only preserved across subsequent
            // CommitCellEdit calls because CommitCellEdit guards on
            // `!entry.IsNewlyAdded` before dropping fields that revert to their
            // Original snapshot (see SunfishDataSheet.Data.cs:106). Without that
            // guard, the first user edit would see newValue == Original default
            // and strip the field out, leaving the new row untracked.
            foreach (var column in _columns)
            {
                if (column.Editable && column.ColumnType != DataSheetColumnType.Computed)
                {
                    entry.DirtyFields.Add(column.Field);
                }
            }

            _dirtyRows[key] = entry;
        }

        // SA-03: Activate the first editable, non-computed column on
        // the new row so the user can immediately start typing.
        var firstEditableCol = _columns.FirstOrDefault(c =>
            c.Editable && c.ColumnType != DataSheetColumnType.Computed);
        if (firstEditableCol != null)
        {
            ActivateCell(newItem, firstEditableCol.Field);
        }

        StateHasChanged();
        await Task.CompletedTask;
    }
}
