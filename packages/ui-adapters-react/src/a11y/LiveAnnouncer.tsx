import React, { useEffect, useRef } from 'react';

export type LiveRegionPoliteness = 'polite' | 'assertive' | 'critical';

/**
 * React live-region announcer per ADR 0077 §4 + §6 + WCAG 2.2 SC 4.1.3.
 * Renders a visually-hidden aria-live region and exposes an `announce`
 * imperative handle via `ReactLiveAnnouncer.announce(msg, politeness)`.
 */
export interface LiveAnnouncerHandle {
  announce(message: string, politeness: LiveRegionPoliteness): void;
}

const _regionRef = { current: null as HTMLElement | null };

function getOrCreateRegion(): HTMLElement {
  if (_regionRef.current && document.body.contains(_regionRef.current)) {
    return _regionRef.current;
  }
  const el = document.createElement('div');
  el.setAttribute('aria-atomic', 'true');
  el.style.cssText =
    'position:absolute;width:1px;height:1px;overflow:hidden;' +
    'clip:rect(0,0,0,0);white-space:nowrap;border:0;';
  document.body.appendChild(el);
  _regionRef.current = el;
  return el;
}

/**
 * Singleton announce function. Mirrors `ILiveAnnouncer.Announce` contract.
 * Side-effect-free: does not throw; call from anywhere.
 *
 * TRUST BOUNDARY: `message` is set via `textContent` (no HTML parse — XSS-safe),
 * but callers must not pass untrusted or unbounded content. Very long strings can
 * cause screen-reader DoS. Validate at the application boundary before calling.
 */
export function announce(message: string, politeness: LiveRegionPoliteness): void {
  const region = getOrCreateRegion();
  const live: 'polite' | 'assertive' = politeness === 'polite' ? 'polite' : 'assertive';
  region.setAttribute('aria-live', live);
  // Clear then re-set forces screen reader re-announcement for identical strings.
  region.textContent = '';
  requestAnimationFrame(() => {
    region.textContent = message ?? '';
  });
}

/**
 * Optional React component that mounts the aria-live region within the
 * React tree lifecycle. Use when you want React to own the region element.
 * If your app calls `announce()` directly, this component is not required.
 */
export function LiveAnnouncerProvider({ children }: { children?: React.ReactNode }): React.ReactElement {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (ref.current) _regionRef.current = ref.current;
    return () => { _regionRef.current = null; };
  }, []);

  return (
    <>
      <div
        ref={ref}
        aria-atomic="true"
        aria-live="polite"
        style={{
          position: 'absolute',
          width: '1px',
          height: '1px',
          overflow: 'hidden',
          clip: 'rect(0,0,0,0)',
          whiteSpace: 'nowrap',
          border: 0,
        }}
      />
      {children}
    </>
  );
}
