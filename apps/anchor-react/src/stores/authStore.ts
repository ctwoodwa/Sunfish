import { create } from 'zustand'

interface AuthState {
  user: string
  role: string
  loaded: boolean
  setAuth: (user: string, role: string) => void
}

export const useAuthStore = create<AuthState>()((set) => ({
  user: '',
  role: '',
  loaded: false,
  setAuth: (user, role) => set({ user, role, loaded: true }),
}))
