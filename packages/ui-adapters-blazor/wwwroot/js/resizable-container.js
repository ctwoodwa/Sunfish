// MariloResizableContainer JS Interop Module
// Handles pointer-based resize, ResizeObserver, keyboard resize, and optional persistence.

let containerEl = null;
let dotNetRef = null;
let options = {};
let resizeObserver = null;
let activeEdge = 0;
let isDragging = false;
let startX = 0;
let startY = 0;
let startWidth = 0;
let startHeight = 0;

// Edge flag constants (must match MariloResizeEdges C# enum)
const EDGE_RIGHT = 1;
const EDGE_BOTTOM = 2;
const EDGE_LEFT = 4;
const EDGE_TOP = 8;

const SMALL_STEP = 4;
const LARGE_STEP = 20;

/**
 * Initialize the resizable container interop.
 * @param {HTMLElement} container - The container element ref
 * @param {object} dotNet - DotNetObjectReference for callbacks
 * @param {object} opts - Configuration options
 */
export function init(container, dotNet, opts) {
    containerEl = container;
    dotNetRef = dotNet;
    options = opts || {};

    // Attach pointer listeners to all handle buttons
    const handles = containerEl.querySelectorAll('.mar-resizable-container__handle, .mar-bs-resizable-container__handle');
    handles.forEach(handle => {
        handle.addEventListener('pointerdown', onPointerDown);
        if (options.keyboardResizeEnabled) {
            handle.addEventListener('keydown', onKeyDown);
        }
    });

    // Set up ResizeObserver
    if (options.observeSizeChanges) {
        resizeObserver = new ResizeObserver(entries => {
            for (const entry of entries) {
                const { width, height } = entry.contentRect;
                dotNetRef.invokeMethodAsync('OnObservedSizeChangedFromJs', width, height);
            }
        });
        resizeObserver.observe(containerEl);
    }

    // Restore persisted size
    if (options.persistSize && options.persistKey) {
        try {
            const stored = localStorage.getItem(`marilo-rc-${options.persistKey}`);
            if (stored) {
                const { w, h } = JSON.parse(stored);
                containerEl.style.width = `${w}px`;
                containerEl.style.height = `${h}px`;
                dotNetRef.invokeMethodAsync('OnPersistedSizeRestoredFromJs', w, h);
            }
        } catch { /* ignore storage errors */ }
    }
}

function onPointerDown(e) {
    if (!options.enabled) return;

    e.preventDefault();
    e.stopPropagation();

    const handle = e.currentTarget;
    activeEdge = getEdgeFromHandle(handle);
    if (activeEdge === 0) return;

    isDragging = true;
    startX = e.clientX;
    startY = e.clientY;

    const rect = containerEl.getBoundingClientRect();
    startWidth = rect.width;
    startHeight = rect.height;

    if (options.disableTextSelection) {
        document.body.style.userSelect = 'none';
        document.body.style.webkitUserSelect = 'none';
    }

    handle.setPointerCapture(e.pointerId);
    handle.addEventListener('pointermove', onPointerMove);
    handle.addEventListener('pointerup', onPointerUp);
    handle.addEventListener('pointercancel', onPointerUp);

    dotNetRef.invokeMethodAsync('OnResizeStartFromJs', startWidth, startHeight, activeEdge);
}

function onPointerMove(e) {
    if (!isDragging) return;

    const dx = e.clientX - startX;
    const dy = e.clientY - startY;
    let newWidth = startWidth;
    let newHeight = startHeight;

    if (activeEdge & EDGE_RIGHT) newWidth = startWidth + dx;
    if (activeEdge & EDGE_LEFT) newWidth = startWidth - dx;
    if (activeEdge & EDGE_BOTTOM) newHeight = startHeight + dy;
    if (activeEdge & EDGE_TOP) newHeight = startHeight - dy;

    // Apply constraints
    ({ width: newWidth, height: newHeight } = applyConstraints(newWidth, newHeight));

    if (!options.useGhostOutline) {
        containerEl.style.width = `${newWidth}px`;
        containerEl.style.height = `${newHeight}px`;

        // Adjust position for top/left edge dragging
        if (activeEdge & EDGE_LEFT) {
            const offsetX = startWidth - newWidth;
            containerEl.style.marginLeft = `${offsetX}px`;
        }
        if (activeEdge & EDGE_TOP) {
            const offsetY = startHeight - newHeight;
            containerEl.style.marginTop = `${offsetY}px`;
        }
    }

    dotNetRef.invokeMethodAsync('OnResizingFromJs', newWidth, newHeight, activeEdge);
}

