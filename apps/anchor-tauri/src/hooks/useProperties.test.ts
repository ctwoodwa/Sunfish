import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createElement } from 'react'
import { useProperties } from './useProperties'

const { mockIsTauri } = vi.hoisted(() => ({ mockIsTauri: vi.fn() }))

vi.mock('@tauri-apps/api/core', () => ({
  invoke: vi.fn(),
}))

vi.mock('@/api/erpnext', () => ({
  getProperties: vi.fn(),
}))

vi.mock('@/stores/companyStore', () => ({
  useCompanyStore: (sel: (s: { activeCompany: string }) => unknown) =>
    sel({ activeCompany: 'test-co' }),
}))

vi.mock('@/stores/syncStore', () => ({
  useSyncStore: (sel: (s: { setSyncState: () => void; setLastSyncedAt: () => void }) => unknown) =>
    sel({ setSyncState: vi.fn(), setLastSyncedAt: vi.fn() }),
}))

vi.mock('@/utils/isTauri', () => ({
  isTauri: mockIsTauri,
}))

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return createElement(QueryClientProvider, { client: qc }, children)
}

describe('useProperties — offline fallback', () => {
  beforeEach(() => {
    vi.resetAllMocks()
  })

  it('falls back to invoke when getProperties throws', async () => {
    mockIsTauri.mockReturnValue(true)
    const { getProperties } = await import('@/api/erpnext')
    const { invoke } = await import('@tauri-apps/api/core')
    ;(getProperties as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('offline'))
    ;(invoke as ReturnType<typeof vi.fn>).mockResolvedValue([
      { name: 'PROP-0001', property_name: '150 Lexington Ct' },
    ])

    const { result } = renderHook(() => useProperties(), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(invoke).toHaveBeenCalledWith('get_cached_properties')
    expect(result.current.data).toHaveLength(1)
  })

  it('returns live data when getProperties succeeds', async () => {
    mockIsTauri.mockReturnValue(true)
    const { getProperties } = await import('@/api/erpnext')
    const { invoke } = await import('@tauri-apps/api/core')
    const liveData = [{ name: 'PROP-0002', property_name: 'Oak Ave' }]
    ;(getProperties as ReturnType<typeof vi.fn>).mockResolvedValue(liveData)

    const { result } = renderHook(() => useProperties(), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(invoke).not.toHaveBeenCalled()
    expect(result.current.data).toEqual(liveData)
  })

  it('skips invoke entirely when not in Tauri', async () => {
    mockIsTauri.mockReturnValue(false)
    const { getProperties } = await import('@/api/erpnext')
    const { invoke } = await import('@tauri-apps/api/core')
    ;(getProperties as ReturnType<typeof vi.fn>).mockResolvedValue([])

    const { result } = renderHook(() => useProperties(), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(invoke).not.toHaveBeenCalled()
  })
})
