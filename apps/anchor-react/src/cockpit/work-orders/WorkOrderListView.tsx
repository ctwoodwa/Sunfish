import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { getCockpitWorkOrders } from '@/cockpit/api'

/**
 * W#29 Phase 3 — work-orders list view.
 *
 * Per XO ruling on the W#29 P2 halt: there is intentionally no "filter
 * by property" control because `WorkOrder` has no `PropertyId` field
 * (W#62 / W#62.1 will add it). Status, vendor, and date-range filters
 * map directly to the Bridge endpoint's query string.
 */
export function WorkOrderListView() {
  const [status, setStatus] = useState<string>('')
  const [page, setPage] = useState<number>(1)
  const pageSize = 20

  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['cockpit-work-orders', { status, page, pageSize }],
    queryFn: () => getCockpitWorkOrders({ status: status || undefined, page, pageSize }),
    refetchOnWindowFocus: true,
    staleTime: 30_000,
    retry: 1,
  })

  return (
    <div>
      <header className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Work Orders</h1>
          <p className="text-sm text-gray-500">
            {data ? `${data.total} ${data.total === 1 ? 'work order' : 'work orders'}` : ' '}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <label className="text-sm text-gray-600" htmlFor="wo-status">
            Status
          </label>
          <select
            id="wo-status"
            value={status}
            onChange={(e) => {
              setStatus(e.target.value)
              setPage(1)
            }}
            className="rounded border border-gray-300 px-2 py-1 text-sm"
          >
            <option value="">All</option>
            <option value="Draft">Draft</option>
            <option value="Sent">Sent</option>
            <option value="Accepted">Accepted</option>
            <option value="Scheduled">Scheduled</option>
            <option value="InProgress">In Progress</option>
            <option value="OnHold">On Hold</option>
            <option value="Completed">Completed</option>
            <option value="Closed">Closed</option>
          </select>
        </div>
      </header>

      {isPending ? (
        <p className="text-gray-500">Loading work orders…</p>
      ) : isError ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4">
          <p className="font-semibold text-red-700">Failed to load work orders</p>
          <p className="mt-1 text-sm text-gray-600">
            {error instanceof Error ? error.message : String(error)}
          </p>
          <button
            onClick={() => void refetch()}
            className="mt-3 rounded bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700"
          >
            Retry
          </button>
        </div>
      ) : data.items.length === 0 ? (
        <p className="text-gray-500">No work orders match this filter.</p>
      ) : (
        <>
          <table className="w-full overflow-hidden rounded border border-gray-200 bg-white text-sm">
            <thead className="bg-gray-50 text-left text-xs uppercase tracking-wider text-gray-500">
              <tr>
                <th className="px-3 py-2">Work Order</th>
                <th className="px-3 py-2">Status</th>
                <th className="px-3 py-2">Vendor</th>
                <th className="px-3 py-2">Scheduled</th>
                <th className="px-3 py-2">Appointment</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {data.items.map((wo) => (
                <tr key={wo.workOrderId} className="hover:bg-gray-50">
                  <td className="px-3 py-2 font-medium">
                    <Link
                      to={`/cockpit/work-orders/${encodeURIComponent(wo.workOrderId)}`}
                      className="text-blue-600 hover:underline"
                    >
                      {wo.workOrderId}
                    </Link>
                  </td>
                  <td className="px-3 py-2">
                    <span className="rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-800">{wo.status}</span>
                  </td>
                  <td className="px-3 py-2 text-gray-700">{wo.vendorId}</td>
                  <td className="px-3 py-2 text-gray-700">{wo.scheduledDate}</td>
                  <td className="px-3 py-2 text-gray-500">
                    {wo.appointmentDate ? new Date(wo.appointmentDate).toLocaleString() : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="mt-3 flex items-center justify-between text-sm text-gray-600">
            <span>
              Page {data.page} · {data.items.length} of {data.total}
            </span>
            <div className="flex gap-2">
              <button
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="rounded border border-gray-300 px-3 py-1 disabled:opacity-50"
              >
                ← Prev
              </button>
              <button
                disabled={page * pageSize >= data.total}
                onClick={() => setPage((p) => p + 1)}
                className="rounded border border-gray-300 px-3 py-1 disabled:opacity-50"
              >
                Next →
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
