// Sunfish DataGrid — interactive JS layer (legacy "marilo-datagrid.js" name for source compat).
// Populated incrementally: B0 ships the module + lifecycle scaffolding; B1-B5 populate the feature hooks.

/**
 * Attaches the DataGrid's JS behaviors to the given root element.
 * Called from .NET after first render.
 *
 * @param {string|HTMLElement} rootElementOrId - the grid's outer container element or its DOM id.
 *   B0 passes the element id string; B1+ will switch to an ElementReference (HTMLElement).
 * @param {DotNetObjectReference} dotnetRef - .NET-side callback target
 * @param {Object} options - feature flags (keyboardNavigation, columnResize, columnReorder, rowDragDrop, frozenColumns)
 * @returns {Object} a handle that .NET keeps alive for the grid's lifetime
 */
export function attachGrid(rootElementOrId, dotnetRef, options) {
    const rootElement = typeof rootElementOrId === 'string'
        ? document.getElementById(rootElementOrId)
        : rootElementOrId;

    const handle = {
        rootElement,
        dotnetRef,
        options: options ?? {},
        listeners: [],  // populated by B1-B5 feature installers
    };

    // Feature hooks — no-ops in B0, populated per subsequent tracker item.
    // if (handle.options.keyboardNavigation) installKeyboardNavigation(handle);
    if (handle.options.columnResize) installColumnResize(handle);    // B2
    if (handle.options.columnReorder) installColumnReorder(handle);  // B3
    // if (handle.options.rowDragDrop) installRowDragDrop(handle);
    // if (handle.options.frozenColumns) installFrozenColumns(handle);

    return handle;
}

/**
 * Detaches all JS behaviors and releases listeners. Called from .NET DisposeAsync.
 *
 * @param {Object} handle - the handle returned by attachGrid
 */
export function detachGrid(handle) {
    if (!handle) return;
    for (const l of handle.listeners) {
        try { l.remove?.(); } catch { /* best-effort cleanup */ }
    }
    handle.listeners.length = 0;
}

/**
 * Set a column's width by targeting the <col> element directly, without triggering a re-render.
 * Used by B2 (column resize). No-op-safe: writing to a missing col is harmless.
 *
 * @param {string|HTMLElement} rootElementOrId - the grid's outer container or its DOM id
 * @param {string} columnId - effective column id (matches <col data-column-id>)
 * @param {string} widthCss - e.g. "120px" or "8rem"
 */
export function setColumnWidth(rootElementOrId, columnId, widthCss) {
    const rootElement = typeof rootElementOrId === 'string'
        ? document.getElementById(rootElementOrId)
        : rootElementOrId;
    if (!rootElement) return;
    const col = rootElement.querySelector(`colgroup col[data-column-id="${CSS.escape(columnId)}"]`);
    if (col) col.style.width = widthCss;
}

// ── B2: Column Resizing ──────────────────────────────────────────────────────
//
// Performance contract (matches spec imperative):
//   mousedown  — capture initial state (startX, startWidth, <col> element, min/max)
//   mousemove  — update <col>.style.width directly in DOM; ZERO .NET calls
//   mouseup    — ONE invokeMethodAsync('OnColumnResized', colIndex, finalWidth)
//
// This keeps resize feeling instantaneous. Browsers re-layout the table cheaply via
// <colgroup>/<col> without forcing a .NET render cycle on every pixel of drag.

/**
 * Install column-resize handlers on a grid handle.
 * Called from attachGrid when options.columnResize is true.
 *
 * @param {Object} handle - the handle returned by attachGrid
 */
