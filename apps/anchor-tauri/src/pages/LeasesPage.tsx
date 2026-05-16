import { Link } from 'react-router-dom'
import { useLeases } from '@/hooks/useLeases'
import { Badge } from '@/components/ui/badge'
import type { Lease } from '@/api/erpnext'

function daysUntilExpiry(endDate: string): number {
  return Math.ceil((new Date(endDate).getTime() - Date.now()) / 86_400_000)
}

function LeaseStatusBadge({ status }: { status: Lease['status'] }) {
  const variant = status === 'Active' ? ('success' as const) : status === 'Expired' ? ('outline' as const) : ('secondary' as const)
  return <Badge variant={variant}>{status}</Badge>
}

export function LeasesPage() {
  const { data: leases, isPending, isError, error, refetch } = useLeases()

  if (isPending) {
    return <div className="flex items-center justify-center h-48 text-gray-500">Loading leases…</div>
  }

  if (isError) {
    return (
      <div className="rounded-lg border border-destructive/20 bg-destructive/10 p-6">
        <p className="font-semibold text-destructive">Failed to load leases</p>
        <p className="mt-1 text-sm text-gray-600">{error.message}</p>
        <button
          onClick={() => void refetch()}
          className="mt-3 rounded bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700"
        >
          Retry
        </button>
      </div>
    )
  }

  if (!leases?.length) {
    return (
      <div className="flex items-center justify-center h-48 text-gray-500">
        No leases found. Create a Lease record in ERPNext.
      </div>
    )
  }

  const now = Date.now()
  const expiringLeases = leases.filter(
    (l) => l.status === 'Active' && new Date(l.end_date).getTime() - now < 60 * 86_400_000,
  )

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Leases</h1>
        <p className="text-gray-500">{leases.length} leases</p>
      </div>

      {expiringLeases.length > 0 && (
        <div className="mb-6 rounded-lg border border-warning/20 bg-warning/10 p-4">
          <p className="font-semibold text-warning">
            {expiringLeases.length} lease{expiringLeases.length > 1 ? 's' : ''} expiring within 60 days
          </p>
          <ul className="mt-1 list-disc pl-5 text-sm text-warning">
            {expiringLeases.map((l) => (
              <li key={l.name}>
                {l.tenant} — {l.property} ({daysUntilExpiry(l.end_date)} days)
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="min-w-full divide-y divide-gray-200 text-sm">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Tenant</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Property / Unit</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Start</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">End</th>
              <th className="px-4 py-3 text-right font-medium text-gray-500">Monthly Rent</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Status</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 bg-white">
            {leases.map((l) => {
              const days = daysUntilExpiry(l.end_date)
              const expiringSoon = l.status === 'Active' && days < 60
              return (
                <tr key={l.name} className={expiringSoon ? 'bg-warning/10' : undefined}>
                  <td className="px-4 py-3 font-medium text-gray-900">{l.tenant}</td>
                  <td className="px-4 py-3 text-gray-600">
                    {l.property}
                    {l.unit ? ` · ${l.unit}` : ''}
                  </td>
                  <td className="px-4 py-3 text-gray-600">{l.start_date}</td>
                  <td className="px-4 py-3 text-gray-600">
                    {l.end_date}
                    {expiringSoon && (
                      <Badge variant="warning" className="ml-2 text-xs">
                        {days}d
                      </Badge>
                    )}
                  </td>
                  <td className="px-4 py-3 text-right text-gray-900">
                    ${l.monthly_rent.toLocaleString()}
                  </td>
                  <td className="px-4 py-3">
                    <LeaseStatusBadge status={l.status} />
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Link
                      to={`/leases/${l.name}`}
                      className="text-blue-600 hover:text-blue-800 text-xs"
                    >
                      Detail →
                    </Link>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
