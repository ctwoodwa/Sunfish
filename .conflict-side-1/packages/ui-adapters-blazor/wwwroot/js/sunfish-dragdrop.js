// Sunfish Shared Interop: Drag & Drop Module
// Provides pointer-based drag operations for Window, Scheduler, etc.

let activeDrag = null;

/**
 * Starts a drag operation from the given element.
 * @param {HTMLElement} element - The element being dragged
 * @param {object} dotNetRef - DotNetObjectReference for callbacks
 * @param {object} options
 * @param {number} options.clientX - Starting pointer X
 * @param {number} options.clientY - Starting pointer Y
 * @param {string} [options.containmentSelector] - CSS selector to constrain drag
 * @param {boolean} [options.disableTextSelection] - Disable text selection during drag
 * @param {string} options.updateMethod - .NET method name for move updates
 * @param {string} options.endMethod - .NET method name for drag end
 */
export function startDrag(element, dotNetRef, options) {
    if (activeDrag) cancelDrag();
    if (!element) return;

    const startX = options.clientX;
    const startY = options.clientY;
    const containment = options.containmentSelector
        ? document.querySelector(options.containmentSelector)
        : null;

    activeDrag = { element, dotNetRef, startX, startY, containment, options };

    if (options.disableTextSelection) {
        document.body.style.userSelect = 'none';
        document.body.style.webkitUserSelect = 'none';
    }

    document.addEventListener('pointermove', onPointerMove);
    document.addEventListener('pointerup', onPointerUp);
    document.addEventListener('pointercancel', onPointerCancel);
}

function onPointerMove(e) {
    if (!activeDrag) return;
    e.preventDefault();

    const dx = e.clientX - activeDrag.startX;
    const dy = e.clientY - activeDrag.startY;

    activeDrag.dotNetRef.invokeMethodAsync(activeDrag.options.updateMethod, {
        clientX: e.clientX,
        clientY: e.clientY,
        deltaX: dx,
        deltaY: dy
    }).catch(() => {});
}

function onPointerUp(e) {
    if (!activeDrag) return;

    const dx = e.clientX - activeDrag.startX;
    const dy = e.clientY - activeDrag.startY;

    cleanup();

    activeDrag.dotNetRef.invokeMethodAsync(activeDrag.options.endMethod, {
        finalX: e.clientX,
        finalY: e.clientY,
        totalDeltaX: dx,
        totalDeltaY: dy,
        wasCancelled: false
    }).catch(() => {});

    activeDrag = null;
}

function onPointerCancel() {
    if (!activeDrag) return;
    const ref = activeDrag.dotNetRef;
    const endMethod = activeDrag.options.endMethod;
    cleanup();

    ref.invokeMethodAsync(endMethod, {
        finalX: 0,
        finalY: 0,
        totalDeltaX: 0,
        totalDeltaY: 0,
        wasCancelled: true
    }).catch(() => {});

    activeDrag = null;
}

function cleanup() {
    if (activeDrag?.options.disableTextSelection) {
        document.body.style.userSelect = '';
        document.body.style.webkitUserSelect = '';
    }
    document.removeEventListener('pointermove', onPointerMove);
    document.removeEventListener('pointerup', onPointerUp);
    document.removeEventListener('pointercancel', onPointerCancel);
}

/**
 * Cancels any active drag operation.
 */
export function cancelDrag() {
    if (activeDrag) {
        onPointerCancel();
    }
}

/**
 * Disposes module resources.
 */
export function dispose() {
    cancelDrag();
}
