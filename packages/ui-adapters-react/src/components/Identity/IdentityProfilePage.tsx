import { useEffect, useId, useState } from 'react';
import type { IdentityProfileResponse } from '../../contracts/IdentityTypes';

export interface IdentityProfilePageProps {
  /** Base URL prefix for Bridge API calls. Defaults to `''` (same-origin). */
  apiBaseUrl?: string;
}

/**
 * React adapter parity for the Bridge Identity Profile page (ADR 0066 §2.1, W#58 Phase 3).
 * Fetches GET /api/v1/identity/profile and renders display-name, contact email,
 * and optional phone number.
 *
 * Mirrors `accelerators/bridge/Sunfish.Bridge.Client/Pages/Identity/IdentityProfileEditPage.razor`.
 */
export function IdentityProfilePage({ apiBaseUrl = '' }: IdentityProfilePageProps) {
  const [data, setData] = useState<IdentityProfileResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headingId = useId();
  const sectionHeadingId = useId();

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
    <main role="main" aria-labelledby={headingId}>
      <h1 id={headingId}>Identity Profile</h1>

      {error !== null ? (
        <p role="alert">{error}</p>
      ) : data === null ? (
        <p role="status" aria-live="polite">
          Loading profile…
        </p>
      ) : (
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
    </main>
  );
}