function onPointerUp(e) {
    if (!isDragging) return;

    isDragging = false;

    const handle = e.currentTarget;
    handle.removeEventListener('pointermove', onPointerMove);
    handle.removeEventListener('pointerup', onPointerUp);
    handle.removeEventListener('pointercancel', onPointerUp);

    if (options.disableTextSelection) {
        document.body.style.userSelect = '';
        document.body.style.webkitUserSelect = '';
    }

    const rect = containerEl.getBoundingClientRect();
    const finalWidth = rect.width;
    const finalHeight = rect.height;

    // Persist if enabled
    if (options.persistSize && options.persistKey) {
        try {
            localStorage.setItem(
                `marilo-rc-${options.persistKey}`,
                JSON.stringify({ w: finalWidth, h: finalHeight })
            );
        } catch { /* ignore storage errors */ }
    }

    dotNetRef.invokeMethodAsync('OnResizeEndFromJs', finalWidth, finalHeight, activeEdge);
    activeEdge = 0;
}

function onKeyDown(e) {
    if (!options.enabled || !options.keyboardResizeEnabled) return;

    const step = e.shiftKey ? LARGE_STEP : SMALL_STEP;
    let dw = 0;
    let dh = 0;

    switch (e.key) {
        case 'ArrowRight': dw = step; break;
        case 'ArrowLeft': dw = -step; break;
        case 'ArrowDown': dh = step; break;
        case 'ArrowUp': dh = -step; break;
        default: return;
    }

    e.preventDefault();

    const rect = containerEl.getBoundingClientRect();
    let newWidth = rect.width + dw;
    let newHeight = rect.height + dh;

    ({ width: newWidth, height: newHeight } = applyConstraints(newWidth, newHeight));

    containerEl.style.width = `${newWidth}px`;
    containerEl.style.height = `${newHeight}px`;

    const handle = e.currentTarget;
    const handleEdge = getEdgeFromHandle(handle);

    dotNetRef.invokeMethodAsync('OnResizeStartFromJs', rect.width, rect.height, handleEdge);
    dotNetRef.invokeMethodAsync('OnResizeEndFromJs', newWidth, newHeight, handleEdge);
}

function applyConstraints(width, height) {
    // Enforce minimum sizes
    if (width < 50) width = 50;
    if (height < 30) height = 30;

    // Clamp to parent if enabled
    if (options.clampToParent && containerEl.parentElement) {
        const parentRect = containerEl.parentElement.getBoundingClientRect();
        if (width > parentRect.width) width = parentRect.width;
        if (height > parentRect.height) height = parentRect.height;
    }

    return { width, height };
}

function getEdgeFromHandle(handle) {
    const classes = handle.className;
    if (classes.includes('--bottom-right')) return EDGE_BOTTOM | EDGE_RIGHT;
    if (classes.includes('--top-left')) return EDGE_TOP | EDGE_LEFT;
    if (classes.includes('--top-right')) return EDGE_TOP | EDGE_RIGHT;
    if (classes.includes('--bottom-left')) return EDGE_BOTTOM | EDGE_LEFT;
    if (classes.includes('--right')) return EDGE_RIGHT;
    if (classes.includes('--bottom')) return EDGE_BOTTOM;
    if (classes.includes('--left')) return EDGE_LEFT;
    if (classes.includes('--top')) return EDGE_TOP;
    return EDGE_BOTTOM | EDGE_RIGHT; // default
}

/**
 * Focus the first resize handle.
 */
export function focusHandle() {
    if (!containerEl) return;
    const handle = containerEl.querySelector('.mar-resizable-container__handle, .mar-bs-resizable-container__handle');
    if (handle) handle.focus();
}

/**
 * Clean up all listeners and observers.
 */
export function dispose() {
    if (resizeObserver) {
        resizeObserver.disconnect();
        resizeObserver = null;
    }

    if (containerEl) {
        const handles = containerEl.querySelectorAll('.mar-resizable-container__handle, .mar-bs-resizable-container__handle');
        handles.forEach(handle => {
            handle.removeEventListener('pointerdown', onPointerDown);
            handle.removeEventListener('keydown', onKeyDown);
        });
    }

    containerEl = null;
    dotNetRef = null;
    options = {};
    isDragging = false;
}
