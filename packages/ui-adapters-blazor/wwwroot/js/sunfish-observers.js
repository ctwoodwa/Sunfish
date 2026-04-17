// Marilo Shared Interop: Observers Module
// Wraps ResizeObserver and IntersectionObserver for Marilo components.

const resizeObservers = new Map();
const intersectionObservers = new Map();
let nextId = 1;

/**
 * Starts observing size changes on the given element.
 * @param {HTMLElement} element
 * @param {object} dotNetRef - DotNetObjectReference for callbacks
 * @param {string} callbackMethod - The .NET method name to invoke
 * @returns {number} An observer handle ID for later disposal.
 */
export function observeResize(element, dotNetRef, callbackMethod) {
    if (!element) return -1;
    const id = nextId++;
    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            const { width, height } = entry.contentRect;
            const rect = element.getBoundingClientRect();
            dotNetRef.invokeMethodAsync(callbackMethod, {
                x: rect.x, y: rect.y, width, height
            });
        }
    });
    observer.observe(element);
    resizeObservers.set(id, { observer, element });
    return id;
}

/**
 * Stops a resize observation.
 * @param {number} id - The handle returned by observeResize.
 */
export function unobserveResize(id) {
    const entry = resizeObservers.get(id);
    if (entry) {
        entry.observer.disconnect();
        resizeObservers.delete(id);
    }
}

/**
 * Starts observing intersection changes on the given element.
 * @param {HTMLElement} element
 * @param {object} dotNetRef - DotNetObjectReference for callbacks
 * @param {string} callbackMethod - The .NET method name to invoke
 * @param {number[]} [thresholds] - IntersectionObserver thresholds (default [0, 0.5, 1])
 * @returns {number} An observer handle ID for later disposal.
 */
export function observeIntersection(element, dotNetRef, callbackMethod, thresholds) {
    if (!element) return -1;
    const id = nextId++;
    const observer = new IntersectionObserver(entries => {
        for (const entry of entries) {
            dotNetRef.invokeMethodAsync(callbackMethod, {
                isIntersecting: entry.isIntersecting,
                intersectionRatio: entry.intersectionRatio
            });
        }
    }, { threshold: thresholds || [0, 0.5, 1] });
    observer.observe(element);
    intersectionObservers.set(id, { observer, element });
    return id;
}

/**
 * Stops an intersection observation.
 * @param {number} id - The handle returned by observeIntersection.
 */
export function unobserveIntersection(id) {
    const entry = intersectionObservers.get(id);
    if (entry) {
        entry.observer.disconnect();
        intersectionObservers.delete(id);
    }
}

/**
 * Disposes all active observers.
 */
export function dispose() {
    for (const [, entry] of resizeObservers) {
        entry.observer.disconnect();
    }
    resizeObservers.clear();

    for (const [, entry] of intersectionObservers) {
        entry.observer.disconnect();
    }
    intersectionObservers.clear();
}
