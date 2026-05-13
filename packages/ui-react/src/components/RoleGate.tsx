import { type ReactNode } from 'react'

export interface RoleGateProps {
  role: string
  allow: string[]
  children: ReactNode
  fallback?: ReactNode
}

export function RoleGate({ role, allow, children, fallback = null }: RoleGateProps) {
  if (!allow.includes(role)) return <>{fallback}</>
  return <>{children}</>
}
