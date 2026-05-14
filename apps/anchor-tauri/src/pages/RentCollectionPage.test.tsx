import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { RentCollectionPage } from './RentCollectionPage'

const { stateRef } = vi.hoisted(() => ({
  stateRef: { syncState: 'synced' },
}))

const mockUseLeases = vi.fn()
vi.mock('@/hooks/useLeases', () => ({
  useLeases: () => mockUseLeases(),
}))

vi.mock('@/api/erpnext', () => ({
  recordPayment: vi.fn(),
}))

vi.mock('@/stores/syncStore', () => ({
  useSyncStore: (selector: (s: { syncState: string }) => unknown) =>
    selector({ syncState: stateRef.syncState }),
}))

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return (
    <QueryClientProvider client={qc}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  )
}

describe('RentCollectionPage offline guard', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    stateRef.syncState = 'synced'
    mockUseLeases.mockReturnValue({ data: [], isPending: false, isError: false })
  })

  it('submit button is enabled when online', () => {
    stateRef.syncState = 'synced'
    render(<RentCollectionPage />, { wrapper })
    const btn = screen.getByRole('button', { name: /record payment/i })
    expect(btn).not.toBeDisabled()
  })

  it('submit button is disabled when offline', () => {
    stateRef.syncState = 'offline'
    render(<RentCollectionPage />, { wrapper })
    const btn = screen.getByRole('button', { name: /network required/i })
    expect(btn).toBeDisabled()
  })

  it('shows offline warning message when offline', () => {
    stateRef.syncState = 'offline'
    render(<RentCollectionPage />, { wrapper })
    expect(screen.getByText(/rent payments require a live connection/i)).toBeInTheDocument()
  })
})
