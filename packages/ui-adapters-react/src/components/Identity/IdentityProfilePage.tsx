import { useEffect, useId, useState } from 'react';
import type { IdentityProfileResponse, PendingDiffPreview } from '../../contracts/IdentityTypes';

export interface IdentityProfilePageProps {
  /** Base URL prefix for Bridge API calls. Defaults to `''` (same-origin). */
  apiBaseUrl?: string;
  /**
   * Pending Standing Order diff — cascaded from the Helm widget when a profile mutation
   * is awaiting confirmation (ADR 0077 §4 + ADR 0066 §Phase 4).
   * Renders a DiffPreviewView.Expanded confirmation table when provided.
   */
  pendingDiff?: PendingDiffPreview | null;
}

/**
 * React adapter parity for the Bridge Identity Profile page (ADR 0066 §2.1, W#58 Phase 3).
 * Fetches GET /api/v1/identity/profile and renders display-name, contact email,
 * and optional phone number.
 *
 * Mirrors `accelerators/bridge/Sunfish.Bridge.Client/Pages/Identity/IdentityProfileEditPage.razor`.
 */
export function IdentityProfilePage({ apiBaseUrl = '', pendingDiff }: IdentityProfilePageProps) {
  const [data, setData] = useState<IdentityProfileResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headingId = useId();
  const sectionHeadingId = useId();
  const pendingOrderHeadingId = useId();

  useEffect(() => {
    const controller = new AbortController();
    fetch(`${apiBaseUrl}/api/v1/identity/profile`, { signal: controller.signal })
      .then((r) => (r.ok ? r.json() : Promise.reject(`HTTP ${r.status}`)))
      .then((json) => setData(json as IdentityProfileResponse))
      .catch((e: unknown) => {
        if (e instanceof DOMException && e.name === 'AbortError') return;
        setError(String(e));
      });
    return () => controller.abort();
  }, [apiBaseUrl]);

  return (
    <main aria-labelledby={headingId}>
      <h1 id={headingId}>Identity Profile</h1>

      {/* M4: alert container always present so AT observes it before content is injected */}
      <div role="alert" aria-atomic="true">
        {error !== null && (
          <p><strong>Failed to load profile.</strong> {error}</p>
        )}
      </div>

      {error === null && data === null && (
        <p role="status">Loading profile…</p>
      )}

      {data !== null && (
        <section aria-labelledby={sectionHeadingId}>
          <h2 id={sectionHeadingId}>Profile details</h2>
          <dl>
            <dt>Display name</dt>
            <dd>{data.displayName || 'Not set'}</dd>
            <dt>Contact email</dt>
            <dd>{data.contactEmail || 'Not set'}</dd>
            <dt>Phone number</dt>
            <dd>{data.phoneNumber ?? 'Not set'}</dd>
          </dl>
        </section>
      )}

      {/* Diff-preview confirmation surface — ADR 0077 §4 + ADR 0066 §Phase 4.
          Rendered when the Helm widget passes a pending Standing Order preview.
          DiffPreviewView.Expanded: tabular field-by-field change list (SC 1.3.1). */}
      {pendingDiff != null && pendingDiff.entries.length > 0 && (
        <section aria-labelledby={pendingOrderHeadingId}>
          <h2 id={pendingOrderHeadingId}>
            Pending change — {pendingDiff.summary || 'pending fields below'}
          </h2>
          <table aria-describedby={pendingOrderHeadingId}>
            <caption className="sr-only">
              Pending profile field changes ({pendingDiff.entries.length})
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
