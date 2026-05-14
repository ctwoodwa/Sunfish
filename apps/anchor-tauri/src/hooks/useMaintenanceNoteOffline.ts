import { invoke } from '@tauri-apps/api/core'
import { getNoteText, exportUpdate } from '@/lib/loro'
import { useSyncStore } from '@/stores/syncStore'
import { isTauri } from '@/utils/isTauri'
import { createMaintenanceTicket } from '@/api/erpnext'

export function useMaintenanceNoteSubmit(ticketName: string) {
  const syncState = useSyncStore((s) => s.syncState)

  return async (noteText: string) => {
    if (isTauri() && syncState === 'offline') {
      const text = getNoteText(ticketName)
      text.insert(0, noteText)
      const update = exportUpdate(ticketName)
      await invoke('enqueue_write', {
        doctype: 'Maintenance Note',
        opType: 'create',
        docName: null,
        payloadJson: JSON.stringify({
          ticket: ticketName,
          content: noteText,
          loro_snapshot: Array.from(update),
        }),
      })
    } else {
      await createMaintenanceTicket({ Subject: noteText, Property: '', Priority: 'Medium' })
    }
  }
}
