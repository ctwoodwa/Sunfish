import { useEffect, useId, useState } from 'react';
import type { KeyRotationResponse } from '../../contracts/IdentityTypes';

export interface KeyRotationPageProps {
  /** Base URL prefix for Bridge API calls. Defaults to `''` (same-origin). */
  apiBaseUrl?: string;
}

/**
 * React adapter parity for the Bridge Key Management page (ADR 0066 §2.2, W#58 Phase 3).
 * Fetches GET /api/v1/identity/keys and renders the active key fingerprint,
 * rotation status, and a link to the historical-keys page.
 *
 * Mirrors `accelerators/bridge/Sunfish.Bridge.Client/Pages/Identity/KeyRotationPage.razor`.
 */
export function KeyRotationPage({ apiBaseUrl = '' }: KeyRotationPageProps) {
  const [data, setData] = useState<KeyRotationResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headingId = useId();
  const keySectionId = useId();
  const historyLinkSectionId = useId();

  useEffect(() => {
    const controller = new AbortController();
    fetch(`${apiBaseUrl}/api/v1/identity/keys`, { signal: controller.signal })
      .then((r) => (r.ok ? r.json() : Promise.reject(`HTTP ${r.status}`)))
      .then((json) => setData(json as KeyRotationResponse))
      .catch((e: unknown) => {
        if (e instanceof DOMException && e.name === 'AbortError') return;
        setError(String(e));
      });
    return () => controller.abort();
  }, [apiBaseUrl]);

  return (
    <main role="main" aria-labelledby={headingId}>
      <h1 id={headingId}>Key Management</h1>

      {error !== null ? (
        <p role="alert">{error}</p>
      ) : data === null ? (
        <p role="status" aria-live="polite">
          Loading key information…
        </p>
      ) : (
        <>
          <section aria-labelledby={keySectionId}>
            <h2 id={keySectionId}>Current signing key</h2>
            <dl>
              <dt>Fingerprint</dt>
              <dd>{data.currentFingerprint ?? 'No key registered'}</dd>
              <dt>Historical keys</dt>
              <dd>{data.historicalKeyCount} rotation(s)</dd>
              <dt>Rotation in progress</dt>
              <dd>{data.rotationInProgress ? 'Yes' : 'No'}</dd>
              {data.rotationWindowExpiry !== null && (
                <>
                  <dt>Rotation window expires</dt>
                  <dd>{new Date(data.rotationWindowExpiry).toLocaleString()}</dd>
                </>
              )}
            </dl>
          </section>

          <section aria-labelledby={historyLinkSectionId}>
            <h2 id={historyLinkSectionId}>Key history</h2>
            <p>
              <a href="/identity/keys/history">View key history</a>
            </p>
          </section>
        </>
      )}
    </main>
  );
}
