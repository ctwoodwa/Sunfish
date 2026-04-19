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
    // if (handle.options.columnReorder) installColumnReorder(handle);
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
