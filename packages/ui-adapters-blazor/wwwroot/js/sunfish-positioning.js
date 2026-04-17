// Marilo Shared Interop: Positioning Module
// Computes popup position relative to an anchor element, with flip and viewport clamping.

/**
 * Computes the optimal position for a popup element relative to an anchor.
 * @param {HTMLElement} anchor - The anchor element
 * @param {HTMLElement} popup - The popup element to position
 * @param {object} options - Positioning options
 * @param {string} options.placement - Preferred placement (e.g. "bottom", "top-start")
 * @param {number} options.offset - Pixel offset from anchor edge
 * @param {boolean} options.autoFlip - Whether to flip if overflowing viewport
 * @param {number} options.viewportMargin - Minimum margin from viewport edges
 * @returns {{ top: number, left: number, actualPlacement: string, wasFlipped: boolean }}
 */
export function computePosition(anchor, popup, options) {
    if (!anchor || !popup) {
        return { top: 0, left: 0, actualPlacement: options.placement || 'bottom', wasFlipped: false };
    }

    const anchorRect = anchor.getBoundingClientRect();
    const popupRect = popup.getBoundingClientRect();
    const viewport = { width: window.innerWidth, height: window.innerHeight };
    const offset = options.offset || 4;
    const margin = options.viewportMargin || 8;
    const placement = options.placement || 'bottom';

    let result = calculatePosition(anchorRect, popupRect, placement, offset);
    let wasFlipped = false;

    if (options.autoFlip) {
        const overflow = detectOverflow(result, popupRect, viewport, margin);
        if (overflow) {
            const flipped = getFlippedPlacement(placement);
            const flippedResult = calculatePosition(anchorRect, popupRect, flipped, offset);
            const flippedOverflow = detectOverflow(flippedResult, popupRect, viewport, margin);
            if (!flippedOverflow) {
                result = flippedResult;
                result.actualPlacement = flipped;
                wasFlipped = true;
            }
        }
    }

    result.wasFlipped = wasFlipped;
    if (!result.actualPlacement) result.actualPlacement = placement;

    return result;
}

function calculatePosition(anchorRect, popupRect, placement, offset) {
    let top = 0;
    let left = 0;
    const base = placement.split('-')[0];
    const alignment = placement.split('-')[1]; // 'start', 'end', or undefined

    switch (base) {
        case 'bottom':
            top = anchorRect.bottom + offset;
            left = getAlignedLeft(anchorRect, popupRect, alignment);
            break;
        case 'top':
            top = anchorRect.top - popupRect.height - offset;
            left = getAlignedLeft(anchorRect, popupRect, alignment);
            break;
        case 'left':
            top = getAlignedTop(anchorRect, popupRect, alignment);
            left = anchorRect.left - popupRect.width - offset;
            break;
        case 'right':
            top = getAlignedTop(anchorRect, popupRect, alignment);
            left = anchorRect.right + offset;
            break;
    }

    return { top, left, actualPlacement: placement, wasFlipped: false };
}

function getAlignedLeft(anchorRect, popupRect, alignment) {
    if (alignment === 'start') return anchorRect.left;
    if (alignment === 'end') return anchorRect.right - popupRect.width;
    return anchorRect.left + (anchorRect.width - popupRect.width) / 2;
}

function getAlignedTop(anchorRect, popupRect, alignment) {
    if (alignment === 'start') return anchorRect.top;
    if (alignment === 'end') return anchorRect.bottom - popupRect.height;
    return anchorRect.top + (anchorRect.height - popupRect.height) / 2;
}

function detectOverflow(pos, popupRect, viewport, margin) {
    return pos.top < margin ||
        pos.left < margin ||
        pos.top + popupRect.height > viewport.height - margin ||
        pos.left + popupRect.width > viewport.width - margin;
}

function getFlippedPlacement(placement) {
    const flipMap = {
        'top': 'bottom', 'bottom': 'top',
        'left': 'right', 'right': 'left',
        'top-start': 'bottom-start', 'bottom-start': 'top-start',
        'top-end': 'bottom-end', 'bottom-end': 'top-end',
        'left-start': 'right-start', 'right-start': 'left-start',
        'left-end': 'right-end', 'right-end': 'left-end'
    };
    return flipMap[placement] || placement;
}

/**
 * Disposes module resources (stateless — no-op for now).
 */
export function dispose() {
    // Stateless module; no cleanup needed.
}
