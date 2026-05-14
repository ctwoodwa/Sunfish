import { useCompanyStore } from '@/stores/companyStore'
import { useQueryClient } from '@tanstack/react-query'

export function CompanySwitcher() {
  const { activeCompany, availableCompanies, setActiveCompany } = useCompanyStore()
  const queryClient = useQueryClient()

  if (availableCompanies.length <= 1) return null

  function handleChange(e: React.ChangeEvent<HTMLSelectElement>) {
    setActiveCompany(e.target.value)
    void queryClient.invalidateQueries()
  }

  return (
    <select
      value={activeCompany}
      onChange={handleChange}
      className="rounded border border-gray-300 bg-white px-3 py-1.5 text-sm"
      aria-label="Active company"
    >
      {availableCompanies.map((c) => (
        <option key={c} value={c}>
          {c}
        </option>
      ))}
    </select>
  )
}
