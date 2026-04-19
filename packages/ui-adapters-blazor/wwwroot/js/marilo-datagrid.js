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
    if (handle.options.rowDragDrop) installRowDragDrop(handle);     // B4
    if (handle.options.frozenColumns) recomputeFrozenOffsets(rootElement);  // B5 — initial offset pass

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

            // B5.7 — recompute sticky offsets after resize commits (only if frozen columns are active)
            if (handle.options.frozenColumns) recomputeFrozenOffsets(rootElement);
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

        // B5.7 — recompute sticky offsets after reorder commits (only if frozen columns are active)
        if (handle.options.frozenColumns) recomputeFrozenOffsets(rootElement);
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

// ── B4: Row Drag-and-Drop ─────────────────────────────────────────────────────
//
// Performance contract:
//   dragstart  — capture source row index; build floating clone <tr>; create drop-line indicator
//   drag       — move clone to cursor position; ZERO .NET calls
//   dragover   — update drop-line position; ZERO .NET calls
//   drop       — exactly ONE .NET call: invokeMethodAsync('OnRowDropped', sourceIndex, destIndex, dropPosition)
//   dragend    — cleanup (fires even if drop was outside a valid target)
//
// B3 coexistence: both B3 and B4 listen on root dragstart. B3 targets th[data-column-index],
// B4 targets tr[data-row-index]. An event can only match one — they are mutually exclusive.
//
// Firefox quirk: dataTransfer.setData('text/plain', ...) is required to actually start HTML5 drag.

/**
 * Install row drag-and-drop handlers on a grid handle.
 * Called from attachGrid when options.rowDragDrop is true.
 *
 * @param {Object} handle - the handle returned by attachGrid
 */
