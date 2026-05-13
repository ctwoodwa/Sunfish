import { useEffect, useId, useState } from 'react';
import type { RecoveryContactsResponse, PendingDiffPreview } from '../../contracts/IdentityTypes';

export interface RecoveryContactsPageProps {
  /** Base URL prefix for Bridge API calls. Defaults to `''` (same-origin). */
  apiBaseUrl?: string;
  /**
   * Pending Standing Order diff — cascaded from the Helm widget when a recovery-contact
   * mutation is awaiting confirmation (ADR 0077 §4 + ADR 0066 §Phase 4).
   */
  pendingDiff?: PendingDiffPreview | null;
}

/**
 * React adapter parity for the Bridge Recovery Contacts page (ADR 0066 §2.3, W#58 Phase 3).
 * Fetches GET /api/v1/identity/recovery and renders currently-enrolled recovery contacts
 * with verification status and enrollment date.
 *
 * UX vocabulary uses "Recovery Contact"; audit / persistence uses "Trustee" per ADR 0046
 * (OQ-1 council decision). This component uses the UX vocabulary exclusively.
 *
 * Mirrors `accelerators/bridge/Sunfish.Bridge.Client/Pages/Identity/RecoveryContactsPage.razor`.
 */
export function RecoveryContactsPage({ apiBaseUrl = '', pendingDiff }: RecoveryContactsPageProps) {
  const [data, setData] = useState<RecoveryContactsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headingId = useId();
  const sectionHeadingId = useId();
  const pendingOrderHeadingId = useId();

  useEffect(() => {
    const controller = new AbortController();
    fetch(`${apiBaseUrl}/api/v1/identity/recovery`, { signal: controller.signal })
      .then((r) => (r.ok ? r.json() : Promise.reject(`HTTP ${r.status}`)))
      .then((json) => setData(json as RecoveryContactsResponse))
      .catch((e: unknown) => {
        if (e instanceof DOMException && e.name === 'AbortError') return;
        setError(String(e));
      });
    return () => controller.abort();
  }, [apiBaseUrl]);

  const enrolledCount = data?.contacts.length ?? 0;
  const maxCount = data?.maxContacts ?? 0;

  return (
    <main aria-labelledby={headingId}>
      <h1 id={headingId}>Recovery Contacts</h1>

      {/* M4: alert container always present so AT observes it before content is injected */}
      <div role="alert" aria-atomic="true">
        {error !== null && (
          <p><strong>Failed to load recovery contacts.</strong> {error}</p>
        )}
      </div>

      {error === null && data === null && (
        <p role="status">Loading recovery contacts…</p>
      )}

      {data !== null && (
        <section aria-labelledby={sectionHeadingId}>
          {/* M5: badge uses aria-hidden + sr-only to avoid conflicting accessible names */}
          <h2 id={sectionHeadingId}>
            Enrolled contacts
            <span className="sf-badge" aria-hidden="true">
              {enrolledCount} / {maxCount}
            </span>
            <span className="sr-only"> — {enrolledCount} of {maxCount} contacts enrolled</span>
          </h2>

          {data.contacts.length === 0 ? (
            <p>No recovery contacts enrolled. Add contacts to enable account recovery.</p>
          ) : (
            /* M8: no aria-live on the list (read-only page; live region is role="status" above) */
            /* M9: aria-label matches visible heading text */
            <ul aria-label="Enrolled contacts">
              {data.contacts.map((contact) => (
                <li key={contact.contactActorId}>
                  <span className="sf-contact-name">{contact.displayName}</span>
                  <span
                    className="sf-contact-status"
                    aria-label={`Verification status: ${contact.verificationStatus}`}
                  >
                    {contact.verificationStatus}
                  </span>
                  <span className="sf-contact-date">
                    Enrolled {new Date(contact.enrolledAt).toLocaleDateString()}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      {/* Diff-preview confirmation surface — ADR 0077 §4 + ADR 0066 §Phase 4. */}
      {pendingDiff != null && pendingDiff.entries.length > 0 && (
        <section aria-labelledby={pendingOrderHeadingId}>
          <h2 id={pendingOrderHeadingId}>
            Pending change — {pendingDiff.summary || 'pending fields below'}
          </h2>
          <table aria-describedby={pendingOrderHeadingId}>
            <caption className="sr-only">
              Pending recovery-contact changes ({pendingDiff.entries.length})
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
