import { type ReactNode } from 'react'
import { useAuthStore } from '@/stores/authStore'

interface RoleGateProps {
  allow: string[]
  children: ReactNode
  fallback?: ReactNode
}

export function RoleGate({ allow, children, fallback = null }: RoleGateProps) {
  const role = useAuthStore((s) => s.role)
  const loaded = useAuthStore((s) => s.loaded)

  if (!loaded) return null
  if (!allow.includes(role)) return <>{fallback}</>
  return <>{children}</>
}
