import { type ReactNode } from 'react'

export type SyncState = 'synced' | 'syncing' | 'pending' | 'error' | 'offline'

const STATE_STYLES: Record<SyncState, { dot: string; label: string }> = {
  synced:  { dot: 'bg-green-500',  label: 'Synced' },
  syncing: { dot: 'bg-blue-500 animate-pulse', label: 'Syncing…' },
  pending: { dot: 'bg-yellow-500', label: 'Pending' },
  error:   { dot: 'bg-red-500',    label: 'Error' },
  offline: { dot: 'bg-gray-400',   label: 'Offline' },
}

export interface SyncStateBadgeProps {
  state: SyncState
  label?: ReactNode
  className?: string
}

export function SyncStateBadge({ state, label, className = '' }: SyncStateBadgeProps) {
  const { dot, label: defaultLabel } = STATE_STYLES[state]
  return (
    <span className={`inline-flex items-center gap-1.5 text-xs text-gray-600 ${className}`}>
      <span className={`h-2 w-2 rounded-full ${dot}`} aria-hidden="true" />
      {label ?? defaultLabel}
    </span>
  )
}
