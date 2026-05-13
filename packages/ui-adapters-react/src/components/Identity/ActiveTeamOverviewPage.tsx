import { useEffect, useId, useState } from 'react';
import type { ActiveTeamOverviewResponse } from '../../contracts/IdentityTypes';

export interface ActiveTeamOverviewPageProps {
  /** Base URL prefix for Bridge API calls. Defaults to `''` (same-origin). */
  apiBaseUrl?: string;
}

/**
 * React adapter parity for the Bridge Team Memberships page (ADR 0066 §2.5–2.6, W#58 Phase 3).
 * Fetches GET /api/v1/identity/teams and renders all team memberships.
 *
 * Bridge posture: `activeTeamId` is always null (no `IActiveTeamAccessor` on the Bridge);
 * all memberships render without the "active" badge. This matches the Bridge Blazor posture.
 *
 * `TeamId` uses `Guid` on the C# side (cycle-break per W#53 P1a); the wire form is a
 * lowercase-hyphenated UUID string which is used as the React list key.
 *
 * Mirrors `accelerators/bridge/Sunfish.Bridge.Client/Pages/Identity/ActiveTeamOverviewPage.razor`.
 */
export function ActiveTeamOverviewPage({ apiBaseUrl = '' }: ActiveTeamOverviewPageProps) {
  const [data, setData] = useState<ActiveTeamOverviewResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headingId = useId();
  const sectionHeadingId = useId();

  useEffect(() => {
    const controller = new AbortController();
    fetch(`${apiBaseUrl}/api/v1/identity/teams`, { signal: controller.signal })
      .then((r) => (r.ok ? r.json() : Promise.reject(`HTTP ${r.status}`)))
      .then((json) => setData(json as ActiveTeamOverviewResponse))
      .catch((e: unknown) => {
        if (e instanceof DOMException && e.name === 'AbortError') return;
        setError(String(e));
      });
    return () => controller.abort();
  }, [apiBaseUrl]);

  const teamCount = data?.teams.length ?? 0;
  const teamWord = teamCount === 1 ? 'team' : 'teams';

  return (
    <main aria-labelledby={headingId}>
      <h1 id={headingId}>Team Memberships</h1>

      {/* M4: alert container always present so AT observes it before content is injected */}
      <div role="alert" aria-atomic="true">
        {error !== null && (
          <p><strong>Failed to load team memberships.</strong> {error}</p>
        )}
      </div>

      {error === null && data === null && (
        <p role="status">Loading team memberships…</p>
      )}

      {data !== null && data.teams.length === 0 && (
        <p>No team memberships found.</p>
      )}

      {data !== null && data.teams.length > 0 && (
        <section aria-labelledby={sectionHeadingId}>
          <h2 id={sectionHeadingId}>
            Your teams{' '}
            <span className="sf-badge" aria-label={`${teamCount} ${teamWord}`}>
              {teamCount}
            </span>
          </h2>
          {/* M9: aria-label matches visible heading text "Your teams" */}
          <ul aria-label="Your teams">
            {data.teams.map((membership) => {
              const isActive =
                data.activeTeamId !== null && data.activeTeamId === membership.teamId;
              const fpText = membership.subkeyFingerprint;
              const fpShort = fpText.length > 8 ? fpText.slice(0, 8) + '…' : fpText;
              const liClass = isActive ? 'sf-team-entry sf-team-entry--active' : 'sf-team-entry';

              return (
                <li key={membership.teamId} className={liClass}>
                  {/* M6: no aria-label on parent span; sr-only conveys active status */}
                  <span className="sf-team-name">
                    {membership.displayName}
                    {isActive && (
                      <>
                        <span className="sf-badge sf-badge--active" aria-hidden="true">
                          Active
                        </span>
                        <span className="sr-only"> (active team)</span>
                      </>
                    )}
                  </span>
                  <span className="sf-team-role">{membership.roleDisplayName}</span>
                  {/* M7: sr-only carries full fingerprint; aria-hidden shields truncated visible text */}
                  <span className="sf-team-fingerprint">
                    <span aria-hidden="true">{fpShort}</span>
                    <span className="sr-only">Subkey fingerprint: {fpText}</span>
                  </span>
                </li>
              );
            })}
          </ul>
        </section>
      )}
    </main>
  );
}
