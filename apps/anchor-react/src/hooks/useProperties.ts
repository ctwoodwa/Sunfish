import { useQuery } from '@tanstack/react-query'
import { getProperties } from '@/api/erpnext'
import { useCompanyStore } from '@/stores/companyStore'

export function useProperties() {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  return useQuery({
    queryKey: ['properties', activeCompany],
    queryFn: getProperties,
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}
