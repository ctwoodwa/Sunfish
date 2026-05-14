import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { PLReport } from './PLReport'
import type { PLSummary, Property } from '@/api/erpnext'

const { mockGetProperties, mockGetProfitLoss, mockExportProfitLoss } = vi.hoisted(() => ({
  mockGetProperties: vi.fn(),
  mockGetProfitLoss: vi.fn(),
  mockExportProfitLoss: vi.fn(),
}))
vi.mock('@/api/erpnext', () => ({
  getProperties: mockGetProperties,
  getProfitLoss: mockGetProfitLoss,
  exportProfitLoss: mockExportProfitLoss,
}))

const MOCK_PROPERTIES: Property[] = [
  { name: 'PROP-0001', property_name: '150 Lexington Ct', address_line_1: '150 Lexington Ct',
    city: 'Seattle', state: 'WA', postal_code: '98101', units: 1, status: 'Active', company: 'Royal Key' },
  { name: 'PROP-0002', property_name: '22 Harbor View', address_line_1: '22 Harbor View',
    city: 'Tacoma', state: 'WA', postal_code: '98402', units: 2, status: 'Active', company: 'Royal Key' },
]

const MOCK_SUMMARY: PLSummary = {
  period: '2026',
  income: 12000,
  expenses: 4500,
  net: 7500,
  incomeLines: [{ account: 'Rent Income', amount: 12000 }],
  expenseLines: [{ account: 'Repairs & Maintenance', amount: 4500 }],
}

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return (
    <QueryClientProvider client={qc}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  )
}

describe('PLReport page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGetProperties.mockResolvedValue(MOCK_PROPERTIES)
    mockGetProfitLoss.mockResolvedValue(MOCK_SUMMARY)
    mockExportProfitLoss.mockResolvedValue(undefined)
  })

  it('renders heading and selector controls', async () => {
    render(<PLReport />, { wrapper })
    expect(screen.getByRole('heading', { name: /profit & loss/i })).toBeInTheDocument()
    expect(screen.getByLabelText(/property/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/period/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/as of/i)).toBeInTheDocument()
  })

  it('loads and displays P&L summary data', async () => {
    render(<PLReport />, { wrapper })
    // $12,000 appears in both the summary card and the income-lines table — use getAllByText
    const incomeAmounts = await screen.findAllByText('$12,000.00')
    expect(incomeAmounts.length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('$7,500.00')).toBeInTheDocument()
    expect(screen.getByText('Rent Income')).toBeInTheDocument()
    expect(screen.getByText('Repairs & Maintenance')).toBeInTheDocument()
  })

  it('populates property selector from API', async () => {
    render(<PLReport />, { wrapper })
    const select = screen.getByLabelText(/property/i)
    expect(await screen.findByText('150 Lexington Ct')).toBeDefined()
    expect(select).toHaveTextContent('All properties')
    fireEvent.change(select, { target: { value: 'PROP-0001' } })
    expect((select as HTMLSelectElement).value).toBe('PROP-0001')
  })

  it('changes period selector and keeps value', async () => {
    render(<PLReport />, { wrapper })
    await screen.findByText('Rent Income') // unique anchor — income line label
    const periodSelect = screen.getByLabelText(/period/i)
    fireEvent.change(periodSelect, { target: { value: 'month' } })
    expect((periodSelect as HTMLSelectElement).value).toBe('month')
  })

  it('calls exportProfitLoss when Export CSV is clicked', async () => {
    render(<PLReport />, { wrapper })
    await screen.findByText('Rent Income') // unique anchor — income line label
    fireEvent.click(screen.getByRole('button', { name: /export csv/i }))
    await waitFor(() => expect(mockExportProfitLoss).toHaveBeenCalledOnce())
  })
})
