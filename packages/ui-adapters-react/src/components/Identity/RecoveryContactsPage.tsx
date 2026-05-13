import { useEffect, useId, useState } from 'react';
import type { RecoveryContactsResponse } from '../../contracts/IdentityTypes';

export interface RecoveryContactsPageProps {
  /** Base URL prefix for Bridge API calls. Defaults to `''` (same-origin). */
  apiBaseUrl?: string;
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
export function RecoveryContactsPage({ apiBaseUrl = '' }: RecoveryContactsPageProps) {
  const [data, setData] = useState<RecoveryContactsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headingId = useId();
  const sectionHeadingId = useId();

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
  const badgeLabel = `${enrolledCount} of ${maxCount} contacts enrolled`;

  return (
    <main role="main" aria-labelledby={headingId}>
      <h1 id={headingId}>Recovery Contacts</h1>

      {error !== null ? (
        <p role="alert">{error}</p>
      ) : data === null ? (
        <p role="status" aria-live="polite">
          Loading recovery contacts…
        </p>
      ) : (
        <section aria-labelledby={sectionHeadingId}>
          <h2 id={sectionHeadingId}>
            Enrolled contacts{' '}
            <span className="sf-badge" aria-label={badgeLabel}>
              {enrolledCount} / {maxCount}
            </span>
          </h2>

          {data.contacts.length === 0 ? (
            <p>No recovery contacts enrolled. Add contacts to enable account recovery.</p>
          ) : (
            <ul aria-label="Recovery contacts list" aria-live="polite">
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
    </main>
  );
}
