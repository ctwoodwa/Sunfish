import { type ReactNode } from 'react'
import { RoleGate } from '@sunfish/ui-react'
import { useAuthStore } from '@/stores/authStore'

interface AuthRoleGateProps {
  allow: string[]
  children: ReactNode
  fallback?: ReactNode
}

/**
 * App-level role gate. Reads the current user's role from authStore (populated
 * from /api/v1/whoami) and delegates rendering logic to @sunfish/ui-react RoleGate.
 * Use this in apps/anchor-react; use @sunfish/ui-react RoleGate directly when
 * you supply the role value from elsewhere.
 */
export function AuthRoleGate({ allow, children, fallback = null }: AuthRoleGateProps) {
  const role = useAuthStore((s) => s.role)
  const loaded = useAuthStore((s) => s.loaded)

  if (!loaded) return null
  return <RoleGate role={role} allow={allow} fallback={fallback}>{children}</RoleGate>
}
