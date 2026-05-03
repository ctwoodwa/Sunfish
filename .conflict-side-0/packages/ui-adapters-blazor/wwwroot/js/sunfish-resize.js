// Sunfish Shared Interop: Resize Module
// Provides pointer-based resize operations for Window, Splitter, DataGrid columns, etc.

let activeResize = null;

/**
 * Starts a resize operation on the given element.
 * @param {HTMLElement} element - The element being resized
 * @param {object} dotNetRef - DotNetObjectReference for callbacks
 * @param {object} options
 * @param {number} options.handleFlags - Bitmask of active resize directions
 * @param {number} options.clientX - Starting pointer X
 * @param {number} options.clientY - Starting pointer Y
 * @param {number} options.minWidth
 * @param {number} options.minHeight
 * @param {number} options.maxWidth
 * @param {number} options.maxHeight
 * @param {boolean} options.clampToParent
 * @param {string} options.updateMethod - .NET method name for resize updates
 * @param {string} options.endMethod - .NET method name for resize end
 */
export function startResize(element, dotNetRef, options) {
    if (activeResize) cancelResize();
    if (!element) return;

    const rect = element.getBoundingClientRect();

    activeResize = {
        element,
        dotNetRef,
        options,
        startX: options.clientX,
        startY: options.clientY,
        startTop: rect.top,
        startLeft: rect.left,
        startWidth: rect.width,
        startHeight: rect.height
    };

    document.body.style.userSelect = 'none';
    document.body.style.webkitUserSelect = 'none';

    document.addEventListener('pointermove', onPointerMove);
    document.addEventListener('pointerup', onPointerUp);
    document.addEventListener('pointercancel', onPointerUp);
}

// Handle direction flags (must match ResizeHandle C# enum)
const TOP = 1, RIGHT = 2, BOTTOM = 4, LEFT = 8;

function onPointerMove(e) {
    if (!activeResize) return;
    e.preventDefault();

    const { startX, startY, startWidth, startHeight, startTop, startLeft, options } = activeResize;
    const dx = e.clientX - startX;
    const dy = e.clientY - startY;
    const handle = options.handleFlags;

    let w = startWidth;
    let h = startHeight;
    let top = startTop;
    let left = startLeft;

    if (handle & RIGHT) w = startWidth + dx;
    if (handle & LEFT) { w = startWidth - dx; left = startLeft + dx; }
    if (handle & BOTTOM) h = startHeight + dy;
    if (handle & TOP) { h = startHeight - dy; top = startTop + dy; }

    // Apply constraints
    w = Math.max(options.minWidth || 50, Math.min(w, options.maxWidth || Infinity));
    h = Math.max(options.minHeight || 30, Math.min(h, options.maxHeight || Infinity));

    if (options.clampToParent && activeResize.element.parentElement) {
        const parentRect = activeResize.element.parentElement.getBoundingClientRect();
        w = Math.min(w, parentRect.width);
        h = Math.min(h, parentRect.height);
    }

    activeResize.dotNetRef.invokeMethodAsync(options.updateMethod, {
        width: w, height: h, top, left, activeHandle: handle
    }).catch(() => {});
}

function onPointerUp(e) {
    if (!activeResize) return;

    const rect = activeResize.element.getBoundingClientRect();
    const endMethod = activeResize.options.endMethod;
    const ref = activeResize.dotNetRef;
    const handle = activeResize.options.handleFlags;

    cleanup();

    ref.invokeMethodAsync(endMethod, {
        width: rect.width,
        height: rect.height,
        top: rect.top,
        left: rect.left,
        activeHandle: handle
    }).catch(() => {});

    activeResize = null;
}

function cleanup() {
    document.body.style.userSelect = '';
    document.body.style.webkitUserSelect = '';
    document.removeEventListener('pointermove', onPointerMove);
    document.removeEventListener('pointerup', onPointerUp);
    document.removeEventListener('pointercancel', onPointerUp);
}

/**
 * Cancels any active resize operation.
 */
export function cancelResize() {
    if (activeResize) {
        cleanup();
        activeResize = null;
    }
}

/**
 * Disposes module resources.
 */
export function dispose() {
    cancelResize();
}
