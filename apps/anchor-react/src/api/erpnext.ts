export interface Property {
  name: string
  property_name: string
  address_line_1: string
  city: string
  state: string
  postal_code: string
  units: number
  status: 'Active' | 'Vacant' | 'Maintenance' | 'Sold'
  company: string
}

interface ERPNextListResponse<T> {
  data: T[]
}

async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const resp = await fetch(path, {
    credentials: 'include',
    ...init,
  })
  if (!resp.ok) {
    const text = await resp.text().catch(() => resp.statusText)
    throw new Error(`ERPNext API error ${resp.status}: ${text}`)
  }
  return resp.json() as Promise<T>
}

export async function getProperties(): Promise<Property[]> {
  const result = await apiFetch<ERPNextListResponse<Property>>('/api/v1/erpnext/properties')
  return result.data
}

// ── Phase 3: Leases + Payments ──────────────────────────────────────────────

export interface Lease {
  name: string
  tenant: string
  property: string
  unit: string
  start_date: string
  end_date: string
  monthly_rent: number
  status: 'Active' | 'Expired' | 'Terminated'
  company: string
}

export interface Payment {
  name: string
  lease: string
  amount: number
  date: string
  payment_method: 'Cash' | 'Check' | 'ACH' | 'Card'
  status: 'Pending' | 'Completed'
}

export interface RecordPaymentInput {
  Lease: string
  Amount: number
  Date: string
  PaymentMethod: string
}

export async function getLeases(): Promise<Lease[]> {
  const result = await apiFetch<ERPNextListResponse<Lease>>('/api/v1/erpnext/leases')
  return result.data
}

export async function getLease(name: string): Promise<Lease> {
  const result = await apiFetch<{ data: Lease }>(`/api/v1/erpnext/leases/${encodeURIComponent(name)}`)
  return result.data
}

export async function getPayments(): Promise<Payment[]> {
  const result = await apiFetch<ERPNextListResponse<Payment>>('/api/v1/erpnext/payments')
  return result.data
}

export async function recordPayment(payload: RecordPaymentInput): Promise<Payment> {
  const result = await apiFetch<{ data: Payment }>('/api/v1/erpnext/payments', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  return result.data
}

// ── Phase 4: Accounting ──────────────────────────────────────────────────────

export interface AccountingSummary {
  period: string
  income: number
  expenses: number
  net: number
}

export interface OutstandingInvoice {
  name: string
  customer: string
  outstanding_amount: number
  due_date: string
  status: string
}

interface OutstandingListResponse {
  data: OutstandingInvoice[]
}

export async function getAccountingSummary(): Promise<AccountingSummary> {
  return apiFetch<AccountingSummary>('/api/v1/erpnext/accounting/summary')
}

export async function getAccountingOutstanding(): Promise<OutstandingInvoice[]> {
  const result = await apiFetch<OutstandingListResponse>('/api/v1/erpnext/accounting/outstanding')
  return result.data.filter((inv) => inv.outstanding_amount > 0)
}

// ── Phase 5: Maintenance ─────────────────────────────────────────────────────

export interface MaintenanceTicket {
  name: string
  subject: string
  property: string
  status: 'Open' | 'In Progress' | 'Resolved' | 'Closed'
  priority: 'Low' | 'Medium' | 'High' | 'Critical'
  assigned_to: string | null
  cost: number | null
}

export interface CreateMaintenanceInput {
  Subject: string
  Property: string
  Priority: string
  AssignedTo?: string
  Description?: string
}

export interface UpdateMaintenanceInput {
  Status?: string
  AssignedTo?: string
  Cost?: number
  Resolution?: string
}

export async function getMaintenanceTickets(): Promise<MaintenanceTicket[]> {
  const result = await apiFetch<{ data: MaintenanceTicket[] }>('/api/v1/erpnext/maintenance')
  return result.data
}

export async function createMaintenanceTicket(payload: CreateMaintenanceInput): Promise<{ data: MaintenanceTicket }> {
  return apiFetch<{ data: MaintenanceTicket }>('/api/v1/erpnext/maintenance', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
}

export async function updateMaintenanceTicket(name: string, payload: UpdateMaintenanceInput): Promise<{ data: MaintenanceTicket }> {
  return apiFetch<{ data: MaintenanceTicket }>(`/api/v1/erpnext/maintenance/${encodeURIComponent(name)}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
}
