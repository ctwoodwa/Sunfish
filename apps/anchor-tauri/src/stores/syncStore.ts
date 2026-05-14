import { create } from 'zustand'
import type { SyncState } from '@sunfish/ui-react'

interface SyncStoreState {
  syncState: SyncState
  lastSyncedAt: Date | null
  setSyncState: (s: SyncState) => void
  setLastSyncedAt: (d: Date) => void
}

export const useSyncStore = create<SyncStoreState>()((set) => ({
  syncState: 'pending',
  lastSyncedAt: null,
  setSyncState: (syncState) => set({ syncState }),
  setLastSyncedAt: (lastSyncedAt) => set({ lastSyncedAt }),
}))
