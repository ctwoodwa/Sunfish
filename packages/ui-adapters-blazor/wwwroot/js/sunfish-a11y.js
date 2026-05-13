// Sunfish a11y primitives — live announcer + focus trap
// Bridges ILiveAnnouncer and IFocusTrap Blazor adapters to the browser.
// Loaded lazily as an ES module per the Sunfish JS module convention.

// ── Live Announcer ───────────────────────────────────────────────────────────

let _announceRegion = null;

function getAnnounceRegion() {
    if (_announceRegion && document.body.contains(_announceRegion)) {
        return _announceRegion;
    }
    const el = document.createElement('div');
    el.setAttribute('aria-atomic', 'true');
    el.style.cssText =
        'position:absolute;width:1px;height:1px;overflow:hidden;' +
        'clip:rect(0,0,0,0);white-space:nowrap;border:0;';
    document.body.appendChild(el);
    _announceRegion = el;
    return el;
}

/**
 * Queues a screen-reader announcement.
 * @param {string} message     Localized text to announce.
 * @param {string} politeness  'polite' | 'assertive' | 'critical'
 *                             'critical' maps to assertive (highest browser priority).
 */
export function announce(message, politeness) {
    const region = getAnnounceRegion();
    const live = politeness === 'polite' ? 'polite' : 'assertive';
    region.setAttribute('aria-live', live);
    // Clear then re-set forces re-announcement even for identical strings.
    region.textContent = '';
    requestAnimationFrame(() => { region.textContent = message ?? ''; });
}

// ── Focus Trap ───────────────────────────────────────────────────────────────

const _traps = new Map();

const FOCUSABLE_SELECTOR =
    'a[href],button:not([disabled]),' +
    'input:not([disabled]):not([type="hidden"]),' +
    'select:not([disabled]),textarea:not([disabled]),' +
    '[tabindex]:not([tabindex="-1"]),[contenteditable="true"]';

function getFocusable(root) {
    if (!root) return [];
    return Array.from(root.querySelectorAll(FOCUSABLE_SELECTOR)).filter(el => {
        if (el.closest('[aria-hidden="true"]')) return false;
        return true;
    });
}

function focusFirst(container) {
    const first = getFocusable(container)[0];
    try { (first ?? container).focus({ preventScroll: true }); } catch { /* non-focusable root */ }
}

function handleTrapKeyDown(containerId, e) {
    const trap = _traps.get(containerId);
    if (!trap) return;

    if (e.key === 'Escape') {
        e.preventDefault();
        releaseFocus(containerId);
        return;
    }

    if (e.key !== 'Tab') return;
    const focusables = getFocusable(trap.container);
    if (!focusables.length) { e.preventDefault(); return; }

    const first = focusables[0];
    const last  = focusables[focusables.length - 1];

    if (e.shiftKey) {
        if (document.activeElement === first) {
            e.preventDefault();
            try { last.focus({ preventScroll: true }); } catch { /* ignore */ }
        }
    } else {
        if (document.activeElement === last) {
            e.preventDefault();
            try { first.focus({ preventScroll: true }); } catch { /* ignore */ }
        }
    }
}

/**
 * Traps focus within the element identified by containerId.
 * The element must exist in the DOM (i.e., the component has rendered).
 * Escape releases the trap per WCAG SC 2.1.2.
 *
 * @param {string} containerId  id attribute OR data-focustrap-id of the container element.
 */
export function trapFocus(containerId) {
    if (_traps.has(containerId)) return; // already active — per IFocusTrap re-entry contract: ignore

    const container =
        document.getElementById(containerId) ??
        document.querySelector(`[data-focustrap-id="${CSS.escape(containerId)}"]`);

    if (!container) return;

    const prior = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const onKeyDown = (e) => handleTrapKeyDown(containerId, e);
    document.addEventListener('keydown', onKeyDown, true);
    _traps.set(containerId, { container, prior, onKeyDown });
    focusFirst(container);
}

/**
 * Releases the focus trap and restores prior focus per WCAG SC 2.4.3.
 * @param {string} containerId
 */
export function releaseFocus(containerId) {
    const trap = _traps.get(containerId);
    if (!trap) return;
    document.removeEventListener('keydown', trap.onKeyDown, true);
    _traps.delete(containerId);
    if (trap.prior && document.contains(trap.prior)) {
        try { trap.prior.focus({ preventScroll: true }); } catch { /* ignore */ }
    }
}

/**
 * Disposes all active traps (called by C# DisposeAsync).
 */
export function dispose() {
    for (const id of Array.from(_traps.keys())) {
        releaseFocus(id);
    }
}
