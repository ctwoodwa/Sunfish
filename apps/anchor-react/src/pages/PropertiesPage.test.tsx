import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { PropertiesPage } from './PropertiesPage'
// W#74 PR 1: rebound from ERPNext API to /api/v1/properties Bridge cluster endpoint.
import type { PropertyList } from '@/api/properties'

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

const mockData: PropertyList = {
  properties: [
    {
      propertyId: 'PROP-0001',
      displayName: '150 Lexington Ct',
      kind: 'MultiUnit',
      addressLine1: '150 Lexington Ct',
      city: 'Seattle',
      region: 'WA',
      unitCount: 4,
      status: 'Active',
      entityTag: null,
    },
    {
      propertyId: 'PROP-0002',
      displayName: '200 Main St',
      kind: 'SingleFamily',
      addressLine1: '200 Main St',
      city: 'Bellevue',
      region: 'WA',
      unitCount: 1,
      status: 'Vacant',
      entityTag: null,
    },
  ],
}

describe('PropertiesPage', () => {
  beforeEach(() => {
    mockUseProperties.mockReset()
  })

  it('renders property cards when data is available', async () => {
    mockUseProperties.mockReturnValue({
      data: mockData,
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

  it('shows empty-state copy without ERPNext mention', () => {
    mockUseProperties.mockReturnValue({
      data: { properties: [] },
      isPending: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    })

    render(<PropertiesPage />, { wrapper })

    expect(screen.getByText(/no properties found/i)).toBeInTheDocument()
    // W#74 PR 1: empty-state copy no longer references ERPNext.
    expect(screen.queryByText(/erpnext/i)).not.toBeInTheDocument()
  })
})
