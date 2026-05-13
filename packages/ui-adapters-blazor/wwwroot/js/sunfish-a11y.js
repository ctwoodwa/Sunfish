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
 *                             TRUST BOUNDARY: callers must not pass untrusted/unbounded
 *                             content here. textContent is used (no innerHTML parse), but
 *                             very long strings can cause screen-reader DoS.
 * @param {string} politeness  'polite' | 'assertive' | 'critical'
 *                             'critical' maps to 'assertive' (highest browser priority).
 *                             The Critical→Assertive collapse is intentional on the web;
 *                             MAUI uses AutomationNotificationProcessing.ImportantAll for
 *                             Critical (see MauiLiveAnnouncer.cs). This asymmetry is
 *                             documented in ADR 0077 §6.
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
//
// WCAG SC 2.1.2 (No Keyboard Trap) compliance:
// Multiple simultaneous traps are supported (e.g., modal inside a drawer) via a
// stack: only the TOPMOST (most-recently-entered) trap handles keyboard events.
// Releasing the inner trap restores the outer one automatically.

const _traps = new Map();
const _trapStack = []; // ordered by entry time; last element = active (innermost) trap

const FOCUSABLE_SELECTOR =
    'a[href],button:not([disabled]),' +
    'input:not([disabled]):not([type="hidden"]),' +
    'select:not([disabled]),textarea:not([disabled]),' +
    '[tabindex]:not([tabindex="-1"]),[contenteditable="true"]';

function getFocusable(root) {
    if (!root) return [];
    return Array.from(root.querySelectorAll(FOCUSABLE_SELECTOR)).filter(el => {
        if (el.closest('[aria-hidden="true"]')) return false;
        if (el.closest('[inert]')) return false;
        if (el.hasAttribute('disabled')) return false;
        const style = getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden') return false;
        return true;
    });
}

function focusFirst(container) {
    const first = getFocusable(container)[0];
    try { (first ?? container).focus({ preventScroll: true }); } catch { /* non-focusable root */ }
}

function handleTrapKeyDown(containerId, e) {
    // Only the active (topmost) trap handles keyboard events.
    // This prevents outer traps from intercepting Tab/Escape when an inner trap is active.
    if (_trapStack.length === 0 || _trapStack[_trapStack.length - 1] !== containerId) return;

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
 * If another trap is already active, this trap is pushed onto the stack;
 * the prior trap resumes when this one is released.
 *
 * @param {string} containerId  id attribute OR data-focustrap-id of the container element.
 */
export function trapFocus(containerId) {
    if (_traps.has(containerId)) return; // already active — re-entry no-op per IFocusTrap contract

    const container =
        document.getElementById(containerId) ??
        document.querySelector(`[data-focustrap-id="${CSS.escape(containerId)}"]`);

    if (!container) return;

    const prior = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const onKeyDown = (e) => handleTrapKeyDown(containerId, e);
    document.addEventListener('keydown', onKeyDown, true);
    _traps.set(containerId, { container, prior, onKeyDown });
    _trapStack.push(containerId);
    focusFirst(container);
}

/**
 * Releases the focus trap and restores prior focus per WCAG SC 2.4.3.
 * If a prior trap was suspended by this one, it becomes active again.
 * @param {string} containerId
 */
export function releaseFocus(containerId) {
    const trap = _traps.get(containerId);
    if (!trap) return;
    document.removeEventListener('keydown', trap.onKeyDown, true);
    _traps.delete(containerId);
    const idx = _trapStack.indexOf(containerId);
    if (idx !== -1) _trapStack.splice(idx, 1);
    if (trap.prior && document.contains(trap.prior)) {
        try { trap.prior.focus({ preventScroll: true }); } catch { /* ignore */ }
    }
}

/**
 * Disposes all active traps (called by C# DisposeAsync).
 */
export function dispose() {
    for (const id of Array.from(_trapStack).reverse()) {
        releaseFocus(id);
    }
}
