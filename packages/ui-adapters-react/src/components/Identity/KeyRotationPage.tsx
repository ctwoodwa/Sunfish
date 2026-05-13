import { useEffect, useId, useState } from 'react';
import type { KeyRotationResponse, PendingDiffPreview } from '../../contracts/IdentityTypes';

export interface KeyRotationPageProps {
  /** Base URL prefix for Bridge API calls. Defaults to `''` (same-origin). */
  apiBaseUrl?: string;
  /**
   * Pending Standing Order diff — cascaded from the Helm widget when a key-rotation
   * mutation is awaiting confirmation (ADR 0077 §4 + ADR 0066 §Phase 4).
   */
  pendingDiff?: PendingDiffPreview | null;
}

/**
 * React adapter parity for the Bridge Key Management page (ADR 0066 §2.2, W#58 Phase 3).
 * Fetches GET /api/v1/identity/keys and renders the active key fingerprint,
 * rotation status, and a link to the historical-keys page.
 *
 * Mirrors `accelerators/bridge/Sunfish.Bridge.Client/Pages/Identity/KeyRotationPage.razor`.
 */
export function KeyRotationPage({ apiBaseUrl = '', pendingDiff }: KeyRotationPageProps) {
  const [data, setData] = useState<KeyRotationResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headingId = useId();
  const keySectionId = useId();
  const historyLinkSectionId = useId();
  const pendingOrderHeadingId = useId();

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
    <main aria-labelledby={headingId}>
      <h1 id={headingId}>Key Management</h1>

      {/* M4: alert container always present so AT observes it before content is injected */}
      <div role="alert" aria-atomic="true">
        {error !== null && (
          <p><strong>Failed to load key information.</strong> {error}</p>
        )}
      </div>

      {error === null && data === null && (
        <p role="status">Loading key information…</p>
      )}

      {data !== null && (
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

      {/* Diff-preview confirmation surface — ADR 0077 §4 + ADR 0066 §Phase 4. */}
      {pendingDiff != null && pendingDiff.entries.length > 0 && (
        <section aria-labelledby={pendingOrderHeadingId}>
          <h2 id={pendingOrderHeadingId}>
            Pending change — {pendingDiff.summary || 'pending fields below'}
          </h2>
          <table aria-describedby={pendingOrderHeadingId}>
            <caption className="sr-only">
              Pending key rotation field changes ({pendingDiff.entries.length})
            </caption>
            <thead>
              <tr>
                <th scope="col">Field</th>
                <th scope="col">Previous</th>
                <th scope="col">New value</th>
              </tr>
            </thead>
            <tbody>
              {pendingDiff.entries.map((entry, idx) => (
                <tr key={`${entry.field}-${idx}`}>
                  <th scope="row">{entry.field}</th>
                  <td className="sf-diff-old">{entry.oldValue ?? '—'}</td>
                  <td className="sf-diff-new">
                    <span aria-hidden="true">{'→ '}</span>{entry.newValue ?? '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </main>
  );
}
