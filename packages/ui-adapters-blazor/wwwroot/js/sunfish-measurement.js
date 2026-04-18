// Sunfish Shared Interop: Element Measurement Module
// Provides getBoundingClientRect and viewport measurement for all Sunfish components.

/**
 * Gets the bounding client rect of the given element.
 * @param {HTMLElement} element
 * @returns {{ x: number, y: number, width: number, height: number }}
 */
export function getBoundingClientRect(element) {
    if (!element) return { x: 0, y: 0, width: 0, height: 0 };
    const rect = element.getBoundingClientRect();
    return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
}

/**
 * Gets the current viewport dimensions and scroll offsets.
 * @returns {{ width: number, height: number, scrollX: number, scrollY: number }}
 */
export function getViewport() {
    return {
        width: window.innerWidth,
        height: window.innerHeight,
        scrollX: window.scrollX,
        scrollY: window.scrollY
    };
}

/**
 * Gets the widths of all direct children of the given element.
 * @param {HTMLElement} element
 * @returns {number[]}
 */
export function getChildWidths(element) {
    if (!element) return [];
    return Array.from(element.children).map(c => c.getBoundingClientRect().width);
}

/**
 * Focuses the element with the given ID.
 * @param {string} elementId - The DOM element ID to focus
 */
export function focusById(elementId) {
    document.getElementById(elementId)?.focus();
}

/**
 * Disposes module resources (stateless — no-op).
 */
export function dispose() {
    // Stateless module; no cleanup needed.
}
