import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { RentRoll } from './RentRoll'
import type { RentRollRow } from '@/api/erpnext'

const { mockGetRentRoll } = vi.hoisted(() => ({ mockGetRentRoll: vi.fn() }))
vi.mock('@/api/erpnext', () => ({ getRentRoll: mockGetRentRoll }))

const MOCK_ROWS: RentRollRow[] = [
  {
    propertyId: 'PROP-0001',
    propertyName: '150 Lexington Ct',
    unit: 'A',
    tenantName: 'John Doe',
    leaseStart: '2025-01-01',
    leaseEnd: '2025-12-31',
    monthlyRent: 1500,
    lastPaymentDate: '2025-04-01',
    balanceDue: 0,
    status: 'Current',
  },
  {
    propertyId: 'PROP-0002',
    propertyName: '22 Harbor View',
    unit: undefined,
    tenantName: 'Jane Smith',
    leaseStart: '2024-06-01',
    leaseEnd: '2025-05-31',
    monthlyRent: 2200,
    lastPaymentDate: '2025-03-01',
    balanceDue: 2200,
    status: 'Overdue',
  },
  {
    propertyId: 'PROP-0003',
    propertyName: '88 Sunset Ridge',
    unit: 'B',
    tenantName: '',
    leaseStart: undefined,
    leaseEnd: undefined,
    monthlyRent: 1800,
    lastPaymentDate: undefined,
    balanceDue: 0,
    status: 'Vacant',
  },
]

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return (
    <QueryClientProvider client={qc}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  )
}

describe('RentRoll page', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders the page heading', async () => {
    mockGetRentRoll.mockResolvedValue([])
    render(<RentRoll />, { wrapper })
    expect(screen.getByRole('heading', { name: /rent roll/i })).toBeInTheDocument()
  })

  it('loads and displays rent roll rows', async () => {
    mockGetRentRoll.mockResolvedValue(MOCK_ROWS)
    render(<RentRoll />, { wrapper })
    expect(await screen.findByText('150 Lexington Ct')).toBeInTheDocument()
    expect(screen.getByText('John Doe')).toBeInTheDocument()
    expect(screen.getByText('22 Harbor View')).toBeInTheDocument()
  })

  it('sorts Overdue rows before Current rows', async () => {
    mockGetRentRoll.mockResolvedValue(MOCK_ROWS)
    render(<RentRoll />, { wrapper })
    await screen.findByText('150 Lexington Ct')
    const rows = screen.getAllByRole('row')
    // First data row (index 1, header is 0) should be the Overdue property
    expect(rows[1]).toHaveTextContent('22 Harbor View')
  })

  it('shows vacant unit with em-dash for tenant and unit', async () => {
    mockGetRentRoll.mockResolvedValue(MOCK_ROWS)
    render(<RentRoll />, { wrapper })
    await screen.findByText('88 Sunset Ridge')
    // Vacant row has no tenant name — shows em-dash fallback
    const vacantRows = screen.getAllByText('—')
    expect(vacantRows.length).toBeGreaterThan(0)
  })

  it('shows error state when API fails', async () => {
    mockGetRentRoll.mockRejectedValue(new Error('ERPNext unavailable'))
    render(<RentRoll />, { wrapper })
    expect(await screen.findByText(/could not load rent roll/i)).toBeInTheDocument()
  })
})