function installColumnResize(handle) {
    const rootElement = handle.rootElement;
    if (!rootElement) return;

    const onMouseDown = (ev) => {
        const target = ev.target;
        if (!target?.classList?.contains('mar-datagrid-col-resize-handle')) return;

        ev.preventDefault();
        ev.stopPropagation();

        const columnId = target.getAttribute('data-column-id');
        const columnIndex = Number(target.getAttribute('data-column-index'));
        const colEl = rootElement.querySelector(
            'colgroup col[data-column-id="' + CSS.escape(columnId) + '"]'
        );
        if (!colEl) return;

        const startX = ev.clientX;
        const startWidthPx = colEl.getBoundingClientRect().width;

        const minPx = parseCssLength(target.getAttribute('data-min-width')) ?? 40;
        const maxPx = parseCssLength(target.getAttribute('data-max-width')) ?? Number.POSITIVE_INFINITY;

        target.classList.add('is-dragging');
        document.body.style.cursor = 'col-resize';

        /** mousemove: update <col> width directly — NO .NET calls. */
        const onMouseMove = (mv) => {
            const delta = mv.clientX - startX;
            let newWidth = startWidthPx + delta;
            if (newWidth < minPx) newWidth = minPx;
            if (newWidth > maxPx) newWidth = maxPx;
            colEl.style.width = newWidth + 'px';
        };

        /** mouseup: exactly ONE .NET call with the final width. */
        const onMouseUp = async () => {
            document.removeEventListener('mousemove', onMouseMove, true);
            document.removeEventListener('mouseup', onMouseUp, true);
            target.classList.remove('is-dragging');
            document.body.style.cursor = '';

            const finalWidth = colEl.getBoundingClientRect().width;
            try {
                await handle.dotnetRef.invokeMethodAsync('OnColumnResized', columnIndex, finalWidth);
            } catch {
                // Component may have been torn down mid-drag — swallow the error gracefully.
            }
        };

        document.addEventListener('mousemove', onMouseMove, true);
        document.addEventListener('mouseup', onMouseUp, true);
    };

    rootElement.addEventListener('mousedown', onMouseDown);
    // Register for cleanup so detachGrid removes the listener.
    handle.listeners.push({ remove: () => rootElement.removeEventListener('mousedown', onMouseDown) });
}

/**
 * Parse a CSS length string into pixels for min/max clamping during drag.
 * Supports px, rem, and em (rem/em are approximated at 16 px — sufficient for clamping).
 * Returns null when the value is absent or not parseable.
 *
 * @param {string|null} value - e.g. "40px", "2.5rem", "600px"
 * @returns {number|null}
 */
function parseCssLength(value) {
    if (!value) return null;
    const match = /^([\d.]+)(px|rem|em)?$/.exec(value.trim());
    if (!match) return null;
    const n = Number(match[1]);
    const unit = match[2] ?? 'px';
    if (unit === 'px') return n;
    // 1rem / 1em ~= 16 px (browser default root font size) — close enough for drag clamping.
    return n * 16;
}

// ── B3: Column Reordering ─────────────────────────────────────────────────────
//
// Performance contract (matches spec imperative):
//   dragstart  — capture source index; build floating clone <th>; create drop-indicator line
//   drag       — move clone to cursor position; ZERO .NET calls
//   dragover   — update drop-indicator position; ZERO .NET calls
//   drop       — exactly TWO .NET calls:
//                  1. invokeMethodAsync('OnColumnReordering', oldIndex, newIndex) => bool (cancelable)
//                  2. invokeMethodAsync('OnColumnReordered', oldIndex, newIndex) (if not cancelled)
//   dragend    — cleanup (fires even if drop was outside a valid target)
//
// Firefox quirk: dataTransfer.setData('text/plain', ...) is required to actually start HTML5 drag.
//
// Resize-handle coexistence: the resize handle's onMouseDown calls ev.stopPropagation() (see B2
// installColumnResize), which prevents the mousedown from reaching the <th> and triggering a
// spurious drag. The draggable="true" attribute only arms the HTML5 drag for events that originate
// on the <th> itself, not on the resize handle.

/**
 * Install column-reorder HTML5 drag-and-drop handlers on a grid handle.
 * Called from attachGrid when options.columnReorder is true.
 *
 * @param {Object} handle - the handle returned by attachGrid
 */
