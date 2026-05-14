import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getProperties, getProfitLoss, exportProfitLoss } from '@/api/erpnext'

function fmt(n: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)
}

export function PLReport() {
  const [propertyId, setPropertyId] = useState<string>('')
  const [period, setPeriod] = useState<string>('year')
  const [asOf, setAsOf] = useState<string>(new Date().toISOString().slice(0, 10))
  const [exporting, setExporting] = useState(false)

  const propertiesQuery = useQuery({
    queryKey: ['properties'],
    queryFn: getProperties,
    staleTime: 10 * 60 * 1000,
  })

  const plQuery = useQuery({
    queryKey: ['reports', 'profit-loss', propertyId, period, asOf],
    queryFn: () => getProfitLoss(propertyId || undefined, period, asOf),
    staleTime: 5 * 60 * 1000,
  })

  async function handleExport() {
    setExporting(true)
    try {
      await exportProfitLoss(propertyId || undefined, period, asOf)
    } catch (err) {
      console.error('Export failed:', err)
    } finally {
      setExporting(false)
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Profit &amp; Loss</h1>
          <p className="mt-1 text-sm text-gray-500">Income and expenses from ERPNext General Ledger</p>
        </div>
        <button
          onClick={handleExport}
          disabled={exporting || plQuery.isLoading}
          className="rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
        >
          {exporting ? 'Exporting…' : 'Export CSV'}
        </button>
      </div>

      {/* Selectors */}
      <div className="flex flex-wrap gap-4">
        <div className="flex flex-col gap-1">
          <label htmlFor="pl-property" className="text-xs font-medium text-gray-500">Property</label>
          <select
            id="pl-property"
            value={propertyId}
            onChange={(e) => setPropertyId(e.target.value)}
            className="rounded border border-gray-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">All properties</option>
            {propertiesQuery.data?.map((p) => (
              <option key={p.name} value={p.name}>{p.property_name || p.name}</option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="pl-period" className="text-xs font-medium text-gray-500">Period</label>
          <select
            id="pl-period"
            value={period}
            onChange={(e) => setPeriod(e.target.value)}
            className="rounded border border-gray-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="month">Month to date</option>
            <option value="quarter">Quarter to date</option>
            <option value="year">Year to date</option>
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="pl-asof" className="text-xs font-medium text-gray-500">As of</label>
          <input
            id="pl-asof"
            type="date"
            value={asOf}
            onChange={(e) => setAsOf(e.target.value)}
            className="rounded border border-gray-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
      </div>

      {plQuery.isLoading && <p className="text-sm text-gray-500">Loading…</p>}

      {plQuery.isError && (
        <p className="text-sm text-red-600">
          Could not load P&amp;L report: {(plQuery.error as Error).message}
        </p>
      )}

      {plQuery.data && (
        <div className="space-y-6">
          {/* Summary */}
          <div className="grid grid-cols-3 gap-4">
            <div className="rounded-lg border border-gray-200 p-4">
              <p className="text-xs font-medium text-gray-500">Total Income</p>
              <p className="mt-1 text-xl font-semibold text-green-700">{fmt(plQuery.data.income)}</p>
            </div>
            <div className="rounded-lg border border-gray-200 p-4">
              <p className="text-xs font-medium text-gray-500">Total Expenses</p>
              <p className="mt-1 text-xl font-semibold text-red-700">{fmt(plQuery.data.expenses)}</p>
            </div>
            <div className="rounded-lg border border-gray-200 p-4">
              <p className="text-xs font-medium text-gray-500">Net Income</p>
              <p className={`mt-1 text-xl font-semibold ${plQuery.data.net >= 0 ? 'text-gray-900' : 'text-red-700'}`}>
                {fmt(plQuery.data.net)}
              </p>
            </div>
          </div>

          {/* Income lines */}
          {plQuery.data.incomeLines.length > 0 && (
            <div>
              <h2 className="text-sm font-semibold text-gray-700 mb-2">Income</h2>
              <div className="rounded-lg border border-gray-200 overflow-hidden">
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <tbody className="bg-white divide-y divide-gray-50">
                    {plQuery.data.incomeLines.map((line) => (
                      <tr key={line.account}>
                        <td className="px-4 py-2 text-gray-700">{line.account}</td>
                        <td className="px-4 py-2 text-right font-medium text-green-700">{fmt(line.amount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Expense lines */}
          {plQuery.data.expenseLines.length > 0 && (
            <div>
              <h2 className="text-sm font-semibold text-gray-700 mb-2">Expenses</h2>
              <div className="rounded-lg border border-gray-200 overflow-hidden">
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <tbody className="bg-white divide-y divide-gray-50">
                    {plQuery.data.expenseLines.map((line) => (
                      <tr key={line.account}>
                        <td className="px-4 py-2 text-gray-700">{line.account}</td>
                        <td className="px-4 py-2 text-right font-medium text-red-700">{fmt(line.amount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
