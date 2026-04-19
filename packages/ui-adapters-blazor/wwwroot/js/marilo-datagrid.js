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
    // if (handle.options.columnResize) installColumnResize(handle);
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
