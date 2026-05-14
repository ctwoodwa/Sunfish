import { Loro, LoroText } from 'loro-crdt'

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
  return getLoroDoc(ticketName).exportSnapshot()
}

export function importUpdate(ticketName: string, update: Uint8Array): void {
  getLoroDoc(ticketName).import(update)
}
