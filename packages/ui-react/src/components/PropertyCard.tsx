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
  Active:      'bg-green-100 text-green-700',
  Vacant:      'bg-yellow-100 text-yellow-700',
  Maintenance: 'bg-orange-100 text-orange-700',
  Sold:        'bg-gray-100 text-gray-500',
}

export function PropertyCard({ name, address, city, state, units, status, className = '', actions }: PropertyCardProps) {
  return (
    <div className={`rounded-lg border border-gray-200 bg-white p-4 shadow-sm ${className}`}>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-gray-900">{address}</p>
          <p className="text-xs text-gray-500">{city}, {state}</p>
        </div>
        <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[status] ?? 'bg-gray-100 text-gray-600'}`}>
          {status}
        </span>
      </div>
      <div className="mt-3 flex items-center justify-between">
        <span className="text-xs text-gray-500">{units} unit{units !== 1 ? 's' : ''}</span>
        <span className="font-mono text-xs text-gray-400">{name}</span>
      </div>
      {actions && <div className="mt-3 border-t border-gray-100 pt-3">{actions}</div>}
    </div>
  )
}
