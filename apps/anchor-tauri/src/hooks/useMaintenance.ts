import { useQuery } from '@tanstack/react-query'
import { invoke } from '@tauri-apps/api/core'
import { getMaintenanceTickets, type MaintenanceTicket } from '@/api/erpnext'
import { useSyncStore } from '@/stores/syncStore'
import { isTauri } from '@/utils/isTauri'

export function useMaintenanceTickets() {
  const setSyncState = useSyncStore((s) => s.setSyncState)

  return useQuery<MaintenanceTicket[]>({
    queryKey: ['maintenance'],
    queryFn: async () => {
      if (isTauri()) {
        try {
          return await getMaintenanceTickets()
        } catch {
          setSyncState('offline')
          return invoke<MaintenanceTicket[]>('get_cached_maintenance_tickets')
        }
      }
      return getMaintenanceTickets()
    },
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}
