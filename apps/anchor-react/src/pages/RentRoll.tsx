import { useQuery } from '@tanstack/react-query'
import { getRentRoll, type RentRollRow } from '@/api/erpnext'

function StatusBadge({ status }: { status: RentRollRow['status'] }) {
  const styles = {
    Current: 'bg-green-100 text-green-800',
    Overdue: 'bg-red-100 text-red-800',
    Vacant:  'bg-gray-100 text-gray-600',
  } as const
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${styles[status]}`}>
      {status}
    </span>
  )
}

function fmt(n: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)
}

export function RentRoll() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['reports', 'rent-roll'],
    queryFn: getRentRoll,
    staleTime: 5 * 60 * 1000,
  })

  const sorted = data
    ? [...data].sort((a, b) => {
        const order = { Overdue: 0, Current: 1, Vacant: 2 }
        return (order[a.status] ?? 3) - (order[b.status] ?? 3)
      })
    : []

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900">Rent Roll</h1>
        <p className="mt-1 text-sm text-gray-500">All properties × units — occupancy and payment status</p>
      </div>

      {isLoading && <p className="text-sm text-gray-500">Loading…</p>}

      {isError && (
        <p className="text-sm text-red-600">
          Could not load rent roll: {(error as Error).message}
        </p>
      )}

      {sorted.length > 0 && (
        <div className="overflow-hidden rounded-lg border border-gray-200">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Property</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Unit</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Tenant</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Lease Start</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Lease End</th>
                <th className="px-4 py-3 text-right font-medium text-gray-500">Monthly Rent</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Last Payment</th>
                <th className="px-4 py-3 text-right font-medium text-gray-500">Balance Due</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {sorted.map((row, i) => (
                <tr key={`${row.propertyId}-${row.unit ?? i}`} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium text-gray-900">{row.propertyName}</td>
                  <td className="px-4 py-3 text-gray-600">{row.unit ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-900">{row.tenantName || '—'}</td>
                  <td className="px-4 py-3 text-gray-600">{row.leaseStart ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-600">{row.leaseEnd ?? '—'}</td>
                  <td className="px-4 py-3 text-right text-gray-900">{fmt(row.monthlyRent)}</td>
                  <td className="px-4 py-3 text-gray-600">{row.lastPaymentDate ?? '—'}</td>
                  <td className="px-4 py-3 text-right font-medium text-gray-900">{fmt(row.balanceDue)}</td>
                  <td className="px-4 py-3">
                    <StatusBadge status={row.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!isLoading && !isError && sorted.length === 0 && (
        <p className="text-sm text-gray-500">No leases found.</p>
      )}
    </div>
  )
}