function installColumnReorder(handle) {
    const rootElement = handle.rootElement;
    if (!rootElement) return;

    let draggingIndex = null;
    let draggingColumnId = null;
    let cloneEl = null;
    let dropLineEl = null;

    const onDragStart = (ev) => {
        const th = ev.target.closest('th[data-column-index][draggable="true"]');
        if (!th) return;
        draggingIndex = Number(th.getAttribute('data-column-index'));
        draggingColumnId = th.getAttribute('data-column-id');

        ev.dataTransfer.effectAllowed = 'move';
        // Firefox requires a data payload to actually initiate drag.
        ev.dataTransfer.setData('text/plain', draggingColumnId ?? '');

        // Build floating clone positioned under the cursor.
        cloneEl = th.cloneNode(true);
        cloneEl.classList.add('mar-datagrid-col-drag-clone');
        cloneEl.style.position = 'fixed';
        cloneEl.style.left = ev.clientX + 'px';
        cloneEl.style.top = ev.clientY + 'px';
        cloneEl.style.pointerEvents = 'none';
        cloneEl.style.opacity = '0.75';
        cloneEl.style.zIndex = '10000';
        cloneEl.style.width = th.getBoundingClientRect().width + 'px';
        document.body.appendChild(cloneEl);

        // Vertical drop-indicator line (positioned relative to root element).
        dropLineEl = document.createElement('div');
        dropLineEl.className = 'mar-datagrid-col-drop-line';
        dropLineEl.style.position = 'absolute';
        dropLineEl.style.top = '0';
        dropLineEl.style.bottom = '0';
        dropLineEl.style.width = '2px';
        dropLineEl.style.background = 'var(--mar-datagrid-reorder-line-color, #1e90ff)';
        dropLineEl.style.pointerEvents = 'none';
        dropLineEl.style.display = 'none';
        // Root element must be positioned for the absolute line to align correctly.
        const currentPosition = getComputedStyle(rootElement).position;
        if (currentPosition === 'static') rootElement.style.position = 'relative';
        rootElement.appendChild(dropLineEl);

        th.classList.add('is-dragging-source');
    };

    // Move the floating clone with the cursor — no .NET calls.
    const onDrag = (ev) => {
        if (cloneEl && ev.clientX !== 0) {
            cloneEl.style.left = ev.clientX + 'px';
            cloneEl.style.top = ev.clientY + 'px';
        }
    };

    // Update drop-indicator position — no .NET calls.
    const onDragOver = (ev) => {
        if (draggingIndex === null) return;
        const th = ev.target.closest('th[data-column-index]');
        if (!th) return;
        ev.preventDefault();
        ev.dataTransfer.dropEffect = 'move';

        const rect = th.getBoundingClientRect();
        const insertAfter = ev.clientX > rect.left + rect.width / 2;
        const rootRect = rootElement.getBoundingClientRect();
        if (dropLineEl) {
            dropLineEl.style.display = 'block';
            dropLineEl.style.left = ((insertAfter ? rect.right : rect.left) - rootRect.left) + 'px';
            dropLineEl.style.height = rect.height + 'px';
        }
    };

    // On drop: fire the two .NET calls (pre-check, then commit).
    const onDrop = async (ev) => {
        if (draggingIndex === null) return;
        const th = ev.target.closest('th[data-column-index]');
        if (!th) { cleanup(); return; }
        ev.preventDefault();

        const targetIndex = Number(th.getAttribute('data-column-index'));
        const rect = th.getBoundingClientRect();
        const insertAfter = ev.clientX > rect.left + rect.width / 2;
        let newIndex = insertAfter ? targetIndex + 1 : targetIndex;
        // Account for removal of the dragged column at oldIndex.
        if (newIndex > draggingIndex) newIndex -= 1;
        if (newIndex === draggingIndex) { cleanup(); return; }

        try {
            const allowed = await handle.dotnetRef.invokeMethodAsync(
                'OnColumnReordering', draggingIndex, newIndex);
            if (allowed === false) { cleanup(); return; }
            await handle.dotnetRef.invokeMethodAsync(
                'OnColumnReordered', draggingIndex, newIndex);
        } catch {
            // Component may have been torn down mid-drag — swallow the error gracefully.
        }
        cleanup();
    };

    const onDragEnd = () => cleanup();

    const cleanup = () => {
        if (cloneEl) { cloneEl.remove(); cloneEl = null; }
        if (dropLineEl) { dropLineEl.remove(); dropLineEl = null; }
        rootElement.querySelectorAll('.is-dragging-source')
            .forEach(n => n.classList.remove('is-dragging-source'));
        draggingIndex = null;
        draggingColumnId = null;
    };

    rootElement.addEventListener('dragstart', onDragStart);
    rootElement.addEventListener('drag', onDrag);
    rootElement.addEventListener('dragover', onDragOver);
    rootElement.addEventListener('drop', onDrop);
    rootElement.addEventListener('dragend', onDragEnd);

    handle.listeners.push({
        remove: () => {
            rootElement.removeEventListener('dragstart', onDragStart);
            rootElement.removeEventListener('drag', onDrag);
            rootElement.removeEventListener('dragover', onDragOver);
            rootElement.removeEventListener('drop', onDrop);
            rootElement.removeEventListener('dragend', onDragEnd);
            cleanup();
        }
    });
}
