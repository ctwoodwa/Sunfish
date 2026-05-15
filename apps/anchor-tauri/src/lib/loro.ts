import { Loro, LoroText } from 'loro-crdt'

// PHASE 3: in-memory only. Acceptable because Phase 3 is single-writer CO and
// each enqueue ships full content in payload_json.content.
// PHASE 4: persist docs to SQLite so CRDT history survives restart and peer merge is correct.
const docs = new Map<string, Loro>()

export function getLoroDoc(ticketName: string): Loro {
  if (!docs.has(ticketName)) {
    docs.set(ticketName, new Loro())
  }
  return docs.get(ticketName)!
}

export function getNoteText(ticketName: string): LoroText {
  return getLoroDoc(ticketName).getText('notes')
}

export function exportUpdate(ticketName: string): Uint8Array {
  // loro-crdt 1.x replaced per-mode helpers (exportSnapshot, exportFrom)
  // with a unified `doc.export({ mode })`.
  return getLoroDoc(ticketName).export({ mode: 'snapshot' })
}

// PHASE 4 SECURITY: validate signature + size cap before import when peer sync is added.
export function importUpdate(ticketName: string, update: Uint8Array): void {
  getLoroDoc(ticketName).import(update)
}
