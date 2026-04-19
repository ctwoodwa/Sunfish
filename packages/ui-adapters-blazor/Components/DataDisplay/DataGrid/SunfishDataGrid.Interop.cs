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

    /// <summary>
    /// Whether columns can be resized by dragging (B2 canonical parameter). Defaults to <c>false</c>.
    /// When <c>true</c>, drag handles are rendered in each resizable column header and the JS resize
    /// handler is activated. Individual columns can opt out by setting their <c>Resizable</c> parameter
    /// to <c>false</c>.
    /// </summary>
    [Parameter] public bool AllowColumnResize { get; set; }

    /// <summary>
    /// Whether columns can be resized by dragging. Defaults to false.
    /// <para><b>Note:</b> <see cref="AllowColumnResize"/> is the canonical B2 parameter.
    /// This property is honoured for backward compatibility; setting either is sufficient.</para>
    /// </summary>
    [Parameter] public bool Resizable { get; set; }

    /// <summary>Whether columns can be reordered by dragging. Defaults to false.</summary>
    [Parameter] public bool Reorderable { get; set; }

    /// <summary>
    /// Whether columns can be reordered by dragging (B3 canonical parameter). Defaults to <c>false</c>.
    /// When <c>true</c>, header cells for reorderable columns become HTML5 drag sources and the JS
    /// reorder handler is activated. Individual columns can opt out by setting their
    /// <c>Reorderable</c> parameter to <c>false</c>.
    /// </summary>
    [Parameter] public bool AllowColumnReorder { get; set; }

    /// <summary>Fires when column order changes (legacy event — prefer <see cref="OnColumnReordered"/>).</summary>
    [Parameter] public EventCallback<List<string>> OnColumnReorder { get; set; }

    /// <summary>
    /// Fires before a column reorder is committed, allowing the consumer to cancel it.
    /// Set <see cref="DataGridColumnReorderingEventArgs.Cancel"/> to <c>true</c> to abort.
    /// </summary>
    [Parameter] public EventCallback<DataGridColumnReorderingEventArgs> OnColumnReordering { get; set; }

    /// <summary>
    /// Fires after a column reorder has been applied.
    /// Carries the old and new indices of the moved column.
    /// </summary>
    [Parameter] public EventCallback<DataGridColumnReorderedEventArgs> OnColumnReordered { get; set; }

    /// <summary>
    /// Fires once (on mouseup) when a column is resized via the drag handle.
    /// Carries the column index, column id, and final width in pixels.
    /// </summary>
    [Parameter] public EventCallback<DataGridColumnResizedEventArgs> OnColumnResized { get; set; }

    /// <summary>Fires when a column is resized (legacy event — prefer <see cref="OnColumnResized"/>).</summary>
    [Parameter] public EventCallback<ColumnResizeEventArgs> OnColumnResize { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            // AllowColumnResize is canonical (B2); Resizable is the legacy alias — honour either.
            _resizable = AllowColumnResize || Resizable;
            // AllowColumnReorder is canonical (B3); Reorderable is the legacy alias — honour either.
            _reorderable = AllowColumnReorder || Reorderable;

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
                    keyboardNavigation = false,                             // B1
                    columnResize = AllowColumnResize || Resizable,          // B2
                    columnReorder = AllowColumnReorder || Reorderable,      // B3
                    rowDragDrop = false,                                    // B4
                    frozenColumns = _columns.Any(c => c.Locked)            // B5
                });
        }
        catch (Exception ex) when (ex is JSDisconnectedException
                                       || ex is InvalidOperationException
                                       || ex is JSException
                                       || ex.GetType().Name.Contains("JSRuntime"))
        {
            // Tolerate: circuit down (JSDisconnectedException), SSR/pre-rendering
            // (InvalidOperationException), module unavailable (JSException), or test-harness
            // interop exceptions where import() is not configured (JSRuntimeUnhandledInvocationException).
            // The datagrid renders correctly without JS — interactive features simply aren't wired.
        }
    }

    /// <summary>
    /// Called from JS exactly once, on mouseup, when a column drag-resize completes.
    /// The <c>[JSInvokable("OnColumnResized")]</c> attribute binds the .NET identifier
    /// <c>"OnColumnResized"</c> used by <c>invokeMethodAsync</c> in marilo-datagrid.js to this
    /// method, which is named <c>HandleColumnResizedFromJs</c> to avoid a C# naming conflict
    /// with the <see cref="OnColumnResized"/> EventCallback parameter.
    /// Updates the column's runtime width, persists to <see cref="GridState.ColumnStates"/>,
    /// fires <see cref="OnColumnResized"/> (and the legacy <see cref="OnColumnResize"/>),
    /// then triggers <c>StateHasChanged</c>.
    /// </summary>
    [JSInvokable("OnColumnResized")]
    public async Task HandleColumnResizedFromJs(int columnIndex, double newWidth)
    {
        if (columnIndex < 0 || columnIndex >= _visibleColumns.Count)
            return;

        var column = _visibleColumns[columnIndex];
        var widthCss = $"{newWidth:F0}px";
        column.RuntimeWidth = widthCss;
        ResolveLayoutContract();

        // B2.7: persist width into GridState.ColumnStates so consumers can round-trip the state.
        var colState = _state.ColumnStates.FirstOrDefault(cs => cs.Field == column.Field);
        if (colState is not null)
            colState.Width = widthCss;

        // B2.8: fire the new typed EventCallback first.
        if (OnColumnResized.HasDelegate)
        {
            await OnColumnResized.InvokeAsync(
                new DataGridColumnResizedEventArgs(columnIndex, column.EffectiveId, newWidth));
        }

        // Legacy callback kept for backward compatibility.
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

    /// <summary>
    /// Called from JS before a column drag-reorder is committed.
    /// Returns <c>true</c> when the reorder is allowed to proceed; <c>false</c> when either a
    /// guard condition fails or the consumer handler sets <see cref="DataGridColumnReorderingEventArgs.Cancel"/>.
    /// The <c>[JSInvokable("OnColumnReordering")]</c> attribute binds the .NET identifier used by
    /// <c>invokeMethodAsync</c> in marilo-datagrid.js to this method, which is named
    /// <c>HandleColumnReorderingFromJs</c> to avoid a C# naming conflict with the
    /// <see cref="OnColumnReordering"/> EventCallback parameter.
    /// </summary>
    [JSInvokable("OnColumnReordering")]
    public async Task<bool> HandleColumnReorderingFromJs(int oldIndex, int newIndex)
    {
        var effectiveAllowReorder = AllowColumnReorder || Reorderable;
        if (!effectiveAllowReorder) return false;
        if (oldIndex < 0 || oldIndex >= _columns.Count) return false;
        if (newIndex < 0 || newIndex > _columns.Count - 1) return false;
        if (!_columns[oldIndex].IsReorderable(effectiveAllowReorder)) return false;

        var args = new DataGridColumnReorderingEventArgs(oldIndex, newIndex);
        await OnColumnReordering.InvokeAsync(args);
        return !args.Cancel;
    }

    /// <summary>
    /// Called from JS after a column drag-reorder completes (not cancelled).
    /// Moves the column in <c>_columns</c>, re-sequences <see cref="SunfishGridColumn{TItem}.OrderIndex"/>
    /// on all columns, updates <see cref="GridState.ColumnStates"/> order, fires
    /// <see cref="OnColumnReordered"/>, and triggers a re-render.
    /// The <c>[JSInvokable("OnColumnReordered")]</c> attribute binds the .NET identifier used by
    /// <c>invokeMethodAsync</c> in marilo-datagrid.js to this method, which is named
    /// <c>HandleColumnReorderedFromJs</c> to avoid a C# naming conflict with the
    /// <see cref="OnColumnReordered"/> EventCallback parameter.
    /// </summary>
    [JSInvokable("OnColumnReordered")]
    public async Task HandleColumnReorderedFromJs(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= _columns.Count) return;
        if (newIndex < 0 || newIndex >= _columns.Count) return;

        // B3.6: remove from old position and insert at new position.
        var col = _columns[oldIndex];
        _columns.RemoveAt(oldIndex);
        _columns.Insert(newIndex, col);

        // Re-sequence OrderIndex on all columns to match new positions.
        for (int i = 0; i < _columns.Count; i++)
            _columns[i].SetOrderIndex(i);

        // B3.7: keep GridState.ColumnStates[i].Order in sync with new column order.
        if (_state?.ColumnStates is { Count: > 0 } states)
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                var target = states.FirstOrDefault(s => s.Field == _columns[i].EffectiveId);
                if (target is not null) target.Order = i;
            }
        }

        await OnColumnReordered.InvokeAsync(new DataGridColumnReorderedEventArgs(oldIndex, newIndex));

        // Legacy callback — fire the old event with the new field order.
        if (OnColumnReorder.HasDelegate)
            await OnColumnReorder.InvokeAsync(_columns.Select(c => c.Field).ToList());

        await NotifyStateChanged("ColumnReorder");
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Called from JS when columns are reordered (legacy path — pre-B3).</summary>
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

