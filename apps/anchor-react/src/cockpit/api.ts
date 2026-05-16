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

// ── Property detail ────────────────────────────────────────────────────

export interface CockpitEquipmentSummary {
  equipmentId: string
  displayName: string
  class: string
  make: string | null
  model: string | null
  installedAt: string | null
  locationInProperty: string | null
}

export interface CockpitLeaseSummary {
  leaseId: string
  tenantDisplayName: string
  monthlyRent: number
  endDate: string
}

export interface CockpitPropertyDetail {
  propertyId: string
  displayAddress: string
  kind: string
  equipment: CockpitEquipmentSummary[]
  // The following four fields are stubbed server-side per W#29 PR 2 + XO
  // ruling on 2026-05-16: lease / WO / inspection aggregation requires
  // the W#62 PropertyUnit substrate. The View renders empty placeholders
  // and a "coming soon" note for now.
  activeLease: CockpitLeaseSummary | null
  openWorkOrderCount: number
  lastInspectionDate: string | null
  lastInspectionResult: string | null
}

/** Returns the property detail (card + equipment + stubbed aggregation). */
export async function getCockpitPropertyDetail(propertyId: string): Promise<CockpitPropertyDetail> {
  const resp = await fetch(
    `/api/v1/cockpit/${encodeURIComponent(propertyId)}/detail`,
    { credentials: 'include' },
  )
  if (resp.status === 404) {
    throw new Error('Property not found')
  }
  if (!resp.ok) {
    throw new Error(`Failed to load property detail: ${resp.status} ${resp.statusText}`)
  }
  return (await resp.json()) as CockpitPropertyDetail
}
