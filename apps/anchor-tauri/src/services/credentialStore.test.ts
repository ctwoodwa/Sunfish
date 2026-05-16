// W#60 P4 PR 1 — credentialStore tests.
//
// Mocks `@tauri-apps/plugin-stronghold` + `@tauri-apps/api/path`. Verifies the
// init / setToken / getToken / clearToken surface without touching real
// Stronghold or filesystem state. Each test resets the credentialStore module
// cache via `resetForTesting()` to avoid cross-test leakage of the cached
// Stronghold handles.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// --- mocks ----------------------------------------------------------------

const mockStore = {
  insert: vi.fn<(key: string, data: number[]) => Promise<void>>(),
  get: vi.fn<(key: string) => Promise<number[]>>(),
  remove: vi.fn<(key: string) => Promise<void>>(),
}

const mockClient = {
  getStore: vi.fn(() => mockStore),
}

const mockStronghold = {
  loadClient: vi.fn<(name: string) => Promise<typeof mockClient>>(),
  createClient: vi.fn<(name: string) => Promise<typeof mockClient>>(),
  save: vi.fn<() => Promise<void>>(),
}

vi.mock('@tauri-apps/plugin-stronghold', () => ({
  Stronghold: {
    load: vi.fn(async () => mockStronghold),
  },
}))

vi.mock('@tauri-apps/api/path', () => ({
  appDataDir: vi.fn(async () => '/test/appdata'),
}))

// --- helpers --------------------------------------------------------------

async function importFresh() {
  // Each test imports the module fresh AFTER mocks are in place; resetForTesting
  // clears the module-level cache so the next call re-runs Stronghold.load.
  const mod = await import('./credentialStore')
  mod.resetForTesting()
  return mod
}

beforeEach(() => {
  vi.clearAllMocks()
  // Default: loadClient succeeds (existing client). Tests override to exercise
  // the create-fallback path.
  mockStronghold.loadClient.mockResolvedValue(mockClient)
  mockStronghold.createClient.mockResolvedValue(mockClient)
  mockStronghold.save.mockResolvedValue(undefined)
  mockStore.insert.mockResolvedValue(undefined)
  mockStore.remove.mockResolvedValue(undefined)
})

afterEach(() => {
  vi.resetModules()
})

// --- tests ----------------------------------------------------------------

describe('credentialStore', () => {
  it('init: opens snapshot at appDataDir + loads or creates the auth client', async () => {
    const { init } = await importFresh()
    // First attempt: loadClient succeeds (existing snapshot)
    await init()
    const { Stronghold } = await import('@tauri-apps/plugin-stronghold')
    expect(Stronghold.load).toHaveBeenCalledWith(
      '/test/appdata/anchor.stronghold',
      'anchor-machine-locked',
    )
    expect(mockStronghold.loadClient).toHaveBeenCalledWith('anchor-auth')
    // Should NOT have fallen back to createClient
    expect(mockStronghold.createClient).not.toHaveBeenCalled()

    // Second init call should reuse the cache — Stronghold.load called only once total
    await init()
    expect(Stronghold.load).toHaveBeenCalledTimes(1)
  })

  it('setToken: inserts UTF-8 bytes under the token key and persists the snapshot', async () => {
    const { setToken } = await importFresh()
    await setToken('test-bridge-token-abc123')

    expect(mockStore.insert).toHaveBeenCalledTimes(1)
    const [keyArg, dataArg] = mockStore.insert.mock.calls[0]!
    expect(keyArg).toBe('bridge-token')
    // Verify the bytes round-trip via TextDecoder
    const decoded = new TextDecoder().decode(new Uint8Array(dataArg))
    expect(decoded).toBe('test-bridge-token-abc123')
    expect(mockStronghold.save).toHaveBeenCalledTimes(1)
  })

  it('getToken: returns the decoded token when present, null when absent or empty', async () => {
    const { getToken, resetForTesting } = await importFresh()

    // Case 1: token present
    const sample = 'persisted-token-xyz'
    mockStore.get.mockResolvedValueOnce(Array.from(new TextEncoder().encode(sample)))
    const result = await getToken()
    expect(result).toBe(sample)

    // Case 2: empty array → null (treated as not-present)
    resetForTesting()
    mockStore.get.mockResolvedValueOnce([])
    const empty = await getToken()
    expect(empty).toBeNull()

    // Case 3: store.get throws (e.g., no record) → null, not surface error
    resetForTesting()
    mockStore.get.mockRejectedValueOnce(new Error('record not found'))
    const missing = await getToken()
    expect(missing).toBeNull()
  })

  it('clearToken: removes the record + persists when present; idempotent when already absent', async () => {
    const { clearToken, resetForTesting } = await importFresh()

    // Case 1: remove succeeds → save IS called
    mockStore.remove.mockResolvedValueOnce(undefined)
    await clearToken()
    expect(mockStore.remove).toHaveBeenCalledWith('bridge-token')
    expect(mockStronghold.save).toHaveBeenCalledTimes(1)

    // Case 2: remove throws (already absent) → no save call, no error surfaced
    resetForTesting()
    vi.clearAllMocks()
    mockStronghold.loadClient.mockResolvedValue(mockClient)
    mockStore.remove.mockRejectedValueOnce(new Error('not present'))
    await expect(clearToken()).resolves.toBeUndefined()
    expect(mockStronghold.save).not.toHaveBeenCalled()
  })
})
