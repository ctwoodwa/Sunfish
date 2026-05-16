import { type ReactNode } from 'react'

export interface PropertyCardProps {
  name: string
  address: string
  city: string
  state: string
  units: number
  status: 'Active' | 'Vacant' | 'Maintenance' | 'Sold' | string
  className?: string
  actions?: ReactNode
}

const STATUS_STYLES: Record<string, string> = {
  Active:      'bg-success/15 text-success',
  Vacant:      'bg-warning/15 text-warning',
  Maintenance: 'bg-priority-medium text-priority-medium-fg',
  Sold:        'bg-muted text-muted-foreground',
}

export function PropertyCard({ name, address, city, state, units, status, className = '', actions }: PropertyCardProps) {
  return (
    <div className={`rounded-lg border border-border bg-card p-4 shadow-sm ${className}`}>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-card-foreground">{address}</p>
          <p className="text-xs text-muted-foreground">{city}, {state}</p>
        </div>
        <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[status] ?? 'bg-muted text-muted-foreground'}`}>
          {status}
        </span>
      </div>
      <div className="mt-3 flex items-center justify-between">
        <span className="text-xs text-muted-foreground">{units} unit{units !== 1 ? 's' : ''}</span>
        <span className="font-mono text-xs text-muted-foreground">{name}</span>
      </div>
      {actions && <div className="mt-3 border-t border-border pt-3">{actions}</div>}
    </div>
  )
}
