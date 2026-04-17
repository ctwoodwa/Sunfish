/**
 * MariloAllocationScheduler JS Interop
 * Handles drag-fill, cell selection, keyboard editing, and clipboard
 * interactions that require DOM-level event handling.
 */
export const AllocationSchedulerInterop = {

    /**
     * Initialize fill-handle drag behavior.
     * Drag starts ONLY when the user grabs the .mar-allocation-scheduler__fill-handle element.
     * Sends {source, targets} JSON to .NET on drop.
     */
    initDragFill: function (gridElement, dotNetRef) {
        if (!gridElement) return;

        let isDragging = false;
        let sourceCell = null;
        let previewCells = [];

        const getCellKey = (cell) => ({
            resourceKey: cell.dataset.resourceKey,
            bucketStart: cell.dataset.bucketStart
        });

        const clearPreview = () => {
            previewCells.forEach(c => c.classList.remove('mar-allocation-scheduler__cell--drag-target'));
            previewCells = [];
        };

        const handleMouseDown = (e) => {
            const handle = e.target.closest('.mar-allocation-scheduler__fill-handle');
            if (!handle) return;

            const cell = handle.closest('[role="gridcell"][data-resource-key]');
            if (!cell) return;
            if (cell.getAttribute('aria-disabled') === 'true') return;
            if (cell.getAttribute('aria-readonly') === 'true') return;

            isDragging = true;
            sourceCell = cell;
            previewCells = [];
            e.preventDefault();
            e.stopPropagation();
        };

        const handleMouseMove = (e) => {
            if (!isDragging || !sourceCell) return;

            const cell = e.target.closest('[role="gridcell"][data-resource-key]');
            if (!cell || cell === sourceCell) return;
            if (cell.getAttribute('aria-disabled') === 'true') return;

            // Rebuild preview from scratch each move — keep order natural
            clearPreview();

            // Collect all cells in the timeline panel between source and target
            const timelinePanel = sourceCell.closest('.mar-allocation-scheduler__timeline-panel');
            if (!timelinePanel) return;

            const allCells = Array.from(timelinePanel.querySelectorAll('[role="gridcell"][data-resource-key]'));
            const srcIdx = allCells.indexOf(sourceCell);
            const tgtIdx = allCells.indexOf(cell);
            if (srcIdx < 0 || tgtIdx < 0) return;

            const lo = Math.min(srcIdx, tgtIdx);
            const hi = Math.max(srcIdx, tgtIdx);
            previewCells = allCells.slice(lo, hi + 1).filter(c => c !== sourceCell);
            previewCells.forEach(c => {
                if (c.getAttribute('aria-disabled') !== 'true')
                    c.classList.add('mar-allocation-scheduler__cell--drag-target');
            });
        };

        const handleMouseUp = async () => {
            if (!isDragging) return;
            isDragging = false;

            const validTargets = previewCells.filter(c =>
                c.getAttribute('aria-disabled') !== 'true' &&
                c.getAttribute('aria-readonly') !== 'true'
            );

            clearPreview();

            if (sourceCell && validTargets.length > 0) {
                const payload = {
                    source: getCellKey(sourceCell),
                    targets: validTargets.map(getCellKey)
                };
                try {
                    await dotNetRef.invokeMethodAsync('OnDragFillCompleted', JSON.stringify(payload));
                } catch (_) { /* component may have been disposed */ }
            }

            sourceCell = null;
        };

        gridElement.addEventListener('mousedown', handleMouseDown);
        document.addEventListener('mousemove', handleMouseMove);
        document.addEventListener('mouseup', handleMouseUp);

        gridElement._allocationSchedulerCleanup = () => {
            gridElement.removeEventListener('mousedown', handleMouseDown);
            document.removeEventListener('mousemove', handleMouseMove);
            document.removeEventListener('mouseup', handleMouseUp);
        };
    },

    /**
     * Initialize keyboard navigation and editing for the scheduler grid.
     * Arrow keys move the active cell.
     * Enter / F2 enter edit mode on the focused cell.
     * Direct typing enters edit mode and seeds the input with the typed character.
     * Delete / Backspace clears the cell value.
     * Escape cancels pending actions.
     */
    initKeyboardNav: function (gridElement, dotNetRef) {
        if (!gridElement) return;

        const getCellKey = (cell) => ({
            resourceKey: cell.dataset.resourceKey,
            bucketStart: cell.dataset.bucketStart
        });

        // Returns only the editable cells in the timeline panel, in DOM order.
        const getEditableCells = () =>
            Array.from(gridElement.querySelectorAll(
                '.mar-allocation-scheduler__timeline-panel [role="gridcell"][tabindex="0"]'
            ));

        // Number of columns in one timeline row.
        const columnsPerRow = () => {
            const panel = gridElement.querySelector('.mar-allocation-scheduler__timeline-panel');
            if (!panel) return 1;
            const rows = panel.querySelectorAll('[role="row"]');
            if (rows.length < 2) return 1;
            return rows[1].querySelectorAll('[role="gridcell"][tabindex="0"]').length || 1;
        };

        const handleKeyDown = async (e) => {
            // Only handle keydown events that originated from a gridcell (not from an input inside edit mode)
            const cell = e.target.closest('[role="gridcell"]');
            if (!cell) return;
            if (e.target.tagName === 'INPUT') return; // edit-mode input handles its own keys

            const allCells = getEditableCells();
            const currentIndex = allCells.indexOf(cell);
            if (currentIndex === -1) return;

            let nextIndex = currentIndex;
            let handled = true;

            const cols = columnsPerRow();

            switch (e.key) {
                case 'ArrowRight':
                    nextIndex = Math.min(currentIndex + 1, allCells.length - 1);
                    break;
                case 'ArrowLeft':
                    nextIndex = Math.max(currentIndex - 1, 0);
                    break;
                case 'ArrowDown':
                    nextIndex = Math.min(currentIndex + cols, allCells.length - 1);
                    break;
                case 'ArrowUp':
                    nextIndex = Math.max(currentIndex - cols, 0);
                    break;
                case 'Tab':
                    if (e.shiftKey) {
                        nextIndex = Math.max(currentIndex - 1, 0);
                    } else {
                        nextIndex = Math.min(currentIndex + 1, allCells.length - 1);
                    }
                    break;
                case 'Enter':
                case 'F2': {
                    // Enter edit mode for the focused cell
                    const key = getCellKey(cell);
                    if (key.resourceKey) {
                        try {
                            await dotNetRef.invokeMethodAsync('OnEnterEditMode', JSON.stringify(key));
                        } catch (_) { }
                    }
                    e.preventDefault();
                    return;
                }
                case 'Delete':
                case 'Backspace': {
                    const key = getCellKey(cell);
                    if (key.resourceKey) {
                        try {
                            await dotNetRef.invokeMethodAsync('OnDeletePressed', JSON.stringify(key));
                        } catch (_) { }
                    }
                    e.preventDefault();
                    return;
                }
                case 'Escape':
                    cell.blur();
                    try {
                        await dotNetRef.invokeMethodAsync('OnEscapePressed');
                    } catch (_) { }
                    e.preventDefault();
                    return;
                default:
                    // Printable characters start typing / enter edit mode
                    if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
                        const key = getCellKey(cell);
                        if (key.resourceKey) {
                            try {
                                await dotNetRef.invokeMethodAsync('OnStartTyping', JSON.stringify(key), e.key);
                            } catch (_) { }
                        }
                        e.preventDefault();
                    }
                    return;
            }

            e.preventDefault();

            if (nextIndex !== currentIndex && allCells[nextIndex]) {
                allCells[nextIndex].focus();
                const key = getCellKey(allCells[nextIndex]);
                if (key.resourceKey) {
                    try {
                        await dotNetRef.invokeMethodAsync('OnCellFocused', JSON.stringify(key));
                    } catch (_) { }
                }
            }
        };

        gridElement.addEventListener('keydown', handleKeyDown);

        const existingCleanup = gridElement._allocationSchedulerCleanup;
        gridElement._allocationSchedulerCleanup = () => {
            existingCleanup?.();
            gridElement.removeEventListener('keydown', handleKeyDown);
        };
    },

    /**
     * Initialize clipboard copy (Ctrl/Cmd+C) and paste (Ctrl/Cmd+V).
     * Copy serialises the selected cells as tab/newline-delimited text.
     * Paste sends the raw text to .NET for parsing and application.
     */
    initClipboard: function (gridElement, dotNetRef) {
        if (!gridElement) return;
        if (typeof navigator.clipboard === 'undefined') return;

        const handleKeyDown = async (e) => {
            if (!e.ctrlKey && !e.metaKey) return;

            if (e.key === 'c' || e.key === 'C') {
                // ── Copy ───────────────────────────────────────────────
                const selected = Array.from(
                    gridElement.querySelectorAll(
                        '.mar-allocation-scheduler__timeline-panel [role="gridcell"].mar-allocation-scheduler__cell--selected[data-resource-key],' +
                        '.mar-allocation-scheduler__timeline-panel [role="gridcell"].mar-bs-allocation-scheduler__cell--selected[data-resource-key]'
                    )
                );
                if (selected.length === 0) return;

                // Build row-major map: resourceKey -> sorted bucketStart -> display text
                const rowMap = new Map();   // resourceKey -> Map<bucketStart, text>
                const bucketSet = new Set();

                selected.forEach(cell => {
                    const rk = cell.dataset.resourceKey;
                    const bs = cell.dataset.bucketStart;
                    const valueEl = cell.querySelector(
                        '.mar-allocation-scheduler__cell-value, .mar-bs-allocation-scheduler__cell-value'
                    );
                    const text = valueEl ? valueEl.textContent.trim() : '';

                    if (!rowMap.has(rk)) rowMap.set(rk, new Map());
                    rowMap.get(rk).set(bs, text);
                    bucketSet.add(bs);
                });

                const buckets = Array.from(bucketSet).sort();
                const tsv = Array.from(rowMap.values())
                    .map(colMap => buckets.map(b => colMap.get(b) ?? '').join('\t'))
                    .join('\n');

                try {
                    await navigator.clipboard.writeText(tsv);
                    e.preventDefault();
                } catch (_) { }

            } else if (e.key === 'v' || e.key === 'V') {
                // ── Paste ──────────────────────────────────────────────
                try {
                    const text = await navigator.clipboard.readText();
                    if (text && text.trim()) {
                        await dotNetRef.invokeMethodAsync('OnPasteData', text);
                        e.preventDefault();
                    }
                } catch (_) { }
            }
        };

        gridElement.addEventListener('keydown', handleKeyDown);

        const existingClipboardCleanup = gridElement._allocationSchedulerClipboardCleanup;
        existingClipboardCleanup?.();
        gridElement._allocationSchedulerClipboardCleanup = () => {
            gridElement.removeEventListener('keydown', handleKeyDown);
        };

        const existingCleanup = gridElement._allocationSchedulerCleanup;
        gridElement._allocationSchedulerCleanup = () => {
            existingCleanup?.();
            gridElement._allocationSchedulerClipboardCleanup?.();
        };
    },

    /**
     * Initialize per-column header drag-to-resize for resource columns.
     * Handles rendered inside <th> cells (class mar-allocation-scheduler__col-resize-handle).
     * On drag: fires OnColumnResizeDrag for live preview (same pattern as splitter).
     * On release: fires OnColumnResizeEnd to persist.
     */
    initColumnResize: function (gridElement, dotNetRef) {
        if (!gridElement) return;

        let isDragging = false;
        let startX = 0;
        let startWidth = 0;
        let activeColId = null;
        let activeColEl = null;  // the <col> element in the colgroup

        const findCol = (colId) => {
            return gridElement.querySelector(
                `.mar-allocation-scheduler__resource-panel colgroup col[data-col-id="${colId}"]`
            );
        };

        const handleMouseDown = (e) => {
            const handle = e.target.closest('.mar-allocation-scheduler__col-resize-handle');
            if (!handle) return;

            const colId = handle.dataset.colId;
            if (!colId) return;

            const th = handle.closest('th');
            if (!th) return;

            const col = findCol(colId);

            isDragging = true;
            startX = e.clientX;
            startWidth = th.getBoundingClientRect().width;
            activeColId = colId;
            activeColEl = col;

            handle.classList.add('mar-allocation-scheduler__col-resize-handle--active');
            document.body.style.cursor = 'col-resize';
            document.body.style.userSelect = 'none';
            e.preventDefault();
            e.stopPropagation();
        };

        const handleMouseMove = async (e) => {
            if (!isDragging || !activeColId) return;
            const delta = e.clientX - startX;
            const newWidth = Math.max(40, startWidth + delta);
            try {
                await dotNetRef.invokeMethodAsync('OnColumnResizeDrag', activeColId, newWidth);
            } catch (_) { /* component disposed */ }
        };

        const handleMouseUp = async () => {
            if (!isDragging) return;
            isDragging = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';

            // Remove active class from all handles
            gridElement.querySelectorAll('.mar-allocation-scheduler__col-resize-handle--active')
                .forEach(h => h.classList.remove('mar-allocation-scheduler__col-resize-handle--active'));

            if (activeColId) {
                const delta = 0; // already applied via OnColumnResizeDrag
                // Read the current col width that .NET has set (or fall back to startWidth + last delta)
                const currentWidth = activeColEl
                    ? parseFloat(activeColEl.style.width || startWidth)
                    : startWidth;
                try {
                    await dotNetRef.invokeMethodAsync('OnColumnResizeEnd', activeColId, currentWidth);
                } catch (_) { }
            }

            activeColId = null;
            activeColEl = null;
        };

        gridElement.addEventListener('mousedown', handleMouseDown);
        document.addEventListener('mousemove', handleMouseMove);
        document.addEventListener('mouseup', handleMouseUp);

        const existingCleanup = gridElement._allocationSchedulerCleanup;
        gridElement._allocationSchedulerCleanup = () => {
            existingCleanup?.();
            gridElement.removeEventListener('mousedown', handleMouseDown);
            document.removeEventListener('mousemove', handleMouseMove);
            document.removeEventListener('mouseup', handleMouseUp);
        };
    },

    /**
     * Initialize splitter drag-to-resize behavior.
     */
    initSplitter: function (gridElement, dotNetRef) {
        if (!gridElement) return;

        const splitter = gridElement.querySelector('[role="separator"]');
        if (!splitter) return;

        const resourcePanel = gridElement.querySelector('.mar-allocation-scheduler__resource-panel');
        const grid = gridElement.querySelector('.mar-allocation-scheduler__grid');
        if (!resourcePanel || !grid) return;

        let isDragging = false;
        let startX = 0;
        let startWidth = 0;

        const handleMouseDown = (e) => {
            const sep = e.target.closest('[role="separator"]');
            if (sep !== splitter) return;
            if (splitter.dataset.locked === 'true') return;

            isDragging = true;
            startX = e.clientX;
            startWidth = resourcePanel.getBoundingClientRect().width;
            splitter.classList.add('mar-allocation-scheduler__splitter--dragging');
            document.body.style.cursor = 'col-resize';
            document.body.style.userSelect = 'none';
            e.preventDefault();
        };

        const handleMouseMove = (e) => {
            if (!isDragging) return;
            const delta = e.clientX - startX;
            const newWidth = startWidth + delta;
            const containerWidth = grid.getBoundingClientRect().width;
            const splitterWidth = splitter.getBoundingClientRect().width || 8;
            const minLeft = parseFloat(gridElement.dataset.minLeftPane) || 40;
            const minRight = parseFloat(gridElement.dataset.minRightPane) || 300;
            const allowCollapse = gridElement.dataset.allowCollapse === 'true';

            if (allowCollapse && newWidth < minLeft * 0.5) {
                resourcePanel.style.width = '0px';
                return;
            }

            if (allowCollapse && newWidth > containerWidth - splitterWidth - minRight * 0.5) {
                resourcePanel.style.width = (containerWidth - splitterWidth) + 'px';
                return;
            }

            const clamped = Math.max(minLeft, Math.min(newWidth, containerWidth - splitterWidth - minRight));
            resourcePanel.style.width = clamped + 'px';
        };

        const handleMouseUp = async () => {
            if (!isDragging) return;
            isDragging = false;
            splitter.classList.remove('mar-allocation-scheduler__splitter--dragging');
            document.body.style.cursor = '';
            document.body.style.userSelect = '';

            const containerWidth = grid.getBoundingClientRect().width;
            const splitterWidth = splitter.getBoundingClientRect().width || 8;
            const currentWidth = resourcePanel.getBoundingClientRect().width;
            const minLeft = parseFloat(gridElement.dataset.minLeftPane) || 40;
            const minRight = parseFloat(gridElement.dataset.minRightPane) || 300;
            const allowCollapse = gridElement.dataset.allowCollapse === 'true';

            if (allowCollapse && currentWidth < minLeft * 0.5) {
                await dotNetRef.invokeMethodAsync('OnSplitterDragEnd', 0);
                return;
            }

            if (allowCollapse && currentWidth > containerWidth - splitterWidth - minRight * 0.5) {
                await dotNetRef.invokeMethodAsync('OnSplitterCollapseRight');
                return;
            }

            const clamped = Math.max(minLeft, Math.min(currentWidth, containerWidth - splitterWidth - minRight));
            await dotNetRef.invokeMethodAsync('OnSplitterDragEnd', clamped);
        };

        splitter.addEventListener('mousedown', handleMouseDown);
        document.addEventListener('mousemove', handleMouseMove);
        document.addEventListener('mouseup', handleMouseUp);

        const existingCleanup = gridElement._allocationSchedulerCleanup;
        gridElement._allocationSchedulerCleanup = () => {
            existingCleanup?.();
            splitter.removeEventListener('mousedown', handleMouseDown);
            document.removeEventListener('mousemove', handleMouseMove);
            document.removeEventListener('mouseup', handleMouseUp);
        };
    },

    /**
     * Initialize per-column header drag-to-resize for time (bucket) columns.
     * Handles rendered inside <th> cells (class mar-allocation-scheduler__time-resize-handle).
     * On drag: fires OnTimeColumnResizeDrag for live preview.
     * On release: fires OnTimeColumnResizeEnd to persist.
     * On double-click (when autoFit is true): measures widest cell and fires OnTimeColumnAutoFit.
     */
    initTimeColumnResize: function (gridElement, dotNetRef, minWidth, maxWidth, autoFit) {
        if (!gridElement) return;

        let isDragging = false;
        let startX = 0;
        let startWidth = 0;
        let activeColIdx = -1;

        const handleMouseDown = (e) => {
            const handle = e.target.closest('.mar-allocation-scheduler__time-resize-handle');
            if (!handle) return;

            const colIdx = parseInt(handle.dataset.bucketColIdx, 10);
            if (isNaN(colIdx)) return;

            const th = handle.closest('th');
            if (!th) return;

            isDragging = true;
            startX = e.clientX;
            startWidth = th.getBoundingClientRect().width;
            activeColIdx = colIdx;

            handle.classList.add('mar-allocation-scheduler__time-resize-handle--active');
            document.body.style.cursor = 'col-resize';
            document.body.style.userSelect = 'none';
            e.preventDefault();
            e.stopPropagation();
        };

        const handleMouseMove = async (e) => {
            if (!isDragging || activeColIdx < 0) return;
            const delta = e.clientX - startX;
            let newWidth = Math.round(startWidth + delta);
            newWidth = Math.max(minWidth || 48, newWidth);
            if (maxWidth > 0) newWidth = Math.min(maxWidth, newWidth);
            try {
                await dotNetRef.invokeMethodAsync('OnTimeColumnResizeDrag', activeColIdx, newWidth);
            } catch (_) { /* component disposed */ }
        };

        const handleMouseUp = async () => {
            if (!isDragging) return;
            isDragging = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';

            // Remove active class from all time resize handles
            gridElement.querySelectorAll('.mar-allocation-scheduler__time-resize-handle--active')
                .forEach(h => h.classList.remove('mar-allocation-scheduler__time-resize-handle--active'));

            if (activeColIdx >= 0) {
                // Read the current <th> width
                const th = gridElement.querySelector(
                    `.mar-allocation-scheduler__timeline-panel th[data-bucket-col-idx="${activeColIdx}"]`
                );
                const finalWidth = th ? Math.round(th.getBoundingClientRect().width) : Math.round(startWidth);
                try {
                    await dotNetRef.invokeMethodAsync('OnTimeColumnResizeEnd', activeColIdx, finalWidth);
                } catch (_) { }
            }

            activeColIdx = -1;
        };

        const handleDblClick = async (e) => {
            if (!autoFit) return;
            const handle = e.target.closest('.mar-allocation-scheduler__time-resize-handle');
            if (!handle) return;

            const colIdx = parseInt(handle.dataset.bucketColIdx, 10);
            if (isNaN(colIdx)) return;

            // Measure widest cell content in this column (header + body rows)
            const timelinePanel = gridElement.querySelector('.mar-allocation-scheduler__timeline-panel');
            if (!timelinePanel) return;

            const nthChild = colIdx + 1;
            const cells = timelinePanel.querySelectorAll(
                `th:nth-child(${nthChild}), td:nth-child(${nthChild})`
            );

            let maxContentWidth = 0;
            cells.forEach(cell => {
                // Measure the scrollWidth with width temporarily set to auto
                const originalWidth = cell.style.width;
                const originalMinWidth = cell.style.minWidth;
                const originalMaxWidth = cell.style.maxWidth;
                cell.style.width = 'auto';
                cell.style.minWidth = 'auto';
                cell.style.maxWidth = 'none';
                maxContentWidth = Math.max(maxContentWidth, cell.scrollWidth);
                cell.style.width = originalWidth;
                cell.style.minWidth = originalMinWidth;
                cell.style.maxWidth = originalMaxWidth;
            });

            const measuredWidth = maxContentWidth + 8; // +8px padding buffer
            try {
                await dotNetRef.invokeMethodAsync('OnTimeColumnAutoFit', colIdx, measuredWidth);
            } catch (_) { }
        };

        gridElement.addEventListener('mousedown', handleMouseDown);
        document.addEventListener('mousemove', handleMouseMove);
        document.addEventListener('mouseup', handleMouseUp);
        gridElement.addEventListener('dblclick', handleDblClick);

        const existingCleanup = gridElement._allocationSchedulerCleanup;
        gridElement._allocationSchedulerCleanup = () => {
            existingCleanup?.();
            gridElement.removeEventListener('mousedown', handleMouseDown);
            document.removeEventListener('mousemove', handleMouseMove);
            document.removeEventListener('mouseup', handleMouseUp);
            gridElement.removeEventListener('dblclick', handleDblClick);
        };
    },

    /**
     * Synchronize vertical scrolling between resource panel and timeline panel.
     */
    initScrollSync: function (gridElement) {
        if (!gridElement) return;

        const resourcePanel = gridElement.querySelector('.mar-allocation-scheduler__resource-panel');
        const timelinePanel = gridElement.querySelector('.mar-allocation-scheduler__timeline-panel');
        if (!resourcePanel || !timelinePanel) return;

        let isSyncing = false;

        const syncFromTimeline = () => {
            if (isSyncing) return;
            isSyncing = true;
            resourcePanel.scrollTop = timelinePanel.scrollTop;
            isSyncing = false;
        };

        const syncFromResource = () => {
            if (isSyncing) return;
            isSyncing = true;
            timelinePanel.scrollTop = resourcePanel.scrollTop;
            isSyncing = false;
        };

        timelinePanel.addEventListener('scroll', syncFromTimeline);
        resourcePanel.addEventListener('scroll', syncFromResource);

        const existingCleanup = gridElement._allocationSchedulerCleanup;
        gridElement._allocationSchedulerCleanup = () => {
            existingCleanup?.();
            timelinePanel.removeEventListener('scroll', syncFromTimeline);
            resourcePanel.removeEventListener('scroll', syncFromResource);
        };
    },

    // ── Pane Width Observer ────────────────────────────────────────────
    // Map of elementId → { observer, rafId } for cleanup.
    _paneObservers: new Map(),

    /**
     * Attach a ResizeObserver to the timeline pane element.
     * On every size change (debounced via requestAnimationFrame) computes
     * the number of columns that fit and calls .NET back.
     * Also fires once immediately with the current width.
     *
     * @param {DotNetObjectReference} dotNetRef - .NET object ref for callbacks
     * @param {string} elementId - DOM id of the timeline pane
     * @param {number} minColWidth - minimum column width in pixels
     * @param {number} minVisibleColumns - floor for the column count
     */
    observePane: function (dotNetRef, elementId, minColWidth, minVisibleColumns) {
        // Clean up any existing observer for this element
        this.unobservePane(elementId);

        const el = document.getElementById(elementId);
        if (!el) return;

        let rafId = 0;

        const measure = () => {
            const paneWidth = el.clientWidth;
            const count = Math.max(minVisibleColumns, Math.floor(paneWidth / minColWidth));
            try {
                dotNetRef.invokeMethodAsync('OnPaneWidthChanged', paneWidth, count);
            } catch (_) { /* component disposed */ }
        };

        const onResize = () => {
            if (rafId) cancelAnimationFrame(rafId);
            rafId = requestAnimationFrame(() => {
                rafId = 0;
                measure();
            });
        };

        const observer = new ResizeObserver(onResize);
        observer.observe(el);

        this._paneObservers.set(elementId, { observer, getRafId: () => rafId, cancelRaf: () => { if (rafId) cancelAnimationFrame(rafId); } });

        // Fire immediately with current size
        measure();
    },

    /**
     * Disconnect and remove the ResizeObserver for the given element.
     * @param {string} elementId - DOM id of the timeline pane
     */
    unobservePane: function (elementId) {
        const entry = this._paneObservers.get(elementId);
        if (!entry) return;
        entry.cancelRaf();
        entry.observer.disconnect();
        this._paneObservers.delete(elementId);
    },

    /**
     * Dispose all event listeners for the scheduler grid.
     */
    dispose: function (gridElement) {
        gridElement?._allocationSchedulerCleanup?.();
        // Clean up any pane observers associated with elements inside this grid
        if (gridElement) {
            for (const [id, entry] of this._paneObservers) {
                const el = document.getElementById(id);
                if (el && gridElement.contains(el)) {
                    entry.cancelRaf();
                    entry.observer.disconnect();
                    this._paneObservers.delete(id);
                }
            }
        }
    }
};
