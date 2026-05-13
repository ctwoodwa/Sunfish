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
