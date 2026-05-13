import React, { useEffect, useRef, useCallback } from 'react';

const FOCUSABLE_SELECTOR =
  'a[href],button:not([disabled]),' +
  'input:not([disabled]):not([type="hidden"]),' +
  'select:not([disabled]),textarea:not([disabled]),' +
  '[tabindex]:not([tabindex="-1"]),[contenteditable="true"]';

function getFocusable(root: HTMLElement): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(
    (el) => !el.closest('[aria-hidden="true"]'),
  );
}

export interface FocusTrapProps {
  /** Whether the focus trap is currently active. */
  active: boolean;
  /** Called when Escape is pressed inside the trap (WCAG SC 2.1.2 escape route). */
  onEscape?: () => void;
  children: React.ReactNode;
}

/**
 * React focus-trap component per ADR 0077 §4 + WCAG 2.2 SC 2.4.3 + SC 2.1.2.
 * Traps keyboard focus within its children when `active` is true.
 * Pressing Escape calls `onEscape` (consumer is responsible for deactivation).
 */
export function FocusTrap({ active, onEscape, children }: FocusTrapProps): React.ReactElement {
  const containerRef = useRef<HTMLDivElement>(null);
  const priorFocusRef = useRef<HTMLElement | null>(null);

  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (!active || !containerRef.current) return;

      if (e.key === 'Escape') {
        e.preventDefault();
        onEscape?.();
        return;
      }

      if (e.key !== 'Tab') return;

      const focusables = getFocusable(containerRef.current);
      if (!focusables.length) { e.preventDefault(); return; }

      const first = focusables[0];
      const last = focusables[focusables.length - 1];

      if (e.shiftKey) {
        if (document.activeElement === first) {
          e.preventDefault();
          last.focus({ preventScroll: true });
        }
      } else {
        if (document.activeElement === last) {
          e.preventDefault();
          first.focus({ preventScroll: true });
        }
      }
    },
    [active, onEscape],
  );

  useEffect(() => {
    if (!active) return;

    // Save prior focus for restoration on deactivation (WCAG SC 2.4.3).
    priorFocusRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;

    // Move focus into the trap.
    const container = containerRef.current;
    if (container) {
      const first = getFocusable(container)[0];
      (first ?? container).focus({ preventScroll: true });
    }

    document.addEventListener('keydown', handleKeyDown, true);
    return () => {
      document.removeEventListener('keydown', handleKeyDown, true);
      // Capture and clear before restore to prevent double-cleanup in StrictMode.
      const prior = priorFocusRef.current;
      priorFocusRef.current = null;
      if (prior !== null && document.contains(prior)) {
        prior.focus({ preventScroll: true });
      }
    };
  }, [active, handleKeyDown]);

  return (
    <div ref={containerRef} tabIndex={active ? -1 : undefined}>
      {children}
    </div>
  );
}
