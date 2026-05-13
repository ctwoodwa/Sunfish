import { create } from 'zustand'

interface CompanyState {
  activeCompany: string
  availableCompanies: string[]
  setActiveCompany: (company: string) => void
  setAvailableCompanies: (companies: string[]) => void
}

export const useCompanyStore = create<CompanyState>()((set) => ({
  activeCompany: '',
  availableCompanies: [],
  setActiveCompany: (company) => set({ activeCompany: company }),
  setAvailableCompanies: (companies) => set({ availableCompanies: companies }),
}))
