// W#74 PR 1 — Properties API client for the Bridge /api/v1/properties
// cluster endpoint. Replaces the ERPNext-backed getProperties() fetcher
// in src/api/erpnext.ts. Per hand-off §3.2:
//   - relative URL (Tauri shell + dev server both map /api/v1/* to the Bridge)
//   - credentials: 'include' so the Bridge auth cookie flows
//   - throw on non-2xx so TanStack Query surfaces the error
//
// EntityTag is a server-side echo for read-only display; callers MUST NOT
// pass an ?entityTag= query parameter — server is the source of truth.

export interface PropertySummary {
  propertyId: string
  displayName: string
  kind: string
  addressLine1: string | null
  city: string
  region: string
  unitCount: number
  status: 'Active' | 'Vacant' | 'Maintenance' | 'Sold'
  entityTag: string | null
}

export interface PropertyList {
  properties: PropertySummary[]
}

export async function getProperties(): Promise<PropertyList> {
  const resp = await fetch('/api/v1/properties', { credentials: 'include' })
  if (!resp.ok) {
    throw new Error(`Failed to load properties: ${resp.status} ${resp.statusText}`)
  }
  return (await resp.json()) as PropertyList
}
