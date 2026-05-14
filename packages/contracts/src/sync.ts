/**
 * Local-first sync domain contracts.
 * Mirrors the Tauri Phase 3 offline queue (W#60 Phase 3) and the
 * AP/CP boundary model (ADR 0086).
 *
 * `SyncStatus` corresponds to the syncStore state atoms in
 * `apps/anchor-tauri/src/stores/syncStore.ts`.
 */

export type SyncStatus = 'online' | 'offline' | 'syncing'

/**
 * A single pending write in the local offline write queue.
 * Mirrors the SQLite `write_queue` table schema
 * (`apps/anchor-tauri/src-tauri/src/db.rs`).
 */
export interface OfflineQueueEntry {
  id: string
  doctype: string
  opType: 'create' | 'update' | 'delete'
  docName?: string
  payloadJson: string
  createdAt: string
  syncedAt?: string
  error?: string
}

/**
 * A CRDT merge conflict detected during peer sync (Phase 4+).
 * Phase 3 is single-writer CO; this type is reserved for Phase 4
 * bidirectional Accountant peer sync.
 */
export interface ConflictRecord {
  id: string
  doctype: string
  docName: string
  localVersion: unknown
  remoteVersion: unknown
  detectedAt: string
  resolution?: 'local-wins' | 'remote-wins' | 'manual'
}
