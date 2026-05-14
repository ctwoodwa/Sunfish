import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { MaintenancePage } from './MaintenancePage'
import type { MaintenanceTicket } from '@/api/erpnext'

const { mockGetTickets, mockCreateTicket, mockUpdateTicket, mockRole } = vi.hoisted(() => ({
  mockGetTickets: vi.fn(),
  mockCreateTicket: vi.fn(),
  mockUpdateTicket: vi.fn(),
  mockRole: { value: 'owner' as string, loaded: true },
}))

vi.mock('@/api/erpnext', () => ({
  getMaintenanceTickets: mockGetTickets,
  createMaintenanceTicket: mockCreateTicket,
  updateMaintenanceTicket: mockUpdateTicket,
}))

vi.mock('@/components/AuthRoleGate', () => ({
  AuthRoleGate: ({ allow, children, fallback = null }: { allow: string[]; children: React.ReactNode; fallback?: React.ReactNode }) =>
    allow.includes(mockRole.value) ? <>{children}</> : <>{fallback}</>,
}))

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return (
    <QueryClientProvider client={qc}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  )
}

const MOCK_TICKETS: MaintenanceTicket[] = [
  { name: 'MT-0001', subject: 'Window broken', property: '150 Lexington Ct', status: 'Open', priority: 'High', assigned_to: null, cost: null },
  { name: 'MT-0002', subject: 'HVAC serviced', property: '200 Oak Ave', status: 'Resolved', priority: 'Medium', assigned_to: 'Joe', cost: 350.5 },
]

describe('MaintenancePage', () => {
  beforeEach(() => {
    mockGetTickets.mockReset()
    mockCreateTicket.mockReset()
    mockUpdateTicket.mockReset()
    mockRole.value = 'owner'
    mockRole.loaded = true
  })

  it('shows loading state while tickets are fetching', () => {
    mockGetTickets.mockReturnValue(new Promise(() => {}))

    render(<MaintenancePage />, { wrapper })

    expect(screen.getByText(/loading maintenance tickets/i)).toBeInTheDocument()
  })

  it('shows error state when fetch fails', async () => {
    mockGetTickets.mockRejectedValueOnce(new Error('Bridge down'))

    render(<MaintenancePage />, { wrapper })

    expect(await screen.findByText(/Error: Bridge down/i)).toBeInTheDocument()
  })

  it('renders ticket rows with correct data', async () => {
    mockGetTickets.mockResolvedValueOnce(MOCK_TICKETS)

    render(<MaintenancePage />, { wrapper })

    expect(await screen.findByText('Window broken')).toBeInTheDocument()
    expect(screen.getByText('HVAC serviced')).toBeInTheDocument()
    expect(screen.getByText('MT-0001')).toBeInTheDocument()
    expect(screen.getByText('$350.50')).toBeInTheDocument()
  })

  it('shows open ticket count in subtitle', async () => {
    mockGetTickets.mockResolvedValueOnce(MOCK_TICKETS)

    render(<MaintenancePage />, { wrapper })

    // MT-0001 is Open, MT-0002 is Resolved → 1 open ticket
    expect(await screen.findByText('1 open ticket')).toBeInTheDocument()
  })

  it('shows empty state when no tickets', async () => {
    mockGetTickets.mockResolvedValueOnce([])

    render(<MaintenancePage />, { wrapper })

    expect(await screen.findByText(/no maintenance tickets found/i)).toBeInTheDocument()
  })

  it('shows create-ticket form for owner role', async () => {
    mockGetTickets.mockResolvedValueOnce([])
    mockRole.value = 'owner'

    render(<MaintenancePage />, { wrapper })

    expect(await screen.findByText('New Ticket')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /submit/i })).toBeInTheDocument()
  })

  it('hides create-ticket form for tenant role', async () => {
    mockGetTickets.mockResolvedValueOnce([])
    mockRole.value = 'tenant'

    render(<MaintenancePage />, { wrapper })

    await screen.findByText(/no maintenance tickets found/i)
    expect(screen.queryByText('New Ticket')).not.toBeInTheDocument()
  })
})
