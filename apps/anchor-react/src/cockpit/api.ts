/**
 * W#29 Phase 1 — Owner cockpit API client.
 *
 * Routes are guarded server-side by `CockpitPolicy` (authenticated +
 * role in {owner, spouse}). PR 1 ships only the property-selector endpoint;
 * PR 2–5 add detail / work-order / vendor / dashboard calls under the same
 * `/api/v1/cockpit` prefix.
 */

export interface CockpitPropertySummary {
  propertyId: string
  displayName: string
  kind: string
  city: string
  region: string
}

export interface CockpitPropertyList {
  properties: CockpitPropertySummary[]
}

/** Returns the property summary list for the authenticated tenant. */
export async function getCockpitProperties(): Promise<CockpitPropertyList> {
  const resp = await fetch('/api/v1/cockpit/properties', { credentials: 'include' })
  if (!resp.ok) {
    throw new Error(`Failed to load cockpit properties: ${resp.status} ${resp.statusText}`)
  }
  return (await resp.json()) as CockpitPropertyList
}
