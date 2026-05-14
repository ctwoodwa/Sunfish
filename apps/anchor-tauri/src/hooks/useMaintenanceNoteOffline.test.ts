import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useMaintenanceNoteSubmit } from './useMaintenanceNoteOffline'

const { mockIsTauri, mockInvoke, mockCreateMaintenanceTicket, mockNoteInsert, stateRef } =
  vi.hoisted(() => ({
    mockIsTauri: vi.fn(),
    mockInvoke: vi.fn(),
    mockCreateMaintenanceTicket: vi.fn(),
    mockNoteInsert: vi.fn(),
    stateRef: { syncState: 'synced' },
  }))

vi.mock('@/utils/isTauri', () => ({ isTauri: mockIsTauri }))
vi.mock('@tauri-apps/api/core', () => ({ invoke: mockInvoke }))
vi.mock('@/api/erpnext', () => ({ createMaintenanceTicket: mockCreateMaintenanceTicket }))
vi.mock('@/lib/loro', () => ({
  getNoteText: () => ({ insert: mockNoteInsert }),
  exportUpdate: () => new Uint8Array([1, 2, 3]),
}))
vi.mock('@/stores/syncStore', () => ({
  useSyncStore: (selector: (s: { syncState: string }) => unknown) =>
    selector({ syncState: stateRef.syncState }),
}))

describe('useMaintenanceNoteSubmit', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    mockIsTauri.mockReturnValue(false)
    stateRef.syncState = 'synced'
  })

  it('invokes enqueue_write when Tauri + offline', async () => {
    mockIsTauri.mockReturnValue(true)
    stateRef.syncState = 'offline'
    mockInvoke.mockResolvedValue(undefined)

    const submit = useMaintenanceNoteSubmit('MT-001')
    await submit('roof leak')

    expect(mockInvoke).toHaveBeenCalledWith('enqueue_write', expect.objectContaining({
      doctype: 'Maintenance Note',
      opType: 'create',
      docName: null,
    }))
    expect(mockCreateMaintenanceTicket).not.toHaveBeenCalled()
  })

  it('calls live Bridge when Tauri + online', async () => {
    mockIsTauri.mockReturnValue(true)
    stateRef.syncState = 'synced'
    mockCreateMaintenanceTicket.mockResolvedValue({})

    const submit = useMaintenanceNoteSubmit('MT-001')
    await submit('roof leak')

    expect(mockCreateMaintenanceTicket).toHaveBeenCalled()
    expect(mockInvoke).not.toHaveBeenCalled()
  })

  it('calls live Bridge when not in Tauri (browser)', async () => {
    mockIsTauri.mockReturnValue(false)
    stateRef.syncState = 'synced'
    mockCreateMaintenanceTicket.mockResolvedValue({})

    const submit = useMaintenanceNoteSubmit('MT-002')
    await submit('broken window')

    expect(mockCreateMaintenanceTicket).toHaveBeenCalled()
    expect(mockInvoke).not.toHaveBeenCalled()
  })
})
