/**
 * Tenant, lease, and communication domain contracts.
 * Maps to ERPNext Customer (tenant), Lease, and Sales Invoice doctypes
 * plus the crew-comms message substrate (W#45/W#59).
 *
 * Naming series: LEASE-.YYYY.-.####
 */

export interface Tenant {
  name: string
  tenantName: string
  email?: string
  phone?: string
  company?: string
}

export interface Lease {
  name: string
  tenant: string
  property: string
  unit?: string
  startDate: string
  endDate: string
  monthlyRent: number
  rentDueDay?: number
  securityDeposit?: number
  status: 'Active' | 'Expired' | 'Terminated' | 'Pending'
  company: string
}

/** A single rent or fee payment record. */
export interface PaymentRecord {
  name: string
  lease: string
  amount: number
  date: string
  paymentMethod: 'Cash' | 'Check' | 'ACH' | 'Card'
  status: 'Pending' | 'Completed'
}

/**
 * Crew-comms message thread (W#45/W#59 substrate).
 * Used by the crew-comms React and Blazor screens.
 */
export interface MessageThread {
  threadId: string
  subject: string
  propertyId?: string
  leaseId?: string
  participants: string[]
  lastMessageAt?: string
}
