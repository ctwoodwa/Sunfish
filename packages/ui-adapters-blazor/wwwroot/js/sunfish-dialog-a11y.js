// Sunfish Shared Interop: Dialog A11y Module
// Provides focus trap, focus restoration, body scroll-lock, and Esc handling
// for SunfishDialog. All behaviors are purely client-side; the C# component
// calls attach/detach once per open/close cycle.
//
// Phase 3 a11y remediation (SYNTHESIS Theme 10). A single JS module is
// loaded lazily via `IJSRuntime.InvokeAsync<IJSObjectReference>("import", ...)`
// per the existing Sunfish JS module convention (see sunfish-positioning.js,
// sunfish-dragdrop.js, etc.).

const SCROLL_LOCK_CLASS = 'sf-dialog-open';
const FOCUSABLE_SELECTOR = [
    'a[href]',
    'area[href]',
    'input:not([disabled]):not([type="hidden"])',
    'select:not([disabled])',
    'textarea:not([disabled])',
    'button:not([disabled])',
    'iframe',
    'object',
    'embed',
    '[tabindex]:not([tabindex="-1"])',
    '[contenteditable="true"]'
].join(',');

// Map of dialog-id → trap descriptor. Supports nested dialogs:
// the most-recently-opened dialog owns the active trap.
const traps = new Map();

// Reference count for scroll-lock class on <body> — we cannot simply toggle
// because N nested dialogs should each contribute, and only removing the
// last one should clear `overflow: hidden`.
let scrollLockCount = 0;

/**
 * Attaches a focus trap, Esc handler, scroll-lock, and focus capture to a
 * dialog root element. Idempotent per dialogId.
 *
 * @param {string} dialogId          Stable id of the dialog root element.
 * @param {HTMLElement} dialogEl     The dialog root element.
 * @param {object} dotNetRef         DotNetObjectReference for Esc callback.
 * @param {object} options
 * @param {boolean} options.closeOnEsc  Whether Esc fires the close callback.
 * @param {string}  options.escMethod   Name of the [JSInvokable] close method.
 */
export function attach(dialogId, dialogEl, dotNetRef, options) {
    if (!dialogEl || traps.has(dialogId)) return;

    const previouslyFocused = document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;

    const descriptor = {
        dialogEl,
        dotNetRef,
        previouslyFocused,
        options: options || { closeOnEsc: true, escMethod: 'OnEscFromJs' }
    };

    const onKeyDown = (e) => handleKeyDown(dialogId, e);
    descriptor.onKeyDown = onKeyDown;
    document.addEventListener('keydown', onKeyDown, true);

    traps.set(dialogId, descriptor);

    // Scroll-lock is reference-counted so nested dialogs compose correctly.
    scrollLockCount += 1;
    if (scrollLockCount === 1) {
        document.body.classList.add(SCROLL_LOCK_CLASS);
    }

    // Move focus into the dialog on open. Prefer first focusable descendant;
    // fall back to the dialog root (which receives tabindex=-1 from the razor).
    const first = findFocusable(dialogEl)[0];
    try {
        if (first) {
            first.focus({ preventScroll: true });
        } else if (typeof dialogEl.focus === 'function') {
            dialogEl.focus({ preventScroll: true });
        }
    } catch { /* non-focusable element; ignore */ }
}

/**
 * Detaches the focus trap for a given dialog. Restores focus to the element
 * that had focus immediately before `attach` was called. Decrements the
 * scroll-lock reference count; removes the body class when count reaches 0.
 *
 * @param {string} dialogId
 */
export function detach(dialogId) {
    const descriptor = traps.get(dialogId);
    if (!descriptor) return;

    document.removeEventListener('keydown', descriptor.onKeyDown, true);
    traps.delete(dialogId);

    scrollLockCount = Math.max(0, scrollLockCount - 1);
    if (scrollLockCount === 0) {
        document.body.classList.remove(SCROLL_LOCK_CLASS);
    }

    // Restore focus to the element that had focus before the dialog opened.
    const prev = descriptor.previouslyFocused;
    if (prev && typeof prev.focus === 'function' && document.contains(prev)) {
        try { prev.focus({ preventScroll: true }); } catch { /* ignore */ }
    }
}

/**
 * Disposes the module — detaches any lingering traps. Called by C# DisposeAsync.
 */
export function dispose() {
    for (const id of Array.from(traps.keys())) {
        detach(id);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Internals
// ────────────────────────────────────────────────────────────────────────────

function handleKeyDown(dialogId, e) {
    // The most-recently-attached dialog owns any nested interaction. If this
    // trap isn't the active one (a newer dialog opened on top), defer.
    const active = getTopmostTrap();
    if (!active || active.id !== dialogId) return;

    const { dialogEl, dotNetRef, options } = active.descriptor;

    if (e.key === 'Escape' && options.closeOnEsc) {
        e.preventDefault();
        e.stopPropagation();
        try {
            dotNetRef.invokeMethodAsync(options.escMethod).catch(() => {});
        } catch { /* ref disposed */ }
        return;
    }

    if (e.key === 'Tab') {
        const focusables = findFocusable(dialogEl);
        if (focusables.length === 0) {
            e.preventDefault();
            try { dialogEl.focus({ preventScroll: true }); } catch { /* ignore */ }
            return;
        }

        const first = focusables[0];
        const last = focusables[focusables.length - 1];
        const current = document.activeElement;

        if (e.shiftKey) {
            if (current === first || !dialogEl.contains(current)) {
                e.preventDefault();
                try { last.focus({ preventScroll: true }); } catch { /* ignore */ }
            }
        } else {
            if (current === last || !dialogEl.contains(current)) {
                e.preventDefault();
                try { first.focus({ preventScroll: true }); } catch { /* ignore */ }
            }
        }
    }
}

function getTopmostTrap() {
    // Map preserves insertion order; last entry is the most recently attached.
    let topId = null;
    let topDescriptor = null;
    for (const [id, descriptor] of traps) {
        topId = id;
        topDescriptor = descriptor;
    }
    return topDescriptor ? { id: topId, descriptor: topDescriptor } : null;
}

function findFocusable(root) {
    if (!root) return [];
    const nodes = root.querySelectorAll(FOCUSABLE_SELECTOR);
    const result = [];
    for (const node of nodes) {
        if (node.offsetParent === null && getComputedStyle(node).position !== 'fixed') continue;
        if (node.hasAttribute('disabled')) continue;
        if (node.getAttribute('aria-hidden') === 'true') continue;
        result.push(node);
    }
    return result;
}
