// ESM module for SunfishDataSheet — clipboard, focus, scroll, keyboard.
// Loaded lazily via import() in SunfishDataSheet.Interop.cs.

const handlers = new Map();

/**
 * Copies text to the system clipboard.
 * @param {string} text - TSV or plain text to copy.
 */
export function copyToClipboard(text) {
    return navigator.clipboard.writeText(text);
}

/**
 * Reads text from the system clipboard.
 * @returns {Promise<string>} Clipboard text content.
 */
export async function readClipboard() {
    return await navigator.clipboard.readText();
}

/**
 * Scrolls a row with the given key into view.
 * @param {string} gridId - The grid element ID.
 * @param {string} rowKey - The data-row-key attribute value.
 */
export function scrollToRow(gridId, rowKey) {
    const grid = document.getElementById(gridId);
    if (!grid) return;
    const row = grid.querySelector(`[data-row-key="${rowKey}"]`);
    if (row) {
        row.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
}

/**
 * Focuses a specific cell input element.
 * @param {string} gridId - The grid element ID.
 * @param {string} rowKey - The data-row-key attribute value.
 * @param {string} field - The data-field attribute value.
 */
export function focusCell(gridId, rowKey, field) {
    const grid = document.getElementById(gridId);
    if (!grid) return;
    const row = grid.querySelector(`[data-row-key="${rowKey}"]`);
    if (!row) return;
    const cell = row.querySelector(`[data-field="${field}"]`);
    if (!cell) return;
    const input = cell.querySelector('input, select, textarea');
    if (input) {
        input.focus();
    } else {
        cell.focus();
    }
}

/**
 * Registers a keydown handler on the grid element that dispatches to .NET.
 * @param {string} gridId - The grid element ID.
 * @param {object} dotNetRef - .NET object reference for callbacks.
 */
export function registerKeydownHandler(gridId, dotNetRef) {
    const grid = document.getElementById(gridId);
    if (!grid) return;

    const handler = async (e) => {
        const key = e.key;
        const ctrl = e.ctrlKey || e.metaKey;
        const shift = e.shiftKey;

        // Are we currently focused inside an editor input? The .NET side
        // tracks _isEditMode, but JS needs its own proxy so it can decide
        // whether to preventDefault for printable characters. When focus
        // sits inside any input/select/textarea descendant of the grid,
        // we're in edit mode and must let the key reach the editor.
        const activeEl = document.activeElement;
        const isInEditor = activeEl
            && activeEl !== grid
            && grid.contains(activeEl)
            && /^(INPUT|SELECT|TEXTAREA)$/.test(activeEl.tagName);

        // Prevent default for grid-handled shortcuts
        if (ctrl && (key === 's' || key === 'z' || key === 'c' || key === 'v' || key === 'd')) {
            e.preventDefault();
        }
        if (['Tab', 'Enter', 'Escape', 'F2', 'Delete',
             'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(key)) {
            e.preventDefault();
        }

        // V07.3 — Space toggles checkboxes when not in an editor. Also
        // suppressed to prevent page scroll when the grid has focus.
        if (key === ' ' && !isInEditor) {
            e.preventDefault();
        }

        // V07.2 — Printable characters begin edit mode on the active cell.
        // Only suppress default when NOT already inside an editor, so the
        // user can still type into open cell editors normally.
        if (!ctrl && !isInEditor && key.length === 1 && key !== ' ') {
            e.preventDefault();
        }

        // Handle Ctrl+C in JS (needs clipboard API access)
        if (ctrl && key === 'c') {
            const activeCell = grid.querySelector('.sf-datasheet__cell--active');
            if (activeCell) {
                // V04.4 — Prefer data-raw-value when present (columns with a
                // Format delegate set this attribute so copy yields the raw
                // property value rather than the formatted display string).
                // Fall back to textContent when no raw value is available.
                const rawValue = activeCell.getAttribute('data-raw-value');
                const text = (rawValue ?? activeCell.textContent ?? '').trim();
                await copyToClipboard(text);
            }
            return;
        }

        // Handle Ctrl+V in JS (needs clipboard API access)
        if (ctrl && key === 'v') {
            try {
                const clipText = await readClipboard();
                if (clipText) {
                    await dotNetRef.invokeMethodAsync('PasteFromClipboard', clipText);
                }
            } catch (err) {
                console.warn('Clipboard read failed:', err);
            }
            return;
        }

        // All other keys dispatched to .NET
        try {
            await dotNetRef.invokeMethodAsync('HandleKeyDown', key, ctrl, shift);
        } catch (err) {
            // Component may have been disposed
        }
    };

    grid.addEventListener('keydown', handler);
    grid.setAttribute('tabindex', '0');
    handlers.set(gridId, handler);
}

/**
 * Removes the keydown handler from the grid element.
 * @param {string} gridId - The grid element ID.
 */
export function unregisterKeydownHandler(gridId) {
    const grid = document.getElementById(gridId);
    const handler = handlers.get(gridId);
    if (grid && handler) {
        grid.removeEventListener('keydown', handler);
    }
    handlers.delete(gridId);
}
