import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { LeaseDetailPage } from './LeaseDetailPage'
import * as useLeaseHook from '@/hooks/useLeases'
import type { Lease, Payment } from '@/api/erpnext'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return (
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={['/leases/LEASE-0001']}>
        <Routes>
          <Route path="/leases/:name" element={children} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  )
}

const MOCK_LEASE: Lease = {
  name: 'LEASE-0001',
  tenant: 'Jane Smith',
  property: '150 Lexington Ct',
  unit: 'Unit 1',
  start_date: '2025-01-01',
  end_date: '2025-12-31',
  monthly_rent: 1500,
  status: 'Active',
  company: 'Royal Key Management LLC',
}

const MOCK_PAYMENTS: Payment[] = [
  { name: 'PAY-0001', lease: 'LEASE-0001', amount: 1200, date: '2025-03-01', payment_method: 'ACH', status: 'Completed' },
  { name: 'PAY-0002', lease: 'LEASE-0002', amount: 1800, date: '2025-03-01', payment_method: 'ACH', status: 'Completed' },
]

describe('LeaseDetailPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('shows loading state while lease is fetching', () => {
    vi.spyOn(useLeaseHook, 'useLease').mockReturnValue({
      data: undefined,
      isPending: true,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useLeaseHook.useLease>)

    vi.spyOn(useLeaseHook, 'usePayments').mockReturnValue({
      data: undefined,
      isPending: true,
    } as unknown as ReturnType<typeof useLeaseHook.usePayments>)

    render(<LeaseDetailPage />, { wrapper })

    expect(screen.getByText(/loading lease/i)).toBeInTheDocument()
  })

  it('shows error state with back link when fetch fails', () => {
    vi.spyOn(useLeaseHook, 'useLease').mockReturnValue({
      data: undefined,
      isPending: false,
      isError: true,
      error: new Error('Not found'),
    } as unknown as ReturnType<typeof useLeaseHook.useLease>)

    vi.spyOn(useLeaseHook, 'usePayments').mockReturnValue({
      data: undefined,
      isPending: false,
    } as unknown as ReturnType<typeof useLeaseHook.usePayments>)

    render(<LeaseDetailPage />, { wrapper })

    expect(screen.getByText(/failed to load lease/i)).toBeInTheDocument()
    expect(screen.getByText('Not found')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /back to leases/i })).toBeInTheDocument()
  })

  it('renders lease details when loaded', () => {
    vi.spyOn(useLeaseHook, 'useLease').mockReturnValue({
      data: MOCK_LEASE,
      isPending: false,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useLeaseHook.useLease>)

    vi.spyOn(useLeaseHook, 'usePayments').mockReturnValue({
      data: MOCK_PAYMENTS,
      isPending: false,
    } as unknown as ReturnType<typeof useLeaseHook.usePayments>)

    render(<LeaseDetailPage />, { wrapper })

    expect(screen.getByText('Jane Smith')).toBeInTheDocument()
    expect(screen.getByText('150 Lexington Ct')).toBeInTheDocument()
    expect(screen.getByText('Unit 1')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('filters payments to show only those for this lease', () => {
    vi.spyOn(useLeaseHook, 'useLease').mockReturnValue({
      data: MOCK_LEASE,
      isPending: false,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useLeaseHook.useLease>)

    vi.spyOn(useLeaseHook, 'usePayments').mockReturnValue({
      data: MOCK_PAYMENTS,
      isPending: false,
    } as unknown as ReturnType<typeof useLeaseHook.usePayments>)

    render(<LeaseDetailPage />, { wrapper })

    // LEASE-0001 payment ($1,200) should appear; LEASE-0002 payment ($1,800) should not
    expect(screen.getByText('$1,200')).toBeInTheDocument()
    expect(screen.queryByText('$1,800')).not.toBeInTheDocument()
  })
})
