import { useEffect, useState } from 'react'

export interface FreshnessBadgeProps {
  updatedAt: Date | string | number
  staleAfterMs?: number
  className?: string
}

function formatRelative(ms: number): string {
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s ago`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ago`
  const h = Math.floor(m / 60)
  return `${h}h ago`
}

export function FreshnessBadge({ updatedAt, staleAfterMs = 5 * 60 * 1000, className = '' }: FreshnessBadgeProps) {
  const [mounted, setMounted] = useState(false)
  useEffect(() => { setMounted(true) }, [])

  if (!mounted) return null

  const ts = new Date(updatedAt).getTime()
  const age = Date.now() - ts
  const stale = age > staleAfterMs

  return (
    <span
      className={`inline-flex items-center gap-1 text-xs ${stale ? 'text-warning' : 'text-muted-foreground'} ${className}`}
      title={new Date(updatedAt).toLocaleString()}
    >
      {stale && (
        <svg className="h-3 w-3" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
          <path fillRule="evenodd" d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.625-1.516 2.625H3.72c-1.347 0-2.189-1.458-1.515-2.625L8.485 2.495z" clipRule="evenodd" />
        </svg>
      )}
      {formatRelative(age)}
    </span>
  )
}
