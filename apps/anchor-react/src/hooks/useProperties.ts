import { useQuery } from '@tanstack/react-query'
// W#74 PR 1: rebound from ERPNext API to the Bridge /api/v1/properties
// cluster endpoint. Cache key unchanged so consumers don't re-render.
import { getProperties } from '@/api/properties'
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
