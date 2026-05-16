import { create } from 'zustand'

interface AuthState {
  user: string
  role: string
  /**
   * Bridge auth token. `null` indicates the user is not authenticated and the
   * LoginPage should be shown. `''` (empty string) is treated the same as null
   * for gate purposes — `setToken('')` is the logout path.
   */
  token: string | null
  loaded: boolean
  setAuth: (user: string, role: string) => void
  setToken: (token: string | null) => void
}

export const useAuthStore = create<AuthState>()((set) => ({
  user: '',
  role: '',
  token: null,
  loaded: false,
  setAuth: (user, role) => set({ user, role, loaded: true }),
  setToken: (token) => set({ token: token && token.length > 0 ? token : null }),
}))
