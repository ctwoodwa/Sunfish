using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// JS interop for column resize, reorder, and keyboard navigation.
/// </summary>
public partial class SunfishDataGrid<TItem> : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<SunfishDataGrid<TItem>>? _dotNetRef;
    private string _gridId = $"mar-grid-{Guid.NewGuid():N}";

    // B0: dedicated ES module for Phase B interactive features (keyboard nav, resize, reorder, DnD, frozen columns).
    // Loaded lazily on first render; B1-B5 populate the feature hooks inside marilo-datagrid.js.
    private IJSObjectReference? _dataGridModule;
    private IJSObjectReference? _dataGridHandle;

    // Column state
    internal List<ColumnState> _columnStates = [];
    internal bool _resizable;
    internal bool _reorderable;

    /// <summary>Whether columns can be resized by dragging. Defaults to false.</summary>
    [Parameter] public bool Resizable { get; set; }

    /// <summary>Whether columns can be reordered by dragging. Defaults to false.</summary>
    [Parameter] public bool Reorderable { get; set; }

    /// <summary>Fires when column order changes.</summary>
    [Parameter] public EventCallback<List<string>> OnColumnReorder { get; set; }

    /// <summary>Fires when a column is resized.</summary>
    [Parameter] public EventCallback<ColumnResizeEventArgs> OnColumnResize { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _resizable = Resizable;
            _reorderable = Reorderable;

            if (_resizable || _reorderable || Navigable || RowDraggable)
            {
                _jsModule = await JS.InvokeAsync<IJSObjectReference>("eval", GetGridScript());
                await _jsModule.InvokeVoidAsync("init", _gridId, _dotNetRef, new
                {
                    resizable = _resizable,
                    reorderable = _reorderable,
                    navigable = Navigable,
                    rowDraggable = RowDraggable
                });
            }

            // B0.2: load the dedicated ES module and attach the Phase B lifecycle handle.
            // Tolerate SSR / pre-rendering where JS is unavailable.
            await AttachDataGridJsAsync();
        }
    }

    /// <summary>
    /// Ensures the <c>marilo-datagrid.js</c> ES module is loaded.
    /// Idempotent — safe to call multiple times; loads only once.
    /// </summary>
    private async Task EnsureDataGridModuleAsync()
    {
        _dataGridModule ??= await JS.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/Sunfish.Components.Blazor/js/marilo-datagrid.js");
    }

    /// <summary>
    /// Loads the datagrid ES module and attaches the JS behavior handle.
    /// Called once from <see cref="OnAfterRenderAsync"/> on first render.
    /// Silently skips if JS is unavailable (SSR / pre-rendering).
    /// </summary>
    private async Task AttachDataGridJsAsync()
    {
        try
        {
            await EnsureDataGridModuleAsync();

            // Pass the grid element ID; the JS module resolves document.getElementById(_gridId).
            // A proper ElementReference (@ref capture) will replace this once B1 adds the @ref.
            _dataGridHandle = await _dataGridModule!.InvokeAsync<IJSObjectReference>(
                "attachGrid",
                _gridId,
                DotNetObjectReference.Create(this),
                new
                {
                    keyboardNavigation = false,   // B1
                    columnResize = false,         // B2
                    columnReorder = false,        // B3
                    rowDragDrop = false,          // B4
                    frozenColumns = false         // B5
                });
        }
        catch (JSDisconnectedException) { /* circuit down during prerender — skip */ }
        catch (InvalidOperationException) { /* JS unavailable in SSR — skip */ }
    }

    /// <summary>Called from JS when a column is resized.</summary>
    [JSInvokable]
    public async Task OnColumnResized(int columnIndex, double newWidth)
    {
        if (columnIndex >= 0 && columnIndex < _visibleColumns.Count)
        {
            var column = _visibleColumns[columnIndex];
            column.RuntimeWidth = $"{newWidth}px";
            ResolveLayoutContract();

            if (OnColumnResize.HasDelegate)
            {
                await OnColumnResize.InvokeAsync(new ColumnResizeEventArgs
                {
                    Field = column.Field,
                    Width = newWidth
                });
            }
            await NotifyStateChanged("ColumnResize");
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Called from JS when columns are reordered.</summary>
    [JSInvokable]
    public async Task OnColumnsReordered(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _columns.Count || toIndex < 0 || toIndex >= _columns.Count)
            return;

        var column = _columns[fromIndex];
        _columns.RemoveAt(fromIndex);
        _columns.Insert(toIndex, column);
        ResolveLayoutContract();

        if (OnColumnReorder.HasDelegate)
        {
            await OnColumnReorder.InvokeAsync(_columns.Select(c => c.Field).ToList());
        }
        await NotifyStateChanged("ColumnReorder");
        StateHasChanged();
    }

    /// <summary>Called from JS for keyboard navigation cell focus.</summary>
    [JSInvokable]
    public void OnCellFocused(int rowIndex, int colIndex)
    {
        // Can be used for accessibility announcements
    }

    /// <summary>Called from JS when a row is dropped to a new position.</summary>
    [JSInvokable]
    public async Task OnRowDropped(int sourceIndex, int destIndex, string dropPosition)
    {
        if (sourceIndex < 0 || sourceIndex >= _displayedItems.Count ||
            destIndex < 0 || destIndex >= _displayedItems.Count)
            return;

        var args = new GridRowDropEventArgs<TItem>
        {
            Item = _displayedItems[sourceIndex],
            DestinationItem = _displayedItems[destIndex],
            DestinationIndex = destIndex,
            DropPosition = string.Equals(dropPosition, "after", StringComparison.OrdinalIgnoreCase)
                ? GridRowDropPosition.After
                : GridRowDropPosition.Before
        };

        if (OnRowDrop.HasDelegate)
            await OnRowDrop.InvokeAsync(args);
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("dispose");
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }
        _dotNetRef?.Dispose();

        // B0.3: dispose the Phase B ES module handle and module reference.
        if (_dataGridHandle is not null)
        {
            try
            {
                if (_dataGridModule is not null)
                    await _dataGridModule.InvokeVoidAsync("detachGrid", _dataGridHandle);
            }
            catch (JSDisconnectedException) { /* circuit down, nothing to clean */ }
            catch (JSException) { /* module unloaded, nothing to clean */ }
            await _dataGridHandle.DisposeAsync();
            _dataGridHandle = null;
        }

        if (_dataGridModule is not null)
        {
            try { await _dataGridModule.DisposeAsync(); }
            catch (JSDisconnectedException) { }
            _dataGridModule = null;
        }

        // Dispose the lazily-created CSV download module (see SunfishDataGrid.Export.cs).
        await DisposeExportModuleAsync();
    }

    private string GetGridScript() => $$"""
(function() {
    let grid = null;
    let dotNetRef = null;
    let resizeState = null;
    let dragState = null;

    return {
        init(gridId, ref, options) {
            grid = document.getElementById(gridId);
            dotNetRef = ref;
            if (!grid) return;

            if (options.resizable) initResize();
            if (options.reorderable) initReorder();
            if (options.navigable) initKeyboardNav();
            if (options.rowDraggable) initRowDrag();
        },
        dispose() {
            grid = null;
            dotNetRef = null;
        }
    };

    function initResize() {
        const headers = grid.querySelectorAll('thead th');
        headers.forEach((th, i) => {
            if (th.classList.contains('mar-datagrid-detail-cell') ||
                th.classList.contains('mar-datagrid-checkbox-cell') ||
                th.classList.contains('mar-datagrid-command-header')) return;

            const handle = document.createElement('div');
            handle.className = 'mar-datagrid-resize-handle';
            handle.style.cssText = 'position:absolute;right:0;top:0;bottom:0;width:4px;cursor:col-resize;z-index:1;';
            th.style.position = 'relative';
            th.appendChild(handle);

            handle.addEventListener('pointerdown', (e) => {
                e.preventDefault();
                e.stopPropagation();
                handle.setPointerCapture(e.pointerId);
                resizeState = { th, index: getDataColumnIndex(th), startX: e.clientX, startWidth: th.offsetWidth };

                const onMove = (ev) => {
                    if (!resizeState) return;
                    const diff = ev.clientX - resizeState.startX;
                    const newWidth = Math.max(40, resizeState.startWidth + diff);
                    resizeState.th.style.width = newWidth + 'px';
                };

                const onUp = (ev) => {
                    if (resizeState) {
                        const finalWidth = resizeState.th.offsetWidth;
                        dotNetRef.invokeMethodAsync('OnColumnResized', resizeState.index, finalWidth);
                    }
                    handle.releasePointerCapture(ev.pointerId);
                    handle.removeEventListener('pointermove', onMove);
                    handle.removeEventListener('pointerup', onUp);
                    resizeState = null;
                };

                handle.addEventListener('pointermove', onMove);
                handle.addEventListener('pointerup', onUp);
            });
        });
    }

    function initReorder() {
        const headers = grid.querySelectorAll('thead th');
        headers.forEach((th) => {
            if (th.classList.contains('mar-datagrid-detail-cell') ||
                th.classList.contains('mar-datagrid-checkbox-cell') ||
                th.classList.contains('mar-datagrid-command-header') ||
                th.classList.contains('mar-datagrid-col--locked')) return;

            th.setAttribute('draggable', 'true');

            th.addEventListener('dragstart', (e) => {
                dragState = { fromIndex: getDataColumnIndex(th) };
                th.classList.add('mar-datagrid-dragging');
                e.dataTransfer.effectAllowed = 'move';
            });

            th.addEventListener('dragover', (e) => {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';
                th.classList.add('mar-datagrid-drop-target');
            });

            th.addEventListener('dragleave', () => {
                th.classList.remove('mar-datagrid-drop-target');
            });

            th.addEventListener('drop', (e) => {
                e.preventDefault();
                th.classList.remove('mar-datagrid-drop-target');
                if (dragState) {
                    const toIndex = getDataColumnIndex(th);
                    if (dragState.fromIndex !== toIndex && dragState.fromIndex >= 0 && toIndex >= 0) {
                        dotNetRef.invokeMethodAsync('OnColumnsReordered', dragState.fromIndex, toIndex);
                    }
                }
            });

            th.addEventListener('dragend', () => {
                th.classList.remove('mar-datagrid-dragging');
                dragState = null;
            });
        });
    }

    function initKeyboardNav() {
        grid.setAttribute('tabindex', '0');
        let focusRow = 0, focusCol = 0;

        grid.addEventListener('keydown', (e) => {
            const rows = grid.querySelectorAll('tbody tr[role="row"]');
            if (rows.length === 0) return;
            const maxRow = rows.length - 1;
            const cells = rows[focusRow]?.querySelectorAll('td[role="gridcell"]');
            if (!cells) return;
            const maxCol = cells.length - 1;

            let handled = false;
            switch (e.key) {
                case 'ArrowDown':
                    if (focusRow < maxRow) { focusRow++; handled = true; }
                    break;
                case 'ArrowUp':
                    if (focusRow > 0) { focusRow--; handled = true; }
                    break;
                case 'ArrowRight':
                    if (focusCol < maxCol) { focusCol++; handled = true; }
                    break;
                case 'ArrowLeft':
                    if (focusCol > 0) { focusCol--; handled = true; }
                    break;
                case 'Home':
                    if (e.ctrlKey) { focusRow = 0; } focusCol = 0; handled = true;
                    break;
                case 'End':
                    if (e.ctrlKey) { focusRow = maxRow; } focusCol = maxCol; handled = true;
                    break;
                case 'Enter':
                case ' ':
                    const cell = rows[focusRow]?.querySelectorAll('td[role="gridcell"]')[focusCol];
                    if (cell) { const btn = cell.querySelector('button,input,a'); if (btn) btn.click(); }
                    handled = true;
                    break;
            }

            if (handled) {
                e.preventDefault();
                const targetRow = grid.querySelectorAll('tbody tr[role="row"]')[focusRow];
                const targetCell = targetRow?.querySelectorAll('td[role="gridcell"]')[focusCol];
                if (targetCell) {
                    targetCell.setAttribute('tabindex', '0');
                    targetCell.focus();
                    dotNetRef.invokeMethodAsync('OnCellFocused', focusRow, focusCol);
                }
            }
        });
    }

    function initRowDrag() {
        const tbody = grid.querySelector('tbody');
        if (!tbody) return;

        let dragSourceIndex = -1;

        tbody.addEventListener('dragstart', (e) => {
            const cell = e.target.closest('.sf-datagrid-drag-cell');
            if (!cell) return;
            e.stopPropagation();
            dragSourceIndex = parseInt(cell.dataset.rowIndex, 10);
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', dragSourceIndex.toString());
            cell.closest('tr')?.classList.add('mar-datagrid-row--dragging');
        });

        tbody.addEventListener('dragover', (e) => {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            const tr = e.target.closest('tr');
            if (!tr) return;
            const rect = tr.getBoundingClientRect();
            const midY = rect.top + rect.height / 2;
            tbody.querySelectorAll('.sf-datagrid-row--drop-before, .sf-datagrid-row--drop-after')
                .forEach(el => el.classList.remove('mar-datagrid-row--drop-before', 'mar-datagrid-row--drop-after'));
            tr.classList.add(e.clientY < midY ? 'mar-datagrid-row--drop-before' : 'mar-datagrid-row--drop-after');
        });

        tbody.addEventListener('dragleave', (e) => {
            const tr = e.target.closest('tr');
            if (tr) {
                tr.classList.remove('mar-datagrid-row--drop-before', 'mar-datagrid-row--drop-after');
            }
        });

        tbody.addEventListener('drop', (e) => {
            e.preventDefault();
            const tr = e.target.closest('tr');
            if (!tr || dragSourceIndex < 0) return;

            const destCell = tr.querySelector('.sf-datagrid-drag-cell');
            const destIndex = destCell ? parseInt(destCell.dataset.rowIndex, 10) : -1;

            const rect = tr.getBoundingClientRect();
            const dropPosition = e.clientY < (rect.top + rect.height / 2) ? 'before' : 'after';

            tbody.querySelectorAll('.sf-datagrid-row--drop-before, .sf-datagrid-row--drop-after, .sf-datagrid-row--dragging')
                .forEach(el => el.classList.remove('mar-datagrid-row--drop-before', 'mar-datagrid-row--drop-after', 'mar-datagrid-row--dragging'));

            if (destIndex >= 0 && destIndex !== dragSourceIndex) {
                dotNetRef.invokeMethodAsync('OnRowDropped', dragSourceIndex, destIndex, dropPosition);
            }
            dragSourceIndex = -1;
        });

        tbody.addEventListener('dragend', () => {
            tbody.querySelectorAll('.sf-datagrid-row--drop-before, .sf-datagrid-row--drop-after, .sf-datagrid-row--dragging')
                .forEach(el => el.classList.remove('mar-datagrid-row--drop-before', 'mar-datagrid-row--drop-after', 'mar-datagrid-row--dragging'));
            dragSourceIndex = -1;
        });
    }

    function getDataColumnIndex(th) {
        const allTh = Array.from(grid.querySelectorAll('thead tr:first-child th'));
        const dataTh = allTh.filter(h =>
            !h.classList.contains('mar-datagrid-drag-header') &&
            !h.classList.contains('mar-datagrid-detail-cell') &&
            !h.classList.contains('mar-datagrid-checkbox-cell') &&
            !h.classList.contains('mar-datagrid-command-header'));
        return dataTh.indexOf(th);
    }
})()
""";
}

/// <summary>Event args for column resize.</summary>
public class ColumnResizeEventArgs
{
    /// <summary>The field name of the resized column.</summary>
    public string Field { get; init; } = "";

    /// <summary>The new width in pixels.</summary>
    public double Width { get; init; }
}

/// <summary>Tracks a column's visual state (width, order, visibility).</summary>
public class ColumnState
{
    /// <summary>The field name.</summary>
    public string Field { get; set; } = "";

    /// <summary>The column width.</summary>
    public string? Width { get; set; }

    /// <summary>The display order index.</summary>
    public int Order { get; set; }

    /// <summary>Whether the column is visible.</summary>
    public bool Visible { get; set; } = true;
}
