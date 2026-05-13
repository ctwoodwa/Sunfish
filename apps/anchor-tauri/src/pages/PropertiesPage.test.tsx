import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { PropertiesPage } from './PropertiesPage'
import type { Property } from '@/api/erpnext'

vi.mock('@/hooks/useProperties')

const mockUseProperties = vi.fn()
vi.mock('@/hooks/useProperties', () => ({
  useProperties: () => mockUseProperties(),
}))

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return (
    <QueryClientProvider client={qc}>
      <BrowserRouter>{children}</BrowserRouter>
    </QueryClientProvider>
  )
}

const mockProperties: Property[] = [
  {
    name: 'PROP-0001',
    property_name: '150 Lexington Ct',
    address_line_1: '150 Lexington Ct',
    city: 'Seattle',
    state: 'WA',
    postal_code: '98101',
    units: 4,
    status: 'Active',
    company: 'Royal Key Management LLC',
  },
  {
    name: 'PROP-0002',
    property_name: '200 Main St',
    address_line_1: '200 Main St',
    city: 'Bellevue',
    state: 'WA',
    postal_code: '98004',
    units: 1,
    status: 'Vacant',
    company: 'Royal Key Management LLC',
  },
]

describe('PropertiesPage', () => {
  beforeEach(() => {
    mockUseProperties.mockReset()
  })

  it('renders property cards when data is available', async () => {
    mockUseProperties.mockReturnValue({
      data: mockProperties,
      isPending: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    })

    render(<PropertiesPage />, { wrapper })

    await waitFor(() => {
      expect(screen.getByText('150 Lexington Ct')).toBeInTheDocument()
      expect(screen.getByText('200 Main St')).toBeInTheDocument()
    })

    expect(screen.getByText('Active')).toBeInTheDocument()
    expect(screen.getByText('Vacant')).toBeInTheDocument()
    expect(screen.getByText('4 units')).toBeInTheDocument()
    expect(screen.getByText('1 unit')).toBeInTheDocument()
  })

  it('shows loading state while pending', () => {
    mockUseProperties.mockReturnValue({
      data: undefined,
      isPending: true,
      isError: false,
      error: null,
      refetch: vi.fn(),
    })

    render(<PropertiesPage />, { wrapper })

    expect(screen.getByText(/loading properties/i)).toBeInTheDocument()
  })

  it('shows error state with retry button on failure', () => {
    const mockRefetch = vi.fn()
    mockUseProperties.mockReturnValue({
      data: undefined,
      isPending: false,
      isError: true,
      error: new Error('Bridge unavailable'),
      refetch: mockRefetch,
    })

    render(<PropertiesPage />, { wrapper })

    expect(screen.getByText(/failed to load properties/i)).toBeInTheDocument()
    expect(screen.getByText('Bridge unavailable')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument()
  })
})