function installRowDragDrop(handle) {
    const rootElement = handle.rootElement;
    if (!rootElement) return;

    let draggingIndex = null;
    let cloneEl = null;
    let dropLineEl = null;

    const onDragStart = (ev) => {
        const tr = ev.target.closest('tr[data-row-index][draggable="true"]');
        if (!tr) return;
        draggingIndex = Number(tr.getAttribute('data-row-index'));

        ev.dataTransfer.effectAllowed = 'move';
        // Firefox requires a data payload to actually initiate drag.
        ev.dataTransfer.setData('text/plain', String(draggingIndex));

        // Build floating clone positioned under the cursor.
        cloneEl = tr.cloneNode(true);
        cloneEl.classList.add('mar-datagrid-row-drag-clone');
        cloneEl.style.position = 'fixed';
        cloneEl.style.left = ev.clientX + 'px';
        cloneEl.style.top = ev.clientY + 'px';
        cloneEl.style.pointerEvents = 'none';
        cloneEl.style.opacity = '0.75';
        cloneEl.style.zIndex = '10000';
        cloneEl.style.background = 'var(--mar-datagrid-row-bg, #fff)';
        cloneEl.style.boxShadow = '0 4px 12px rgba(0, 0, 0, 0.15)';
        document.body.appendChild(cloneEl);

        // Horizontal drop-indicator line (positioned relative to root element).
        dropLineEl = document.createElement('div');
        dropLineEl.className = 'mar-datagrid-row-drop-line';
        dropLineEl.style.display = 'none';
        // Root element must be positioned for the absolute line to align correctly.
        const currentPosition = getComputedStyle(rootElement).position;
        if (currentPosition === 'static') rootElement.style.position = 'relative';
        rootElement.appendChild(dropLineEl);

        tr.classList.add('is-row-dragging-source');
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
        const tr = ev.target.closest('tr[data-row-index]');
        if (!tr) return;
        ev.preventDefault();
        ev.dataTransfer.dropEffect = 'move';

        const rect = tr.getBoundingClientRect();
        const insertAfter = ev.clientY > rect.top + rect.height / 2;
        const rootRect = rootElement.getBoundingClientRect();
        if (dropLineEl) {
            dropLineEl.style.display = 'block';
            dropLineEl.style.top = ((insertAfter ? rect.bottom : rect.top) - rootRect.top) + 'px';
        }
    };

    // On drop: fire exactly ONE .NET call.
    const onDrop = async (ev) => {
        if (draggingIndex === null) return;
        const tr = ev.target.closest('tr[data-row-index]');
        if (!tr) { cleanup(); return; }
        ev.preventDefault();

        const targetIndex = Number(tr.getAttribute('data-row-index'));
        const rect = tr.getBoundingClientRect();
        const insertAfter = ev.clientY > rect.top + rect.height / 2;
        let destIndex = insertAfter ? targetIndex + 1 : targetIndex;
        // Account for removal of dragged row at sourceIndex.
        if (destIndex > draggingIndex) destIndex -= 1;
        if (destIndex === draggingIndex) { cleanup(); return; }

        const dropPosition = insertAfter ? 'After' : 'Before';
        try {
            await handle.dotnetRef.invokeMethodAsync('OnRowDropped', draggingIndex, destIndex, dropPosition);
        } catch {
            // Component may have been torn down mid-drag — swallow the error gracefully.
        }
        cleanup();
    };

    const onDragEnd = () => cleanup();

    const cleanup = () => {
        if (cloneEl) { cloneEl.remove(); cloneEl = null; }
        if (dropLineEl) { dropLineEl.remove(); dropLineEl = null; }
        rootElement.querySelectorAll('.is-row-dragging-source')
            .forEach(n => n.classList.remove('is-row-dragging-source'));
        draggingIndex = null;
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

// ── B5: Frozen / Locked Columns ───────────────────────────────────────────────
//
// Offset model:
//   • Start-frozen (left in LTR): offset = cumulative width of all preceding Start-frozen cols.
//   • End-frozen (right in LTR): offset = cumulative width of all following End-frozen cols.
//
// CSS custom properties are written directly onto each locked <th>/<td>. The CSS class
// `.mar-datagrid-col--locked` uses `inset-inline-start`/`inset-inline-end` referencing
// these properties (the C# inline style also sets position:sticky + the pixel side value
// as a fallback). This JS pass runs on:
//   1. attachGrid mount (initial)
//   2. After column resize commits (mouseup)
//   3. After column reorder commits (drop → OnColumnReordered)

/**
 * Recalculate `--mar-datagrid-col-offset-start` and `--mar-datagrid-col-offset-end`
 * custom properties on every locked `<th>` and `<td>`. The values are derived from the
 * rendered widths of the `<col>` elements which are always up to date after resize/reorder.
 *
 * @param {string|HTMLElement} rootElementOrId - the grid's outer container element or its DOM id.
 */
export function recomputeFrozenOffsets(rootElementOrId) {
    const el = typeof rootElementOrId === 'string'
        ? document.getElementById(rootElementOrId)
        : rootElementOrId;
    if (!el) return;

    // The <colgroup><col data-column-id> sequence is the authoritative column order.
    const cols = Array.from(el.querySelectorAll('colgroup > col[data-column-id]'));

    // Start-frozen: walk left-to-right; accumulate offset before applying.
    let startOffset = 0;
    for (const col of cols) {
        const columnId = col.getAttribute('data-column-id');
        const locked = col.getAttribute('data-locked') === 'true';
        const position = col.getAttribute('data-frozen-position') ?? 'Start';
        if (!locked || position !== 'Start') continue;
        applyFrozenOffset(el, columnId, 'start', startOffset);
        startOffset += col.getBoundingClientRect().width;
    }

    // End-frozen: walk right-to-left; accumulate offset before applying.
    let endOffset = 0;
    for (const col of [...cols].reverse()) {
        const columnId = col.getAttribute('data-column-id');
        const locked = col.getAttribute('data-locked') === 'true';
        const position = col.getAttribute('data-frozen-position') ?? 'Start';
        if (!locked || position !== 'End') continue;
        applyFrozenOffset(el, columnId, 'end', endOffset);
        endOffset += col.getBoundingClientRect().width;
    }
}

/**
 * Write the CSS custom property for the frozen offset onto every cell (th + td) for a column.
 *
 * @param {HTMLElement} rootElement - the grid container
 * @param {string} columnId - matches data-column-id on <th> and locked <td> elements
 * @param {'start'|'end'} side - which inset axis to update
 * @param {number} offsetPx - pixel offset value
 */
function applyFrozenOffset(rootElement, columnId, side, offsetPx) {
    const escaped = CSS.escape(columnId);
    const cells = rootElement.querySelectorAll(
        `th[data-column-id="${escaped}"], td[data-column-id="${escaped}"]`);
    const prop = side === 'start'
        ? '--mar-datagrid-col-offset-start'
        : '--mar-datagrid-col-offset-end';
    cells.forEach(c => c.style.setProperty(prop, offsetPx + 'px'));
}
