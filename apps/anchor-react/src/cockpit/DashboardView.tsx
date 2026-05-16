import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { getCockpitDashboard } from '@/cockpit/api'

/**
 * W#29 Phase 5 — per-property dashboard.
 *
 * Four widgets per hand-off: vacancy rate, 30/60/90-day renewal radar,
 * work-order status rollup (Open / InProgress / Blocked), and overdue
 * inspections list (units with no inspection in 12+ months).
 */
export function DashboardView() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const id = propertyId ?? ''

  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['cockpit-dashboard', id],
    queryFn: () => getCockpitDashboard(id),
    enabled: id.length > 0,
    refetchOnWindowFocus: true,
    staleTime: 30_000,
    retry: 1,
  })

  if (!id) return <NotFound />
  if (isPending) return <p className="text-gray-500">Loading dashboard…</p>
  if (isError) {
    const isNotFound = error instanceof Error && error.message === 'Property not found'
    if (isNotFound) return <NotFound />
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4">
        <p className="font-semibold text-red-700">Failed to load dashboard</p>
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

  const vacancyRate =
    data.totalUnits > 0 ? (data.vacantUnits / data.totalUnits) * 100 : 0

  return (
    <div>
      <header className="mb-6">
        <Link to={`/cockpit/${encodeURIComponent(id)}`} className="text-sm text-blue-600 hover:underline">
          ← Back to property
        </Link>
        <h1 className="mt-2 text-2xl font-bold text-gray-900">Dashboard</h1>
        <p className="text-sm text-gray-500">{id}</p>
      </header>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Card title="Vacancy">
          <p className="text-2xl font-semibold text-gray-900">{vacancyRate.toFixed(0)}%</p>
          <p className="text-xs text-gray-500">
            {data.vacantUnits} of {data.totalUnits} {data.totalUnits === 1 ? 'unit' : 'units'} vacant
          </p>
        </Card>

        <Card title="Work orders">
          <p className="text-2xl font-semibold text-gray-900">
            {data.workOrders.open + data.workOrders.inProgress + data.workOrders.blocked}
          </p>
          <p className="text-xs text-gray-500">
            {data.workOrders.open} open · {data.workOrders.inProgress} in progress · {data.workOrders.blocked} blocked
          </p>
        </Card>

        <Card title="Overdue inspections">
          <p className="text-2xl font-semibold text-gray-900">{data.overdueInspectionUnitIds.length}</p>
          <p className="text-xs text-gray-500">
            unit{data.overdueInspectionUnitIds.length === 1 ? '' : 's'} not inspected in 12+ months
          </p>
        </Card>

        <Card title="Renewals (90 days)">
          <p className="text-2xl font-semibold text-gray-900">
            {data.upcomingRenewals.reduce((sum, b) => sum + b.count, 0)}
          </p>
          <p className="text-xs text-gray-500">
            {data.upcomingRenewals.map((b) => `${b.count}≤${b.withinDays}d`).join(' · ')}
          </p>
        </Card>
      </div>

      {data.overdueInspectionUnitIds.length > 0 && (
        <section className="mt-6 rounded-lg border border-amber-200 bg-amber-50 p-4">
          <h2 className="mb-2 text-base font-semibold text-amber-900">Overdue inspection units</h2>
          <ul className="space-y-1 font-mono text-xs text-amber-900">
            {data.overdueInspectionUnitIds.map((u) => (
              <li key={u}>{u}</li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <p className="text-xs font-medium uppercase tracking-wider text-gray-500">{title}</p>
      <div className="mt-2">{children}</div>
    </div>
  )
}

function NotFound() {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-8 text-center">
      <h2 className="text-lg font-semibold text-gray-900">Property not found</h2>
      <Link to="/cockpit" className="mt-4 inline-block text-sm text-blue-600 hover:underline">
        ← Back to cockpit
      </Link>
    </div>
  )
}
