// Sunfish Shared Interop: Graphics Module
// Provides text measurement, DPI queries, and element size for Chart/Diagram/Map rendering.

let measureCanvas = null;

/**
 * Measures the rendered dimensions of a text string with a given font.
 * @param {string} text
 * @param {string} font - CSS font shorthand (e.g. "14px Arial")
 * @returns {{ width: number, height: number }}
 */
export function measureText(text, font) {
    if (!measureCanvas) {
        measureCanvas = document.createElement('canvas');
    }
    const ctx = measureCanvas.getContext('2d');
    ctx.font = font;
    const metrics = ctx.measureText(text);
    // Approximate height from font size
    const heightMatch = font.match(/(\d+(?:\.\d+)?)\s*px/);
    const height = heightMatch ? parseFloat(heightMatch[1]) * 1.2 : 16;
    return { width: metrics.width, height };
}

/**
 * Gets the device pixel ratio for high-DPI rendering.
 * @returns {number}
 */
export function getDevicePixelRatio() {
    return window.devicePixelRatio || 1;
}

/**
 * Gets the computed dimensions of an element (typically SVG or canvas).
 * @param {HTMLElement} element
 * @returns {{ x: number, y: number, width: number, height: number }}
 */
export function getRenderedSize(element) {
    if (!element) return { x: 0, y: 0, width: 0, height: 0 };
    const rect = element.getBoundingClientRect();
    return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
}

/**
 * Disposes module resources.
 */
export function dispose() {
    measureCanvas = null;
}
