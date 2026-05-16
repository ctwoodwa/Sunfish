import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { getCockpitVendors } from '@/cockpit/api'

/**
 * W#29 Phase 4 — vendors list view.
 *
 * Shows the W-9 status, YTD payments, and 1099-readiness flag per vendor.
 * 1099 readiness rule lives in the Bridge endpoint (see VendorsEndpoint.cs).
 */
export function VendorListView() {
  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['cockpit-vendors'],
    queryFn: getCockpitVendors,
    refetchOnWindowFocus: true,
    staleTime: 30_000,
    retry: 1,
  })

  if (isPending) return <p className="text-gray-500">Loading vendors…</p>
  if (isError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4">
        <p className="font-semibold text-red-700">Failed to load vendors</p>
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
        <h1 className="text-2xl font-bold text-gray-900">Vendors</h1>
        <p className="text-sm text-gray-500">
          {data.vendors.length} {data.vendors.length === 1 ? 'vendor' : 'vendors'}
        </p>
      </header>

      {data.vendors.length === 0 ? (
        <p className="text-gray-500">No vendors yet.</p>
      ) : (
        <table className="w-full overflow-hidden rounded border border-gray-200 bg-white text-sm">
          <thead className="bg-gray-50 text-left text-xs uppercase tracking-wider text-gray-500">
            <tr>
              <th className="px-3 py-2">Vendor</th>
              <th className="px-3 py-2">Specialty</th>
              <th className="px-3 py-2">W-9</th>
              <th className="px-3 py-2 text-right">YTD Payments</th>
              <th className="px-3 py-2">1099?</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {data.vendors.map((v) => (
              <tr key={v.vendorId} className="hover:bg-gray-50">
                <td className="px-3 py-2 font-medium">
                  <Link
                    to={`/cockpit/vendors/${encodeURIComponent(v.vendorId)}`}
                    className="text-blue-600 hover:underline"
                  >
                    {v.displayName}
                  </Link>
                  <div className="text-xs text-gray-500">{v.onboardingState}</div>
                </td>
                <td className="px-3 py-2 text-gray-700">
                  {v.specialties.length > 0 ? v.specialties.join(', ') : '—'}
                </td>
                <td className="px-3 py-2">
                  <W9Chip status={v.w9Status} />
                </td>
                <td className="px-3 py-2 text-right text-gray-700">
                  ${v.ytdPayments.toFixed(2)}
                </td>
                <td className="px-3 py-2">
                  {v.needsForm1099 ? (
                    <span
                      className="inline-flex items-center gap-1 rounded bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-800"
                      title="W-9 missing and YTD payments exceed $600"
                    >
                      ⚠ 1099 required
                    </span>
                  ) : (
                    <span className="text-xs text-gray-400">—</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

function W9Chip({ status }: { status: string }) {
  const cls =
    status === 'On file'
      ? 'bg-green-50 text-green-700'
      : status === 'Awaiting'
      ? 'bg-gray-100 text-gray-700'
      : 'bg-amber-50 text-amber-800'
  return <span className={`rounded px-2 py-0.5 text-xs ${cls}`}>{status}</span>
}
