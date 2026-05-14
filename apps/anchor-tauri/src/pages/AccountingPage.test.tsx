import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { AccountingPage } from './AccountingPage'
import type { AccountingSummary, OutstandingInvoice } from '@/api/erpnext'

const { mockGetSummary, mockGetOutstanding } = vi.hoisted(() => ({
  mockGetSummary: vi.fn(),
  mockGetOutstanding: vi.fn(),
}))

vi.mock('@/api/erpnext', () => ({
  getAccountingSummary: mockGetSummary,
  getAccountingOutstanding: mockGetOutstanding,
}))

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return (
    <QueryClientProvider client={qc}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  )
}

const MOCK_SUMMARY: AccountingSummary = {
  period: 'Jan–Dec 2025',
  income: 84000,
  expenses: 31500,
  net: 52500,
}

const MOCK_INVOICES: OutstandingInvoice[] = [
  { name: 'SINV-0001', customer: 'Jane Smith', outstanding_amount: 1500, due_date: '2025-06-01', status: 'Unpaid' },
]

describe('AccountingPage', () => {
  beforeEach(() => {
    mockGetSummary.mockReset()
    mockGetOutstanding.mockReset()
  })

  it('renders P&L summary with correct figures when data loads', async () => {
    mockGetSummary.mockResolvedValueOnce(MOCK_SUMMARY)
    mockGetOutstanding.mockResolvedValueOnce([])

    render(<AccountingPage />, { wrapper })

    expect(await screen.findByText('$84,000.00')).toBeInTheDocument()
    expect(screen.getByText('$31,500.00')).toBeInTheDocument()
    expect(screen.getByText('$52,500.00')).toBeInTheDocument()
    expect(screen.getByText('Jan–Dec 2025')).toBeInTheDocument()
  })

  it('shows loading indicator while P&L summary fetches', () => {
    mockGetSummary.mockReturnValue(new Promise(() => {}))
    mockGetOutstanding.mockResolvedValueOnce([])

    render(<AccountingPage />, { wrapper })

    expect(screen.getAllByText(/Loading…/i).length).toBeGreaterThan(0)
  })

  it('shows error message when P&L summary fails', async () => {
    mockGetSummary.mockRejectedValueOnce(new Error('ERPNext unreachable'))
    mockGetOutstanding.mockResolvedValueOnce([])

    render(<AccountingPage />, { wrapper })

    expect(await screen.findByText(/Could not load accounting summary.*ERPNext unreachable/i)).toBeInTheDocument()
  })

  it('renders outstanding invoice rows when data loads', async () => {
    mockGetSummary.mockResolvedValueOnce(MOCK_SUMMARY)
    mockGetOutstanding.mockResolvedValueOnce(MOCK_INVOICES)

    render(<AccountingPage />, { wrapper })

    expect(await screen.findByText('Jane Smith')).toBeInTheDocument()
    expect(screen.getByText('SINV-0001')).toBeInTheDocument()
    expect(screen.getByText('$1,500.00')).toBeInTheDocument()
    expect(screen.getByText('2025-06-01')).toBeInTheDocument()
  })

  it('shows "No outstanding invoices" when the list is empty', async () => {
    mockGetSummary.mockResolvedValueOnce(MOCK_SUMMARY)
    mockGetOutstanding.mockResolvedValueOnce([])

    render(<AccountingPage />, { wrapper })

    expect(await screen.findByText('No outstanding invoices.')).toBeInTheDocument()
  })
})
