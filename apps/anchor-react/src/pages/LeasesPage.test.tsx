import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { LeasesPage } from './LeasesPage'
import * as useLeaseHook from '@/hooks/useLeases'
import type { Lease } from '@/api/erpnext'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return (
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={['/leases']}>
        <Routes>
          <Route path="/leases" element={children} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  )
}

const MOCK_LEASES: Lease[] = [
  {
    name: 'LEASE-0001',
    tenant: 'Jane Smith',
    property: '150 Lexington Ct',
    unit: 'Unit 1',
    start_date: '2025-01-01',
    end_date: '2025-12-31',
    monthly_rent: 1500,
    status: 'Active',
    company: 'Royal Key Management LLC',
  },
  {
    name: 'LEASE-0002',
    tenant: 'Bob Jones',
    property: '200 Oak Ave',
    unit: '',
    start_date: '2024-06-01',
    end_date: '2024-12-31',
    monthly_rent: 2000,
    status: 'Expired',
    company: 'Royal Key Management LLC',
  },
]

describe('LeasesPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('shows loading state while pending', () => {
    vi.spyOn(useLeaseHook, 'useLeases').mockReturnValue({
      data: undefined,
      isPending: true,
      isError: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useLeaseHook.useLeases>)

    render(<LeasesPage />, { wrapper })
    expect(screen.getByText(/loading leases/i)).toBeInTheDocument()
  })

  it('renders lease rows for each lease', () => {
    vi.spyOn(useLeaseHook, 'useLeases').mockReturnValue({
      data: MOCK_LEASES,
      isPending: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useLeaseHook.useLeases>)

    render(<LeasesPage />, { wrapper })
    expect(screen.getByText('Jane Smith')).toBeInTheDocument()
    expect(screen.getByText('Bob Jones')).toBeInTheDocument()
    expect(screen.getByText('$1,500')).toBeInTheDocument()
    expect(screen.getByText('$2,000')).toBeInTheDocument()
  })

  it('shows error state when fetch fails', () => {
    vi.spyOn(useLeaseHook, 'useLeases').mockReturnValue({
      data: undefined,
      isPending: false,
      isError: true,
      error: new Error('Network error'),
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useLeaseHook.useLeases>)

    render(<LeasesPage />, { wrapper })
    expect(screen.getByText(/failed to load leases/i)).toBeInTheDocument()
    expect(screen.getByText('Network error')).toBeInTheDocument()
  })

  it('shows empty state when no leases', () => {
    vi.spyOn(useLeaseHook, 'useLeases').mockReturnValue({
      data: [],
      isPending: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useLeaseHook.useLeases>)

    render(<LeasesPage />, { wrapper })
    expect(screen.getByText(/no leases found/i)).toBeInTheDocument()
  })
})
