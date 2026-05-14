/**
 * Property management domain contracts.
 * Mirrors the ERPNext `Property` and `Unit` custom doctypes created in
 * W#60 Phase 1 (see addendum: icm/_state/handoffs/w60-erpnext-react-ui-phase2-stage06-addendum.md).
 *
 * Naming series: PROP-.####  (ERPNext auto-assigned docname)
 */

export type OccupancyStatus = 'Active' | 'Vacant' | 'Maintenance' | 'Sold'

export type RentStatus = 'Current' | 'Overdue' | 'Vacant'

/** An individual rentable unit within a Property (for multi-unit properties). */
export interface Unit {
  designation: string
  monthlyRent: number
  occupancyStatus: OccupancyStatus
  rentStatus: RentStatus
  tenantName?: string
  leaseEnd?: string
}

/**
 * Core property record as returned by the Bridge `/api/v1/properties` endpoint.
 * Field names are camelCase (ASP.NET Core default serialiser).
 */
export interface Property {
  name: string
  propertyName: string
  addressLine1: string
  addressLine2?: string
  city: string
  state: string
  postalCode: string
  units: number
  status: OccupancyStatus
  company: string
  acquisitionDate?: string
  notes?: string
}

/** Rent-roll row — one row per property × unit combination. */
export interface RentRollRow {
  propertyId: string
  propertyName: string
  unit?: string
  tenantName?: string
  leaseStart?: string
  leaseEnd?: string
  monthlyRent: number
  lastPaymentDate?: string
  balanceDue: number
  status: RentStatus
}