/// <summary>
/// Event args raised by <see cref="SunfishDataGrid{TItem}.OnColumnResized"/> (B2.8)
/// when the user completes a column drag-resize.
/// </summary>
/// <param name="ColumnIndex">Zero-based index in <c>_visibleColumns</c> at the time of the resize.</param>
/// <param name="ColumnId">Effective column identifier (matches the <c>data-column-id</c> DOM attribute).</param>
/// <param name="NewWidth">Final column width in pixels.</param>
public sealed record DataGridColumnResizedEventArgs(int ColumnIndex, string ColumnId, double NewWidth);

/// <summary>Event args for column resize (legacy — prefer <see cref="DataGridColumnResizedEventArgs"/>).</summary>
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

/// <summary>
/// Event args for <see cref="SunfishDataGrid{TItem}.OnColumnReordering"/> (B3.5).
/// Set <see cref="Cancel"/> to <c>true</c> to abort the reorder.
/// </summary>
/// <param name="OldIndex">Zero-based index of the column before reorder.</param>
/// <param name="NewIndex">Zero-based index the column will move to if not cancelled.</param>
public sealed record DataGridColumnReorderingEventArgs(int OldIndex, int NewIndex)
{
    /// <summary>Set to <c>true</c> to abort the column reorder.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Event args for <see cref="SunfishDataGrid{TItem}.OnColumnReordered"/> (B3.5).
/// Carries the old and new positions of the moved column.
/// </summary>
/// <param name="OldIndex">Zero-based index of the column before the reorder.</param>
/// <param name="NewIndex">Zero-based index the column moved to after the reorder.</param>
public sealed record DataGridColumnReorderedEventArgs(int OldIndex, int NewIndex);
