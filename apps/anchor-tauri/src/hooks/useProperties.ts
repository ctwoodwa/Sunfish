import { useQuery } from '@tanstack/react-query'
import { invoke } from '@tauri-apps/api/core'
import { getProperties, type Property } from '@/api/erpnext'
import { useCompanyStore } from '@/stores/companyStore'
import { useSyncStore } from '@/stores/syncStore'
import { isTauri } from '@/utils/isTauri'

export function useProperties() {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  const setSyncState = useSyncStore((s) => s.setSyncState)
  const setLastSyncedAt = useSyncStore((s) => s.setLastSyncedAt)

  return useQuery({
    queryKey: ['properties', activeCompany],
    queryFn: async () => {
      if (isTauri()) {
        setSyncState('syncing')
        try {
          const live = await getProperties()
          setSyncState('synced')
          setLastSyncedAt(new Date())
          return live
        } catch {
          setSyncState('offline')
          return invoke<Property[]>('get_cached_properties')
        }
      }
      return getProperties()
    },
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}
