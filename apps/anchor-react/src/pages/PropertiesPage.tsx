import { useProperties } from '@/hooks/useProperties'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import type { Property } from '@/api/erpnext'

function statusVariant(status: Property['status']) {
  switch (status) {
    case 'Active': return 'success' as const
    case 'Vacant': return 'warning' as const
    case 'Maintenance': return 'secondary' as const
    case 'Sold': return 'outline' as const
  }
}

export function PropertiesPage() {
  const { data: properties, isPending, isError, error, refetch } = useProperties()

  if (isPending) {
    return (
      <div className="flex items-center justify-center h-48 text-gray-500">
        Loading properties…
      </div>
    )
  }

  if (isError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6">
        <p className="font-semibold text-red-700">Failed to load properties</p>
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

  if (!properties?.length) {
    return (
      <div className="flex items-center justify-center h-48 text-gray-500">
        No properties found. Create your first Property record in ERPNext.
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Properties</h1>
        <p className="text-gray-500">{properties.length} properties</p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {properties.map((p) => (
          <Card key={p.name}>
            <CardHeader>
              <div className="flex items-start justify-between gap-2">
                <CardTitle className="text-base">{p.property_name}</CardTitle>
                <Badge variant={statusVariant(p.status)}>{p.status}</Badge>
              </div>
              <CardDescription>
                {[p.address_line_1, p.city, p.state].filter(Boolean).join(', ')}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-gray-500">
                {p.units} {p.units === 1 ? 'unit' : 'units'}
              </p>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
