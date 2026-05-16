import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { getCockpitVendorDetail } from '@/cockpit/api'

/**
 * W#29 Phase 4 — vendor detail view.
 *
 * Shows the vendor profile, contact identifiers, performance log
 * (last 5 events), and work-order history. Contact resolution (display
 * names + emails for each VendorContactId) is deferred until
 * IVendorContactService gains a list-by-vendor accessor.
 */
export function VendorDetailView() {
  const { vendorId } = useParams<{ vendorId: string }>()
  const id = vendorId ?? ''

  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['cockpit-vendor-detail', id],
    queryFn: () => getCockpitVendorDetail(id),
    enabled: id.length > 0,
    refetchOnWindowFocus: true,
    staleTime: 30_000,
    retry: 1,
  })

  if (!id) return <NotFound />
  if (isPending) return <p className="text-gray-500">Loading vendor…</p>

  if (isError) {
    const isNotFound = error instanceof Error && error.message === 'Vendor not found'
    if (isNotFound) return <NotFound />
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4">
        <p className="font-semibold text-red-700">Failed to load vendor</p>
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
    )
  }

  return (
    <div>
      <header className="mb-6">
        <Link to="/cockpit/vendors" className="text-sm text-blue-600 hover:underline">
          ← Back to vendors
        </Link>
        <h1 className="mt-2 text-2xl font-bold text-gray-900">{data.displayName}</h1>
        <p className="text-sm text-gray-500">
          {data.status} · {data.onboardingState} ·{' '}
          {data.specialties.length > 0 ? data.specialties.join(', ') : 'no specialty'}
        </p>
      </header>

      <section className="mb-4 rounded-lg border border-gray-200 bg-white p-4">
        <h2 className="mb-2 text-base font-semibold text-gray-900">Contact</h2>
        <dl className="grid grid-cols-3 gap-2 text-sm">
          <dt className="text-gray-500">Name</dt>
          <dd className="col-span-2 text-gray-900">{data.contactName ?? '—'}</dd>
          <dt className="text-gray-500">Email</dt>
          <dd className="col-span-2 text-gray-900">{data.contactEmail ?? '—'}</dd>
          <dt className="text-gray-500">Phone</dt>
          <dd className="col-span-2 text-gray-900">{data.contactPhone ?? '—'}</dd>
          <dt className="text-gray-500">W-9</dt>
          <dd className="col-span-2 text-gray-900">{data.w9Status}</dd>
        </dl>
      </section>

      <section className="mb-4 rounded-lg border border-gray-200 bg-white p-4">
        <h2 className="mb-2 text-base font-semibold text-gray-900">Performance log (last 5)</h2>
        {data.performanceLog.length === 0 ? (
          <p className="text-sm text-gray-500">No performance records.</p>
        ) : (
          <ul className="divide-y divide-gray-100">
            {data.performanceLog.map((p, i) => (
              <li key={i} className="py-2 text-sm">
                <span className="font-medium">{p.event}</span>
                <span className="ml-2 text-gray-500">
                  {new Date(p.occurredAt).toLocaleString()}
                </span>
                {p.notes && <p className="text-xs text-gray-500">{p.notes}</p>}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="rounded-lg border border-gray-200 bg-white p-4">
        <h2 className="mb-2 text-base font-semibold text-gray-900">Work orders</h2>
        {data.workOrders.length === 0 ? (
          <p className="text-sm text-gray-500">No work orders assigned.</p>
        ) : (
          <ul className="divide-y divide-gray-100">
            {data.workOrders.map((w) => (
              <li key={w.workOrderId} className="py-2 text-sm">
                <Link
                  to={`/cockpit/work-orders/${encodeURIComponent(w.workOrderId)}`}
                  className="font-medium text-blue-600 hover:underline"
                >
                  {w.workOrderId}
                </Link>
                <span className="ml-2 text-gray-500">
                  {w.status} · scheduled {w.scheduledDate}
                  {w.completedDate ? ` · completed ${w.completedDate}` : ''}
                  {w.totalCost !== null ? ` · $${w.totalCost.toFixed(2)}` : ''}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}

function NotFound() {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-8 text-center">
      <h2 className="text-lg font-semibold text-gray-900">Vendor not found</h2>
      <Link
        to="/cockpit/vendors"
        className="mt-4 inline-block text-sm text-blue-600 hover:underline"
      >
        ← Back to vendors
      </Link>
    </div>
  )
}
