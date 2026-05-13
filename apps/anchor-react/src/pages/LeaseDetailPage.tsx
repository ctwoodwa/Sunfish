import { useParams, Link } from 'react-router-dom'
import { useLease, usePayments } from '@/hooks/useLeases'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

export function LeaseDetailPage() {
  const { name } = useParams<{ name: string }>()
  const { data: lease, isPending: leasePending, isError: leaseError, error: leaseErr } = useLease(name ?? '')
  const { data: allPayments, isPending: paymentsPending } = usePayments()

  if (leasePending) {
    return <div className="flex items-center justify-center h-48 text-gray-500">Loading lease…</div>
  }

  if (leaseError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6">
        <p className="font-semibold text-red-700">Failed to load lease</p>
        <p className="mt-1 text-sm text-gray-600">{leaseErr.message}</p>
        <Link to="/leases" className="mt-3 inline-block text-sm text-blue-600 hover:underline">
          ← Back to leases
        </Link>
      </div>
    )
  }

  if (!lease) return null

  const payments = allPayments?.filter((p) => p.lease === lease.name) ?? []

  return (
    <div className="max-w-2xl">
      <div className="mb-6 flex items-center gap-3">
        <Link to="/leases" className="text-sm text-blue-600 hover:underline">
          ← Leases
        </Link>
        <h1 className="text-2xl font-bold text-gray-900">{lease.tenant}</h1>
        <Badge variant={lease.status === 'Active' ? 'success' : 'outline'}>{lease.status}</Badge>
      </div>

      <Card className="mb-6">
        <CardHeader>
          <CardTitle className="text-base">Lease Details</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-sm">
            <div>
              <dt className="text-gray-500">Property</dt>
              <dd className="font-medium text-gray-900">{lease.property}</dd>
            </div>
            {lease.unit && (
              <div>
                <dt className="text-gray-500">Unit</dt>
                <dd className="font-medium text-gray-900">{lease.unit}</dd>
              </div>
            )}
            <div>
              <dt className="text-gray-500">Start date</dt>
              <dd className="font-medium text-gray-900">{lease.start_date}</dd>
            </div>
            <div>
              <dt className="text-gray-500">End date</dt>
              <dd className="font-medium text-gray-900">{lease.end_date}</dd>
            </div>
            <div>
              <dt className="text-gray-500">Monthly rent</dt>
              <dd className="font-medium text-gray-900">${lease.monthly_rent.toLocaleString()}</dd>
            </div>
          </dl>
          <div className="mt-4">
            <Link
              to={`/rent?lease=${encodeURIComponent(lease.name)}`}
              className="rounded bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700"
            >
              Record payment
            </Link>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Payment history</CardTitle>
        </CardHeader>
        <CardContent>
          {paymentsPending ? (
            <p className="text-sm text-gray-500">Loading payments…</p>
          ) : payments.length === 0 ? (
            <p className="text-sm text-gray-500">No payments recorded for this lease.</p>
          ) : (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b text-left text-gray-500">
                  <th className="pb-2 pr-4 font-medium">Date</th>
                  <th className="pb-2 pr-4 font-medium">Amount</th>
                  <th className="pb-2 pr-4 font-medium">Method</th>
                  <th className="pb-2 font-medium">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {payments.map((p) => (
                  <tr key={p.name}>
                    <td className="py-2 pr-4 text-gray-700">{p.date}</td>
                    <td className="py-2 pr-4 font-medium text-gray-900">${p.amount.toLocaleString()}</td>
                    <td className="py-2 pr-4 text-gray-600">{p.payment_method}</td>
                    <td className="py-2">
                      <Badge variant={p.status === 'Completed' ? 'success' : 'secondary'}>
                        {p.status}
                      </Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
