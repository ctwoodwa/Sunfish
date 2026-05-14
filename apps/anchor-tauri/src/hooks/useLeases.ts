import { useQuery } from '@tanstack/react-query'
import { invoke } from '@tauri-apps/api/core'
import { getLeases, getLease, getPayments, type Lease, type Payment } from '@/api/erpnext'
import { useCompanyStore } from '@/stores/companyStore'
import { useSyncStore } from '@/stores/syncStore'
import { isTauri } from '@/utils/isTauri'

export function useLeases() {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  const setSyncState = useSyncStore((s) => s.setSyncState)

  return useQuery({
    queryKey: ['leases', activeCompany],
    queryFn: async () => {
      if (isTauri()) {
        try {
          return await getLeases()
        } catch {
          setSyncState('offline')
          return invoke<Lease[]>('get_cached_leases')
        }
      }
      return getLeases()
    },
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}

export function useLease(name: string) {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  return useQuery({
    queryKey: ['lease', name, activeCompany],
    queryFn: () => getLease(name),
    enabled: Boolean(name),
  })
}

export function usePayments() {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  const setSyncState = useSyncStore((s) => s.setSyncState)

  return useQuery({
    queryKey: ['payments', activeCompany],
    queryFn: async () => {
      if (isTauri()) {
        try {
          return await getPayments()
        } catch {
          setSyncState('offline')
          return invoke<Payment[]>('get_cached_payments')
        }
      }
      return getPayments()
    },
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}
