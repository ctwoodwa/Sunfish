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

// ── Work orders (PR 3) ────────────────────────────────────────────────

export interface CockpitWorkOrderSummary {
  workOrderId: string
  status: string
  vendorId: string
  scheduledDate: string
  completedDate: string | null
  appointmentDate: string | null
}

export interface CockpitWorkOrderList {
  items: CockpitWorkOrderSummary[]
  total: number
  page: number
  pageSize: number
}

export interface CockpitEntryNotice {
  plannedEntryUtc: string
  entryReason: string
}

export interface CockpitAppointment {
  slotStartUtc: string
  slotEndUtc: string
  status: string
}

export interface CockpitCompletionAttestation {
  attestedAt: string
  signatureRef: string
}

export interface CockpitWorkOrderDetail {
  workOrderId: string
  status: string
  scheduledDate: string
  completedDate: string | null
  vendorId: string
  vendorDisplayName: string
  notes: string | null
  entryNotices: CockpitEntryNotice[]
  appointment: CockpitAppointment | null
  completionAttestation: CockpitCompletionAttestation | null
  auditTrail: string[]
}

export interface ListWorkOrdersParams {
  status?: string
  vendorId?: string
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

export async function getCockpitWorkOrders(params: ListWorkOrdersParams = {}): Promise<CockpitWorkOrderList> {
  const qs = new URLSearchParams()
  if (params.status) qs.set('status', params.status)
  if (params.vendorId) qs.set('vendorId', params.vendorId)
  if (params.from) qs.set('from', params.from)
  if (params.to) qs.set('to', params.to)
  if (params.page) qs.set('page', String(params.page))
  if (params.pageSize) qs.set('pageSize', String(params.pageSize))
  const url = `/api/v1/cockpit/work-orders${qs.size > 0 ? `?${qs}` : ''}`
  const resp = await fetch(url, { credentials: 'include' })
  if (!resp.ok) throw new Error(`Failed to load work orders: ${resp.status} ${resp.statusText}`)
  return (await resp.json()) as CockpitWorkOrderList
}

export async function getCockpitWorkOrderDetail(workOrderId: string): Promise<CockpitWorkOrderDetail> {
  const resp = await fetch(
    `/api/v1/cockpit/work-orders/${encodeURIComponent(workOrderId)}`,
    { credentials: 'include' },
  )
  if (resp.status === 404) throw new Error('Work order not found')
  if (!resp.ok) throw new Error(`Failed to load work order: ${resp.status} ${resp.statusText}`)
  return (await resp.json()) as CockpitWorkOrderDetail
}

// ── Vendors (PR 4) ────────────────────────────────────────────────────

export interface CockpitVendorSummary {
  vendorId: string
  displayName: string
  specialties: string[]
  onboardingState: string
  w9Status: string
  ytdPayments: number
  needsForm1099: boolean
}

export interface CockpitVendorList {
  vendors: CockpitVendorSummary[]
}

export interface CockpitVendorPerformanceEntry {
  event: string
  occurredAt: string
  notes: string | null
}

export interface CockpitVendorWorkOrderEntry {
  workOrderId: string
  status: string
  scheduledDate: string
  completedDate: string | null
  totalCost: number | null
}

export interface CockpitVendorDetail {
  vendorId: string
  displayName: string
  status: string
  onboardingState: string
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  specialties: string[]
  contactIds: string[]
  w9Status: string
  performanceLog: CockpitVendorPerformanceEntry[]
  workOrders: CockpitVendorWorkOrderEntry[]
}

export async function getCockpitVendors(): Promise<CockpitVendorList> {
  const resp = await fetch('/api/v1/cockpit/vendors', { credentials: 'include' })
  if (!resp.ok) throw new Error(`Failed to load vendors: ${resp.status} ${resp.statusText}`)
  return (await resp.json()) as CockpitVendorList
}

export async function getCockpitVendorDetail(vendorId: string): Promise<CockpitVendorDetail> {
  const resp = await fetch(
    `/api/v1/cockpit/vendors/${encodeURIComponent(vendorId)}`,
    { credentials: 'include' },
  )
  if (resp.status === 404) throw new Error('Vendor not found')
  if (!resp.ok) throw new Error(`Failed to load vendor: ${resp.status} ${resp.statusText}`)
  return (await resp.json()) as CockpitVendorDetail
}

// ── Dashboard (PR 5) ──────────────────────────────────────────────────

export interface CockpitRenewalBucket {
  withinDays: number
  count: number
}

export interface CockpitWorkOrderRollup {
  open: number
  inProgress: number
  blocked: number
}

export interface CockpitDashboard {
  totalUnits: number
  vacantUnits: number
  upcomingRenewals: CockpitRenewalBucket[]
  workOrders: CockpitWorkOrderRollup
  overdueInspectionUnitIds: string[]
}

export async function getCockpitDashboard(propertyId: string): Promise<CockpitDashboard> {
  const resp = await fetch(
    `/api/v1/cockpit/${encodeURIComponent(propertyId)}/dashboard`,
    { credentials: 'include' },
  )
  if (resp.status === 404) throw new Error('Property not found')
  if (!resp.ok) throw new Error(`Failed to load dashboard: ${resp.status} ${resp.statusText}`)
  return (await resp.json()) as CockpitDashboard
}
