import { useEffect, useId, useState } from 'react';
import type { HistoricalKeysResponse } from '../../contracts/IdentityTypes';

export interface HistoricalKeysPageProps {
  /** Base URL prefix for Bridge API calls. Defaults to `''` (same-origin). */
  apiBaseUrl?: string;
}

/**
 * React adapter parity for the Bridge Historical Keys browse page (ADR 0066 §2.4, W#58 Phase 3).
 * Fetches GET /api/v1/identity/keys/history and renders retired keys in reverse-chronological
 * order with fingerprint, activation/retirement dates, rotation reason, and signature count.
 *
 * Phase 1b types `rotationReason` as a free-form string; ADR 0046-A1 (not yet on origin/main)
 * will introduce a typed `KeyRotationReason` enum in a Phase 2 follow-up.
 *
 * Mirrors `accelerators/bridge/Sunfish.Bridge.Client/Pages/Identity/HistoricalKeysPage.razor`.
 */
export function HistoricalKeysPage({ apiBaseUrl = '' }: HistoricalKeysPageProps) {
  const [data, setData] = useState<HistoricalKeysResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headingId = useId();
  const sectionHeadingId = useId();

  useEffect(() => {
    const controller = new AbortController();
    fetch(`${apiBaseUrl}/api/v1/identity/keys/history`, { signal: controller.signal })
      .then((r) => (r.ok ? r.json() : Promise.reject(`HTTP ${r.status}`)))
      .then((json) => setData(json as HistoricalKeysResponse))
      .catch((e: unknown) => {
        if (e instanceof DOMException && e.name === 'AbortError') return;
        setError(String(e));
      });
    return () => controller.abort();
  }, [apiBaseUrl]);

  return (
    <main aria-labelledby={headingId}>
      <h1 id={headingId}>Key History</h1>

      {/* M4: alert container always present so AT observes it before content is injected */}
      <div role="alert" aria-atomic="true">
        {error !== null && (
          <p><strong>Failed to load key history.</strong> {error}</p>
        )}
      </div>

      {error === null && data === null && (
        <p role="status">Loading key history…</p>
      )}

      {data !== null && data.keys.length === 0 && (
        <p>No retired keys on record.</p>
      )}

      {data !== null && data.keys.length > 0 && (
        <section aria-labelledby={sectionHeadingId}>
          <h2 id={sectionHeadingId}>Retired keys</h2>
          {/* M9: aria-label matches visible heading text */}
          <ul aria-label="Retired keys">
            {data.keys.map((entry) => {
              const fpShort =
                entry.fingerprint.length > 8
                  ? entry.fingerprint.slice(0, 8) + '…'
                  : entry.fingerprint;
              return (
                <li key={entry.fingerprint} className="sf-key-entry">
                  <dl>
                    <dt>Fingerprint</dt>
                    {/* M7: sr-only carries full fingerprint; aria-hidden shields truncated visible text */}
                    <dd>
                      <span aria-hidden="true">{fpShort}</span>
                      <span className="sr-only">Fingerprint {entry.fingerprint}</span>
                    </dd>
                    <dt>Activated</dt>
                    <dd>{new Date(entry.activatedAt).toLocaleString()}</dd>
                    {entry.retiredAt !== null && (
                      <>
                        <dt>Retired</dt>
                        <dd>{new Date(entry.retiredAt).toLocaleString()}</dd>
                      </>
                    )}
                    <dt>Rotation reason</dt>
                    <dd>{entry.rotationReason}</dd>
                    <dt>Signatures still verifiable</dt>
                    <dd>{entry.signatureSurvivalCount}</dd>
                  </dl>
                </li>
              );
            })}
          </ul>
        </section>
      )}
    </main>
  );
}
