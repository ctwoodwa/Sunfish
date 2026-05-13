import { useQuery } from '@tanstack/react-query'
import { getLeases, getLease, getPayments } from '@/api/erpnext'
import { useCompanyStore } from '@/stores/companyStore'

export function useLeases() {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  return useQuery({
    queryKey: ['leases', activeCompany],
    queryFn: getLeases,
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
  return useQuery({
    queryKey: ['payments', activeCompany],
    queryFn: getPayments,
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}
