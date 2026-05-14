import { useQuery } from '@tanstack/react-query'
import { getAccountingSummary, getAccountingOutstanding } from '@/api/erpnext'

function formatCurrency(amount: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount)
}

export function AccountingPage() {
  const summary = useQuery({
    queryKey: ['accounting', 'summary'],
    queryFn: getAccountingSummary,
    staleTime: 5 * 60 * 1000,
  })

  const outstanding = useQuery({
    queryKey: ['accounting', 'outstanding'],
    queryFn: getAccountingOutstanding,
    staleTime: 5 * 60 * 1000,
  })

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900">Accounting</h1>
        <p className="mt-1 text-sm text-gray-500">Year-to-date summary from ERPNext General Ledger</p>
      </div>

      {/* P&L Summary */}
      <section>
        <h2 className="text-lg font-medium text-gray-900 mb-4">P&amp;L Summary</h2>
        {summary.isLoading && <p className="text-sm text-gray-500">Loading…</p>}
        {summary.isError && (
          <p className="text-sm text-red-600">
            Could not load accounting summary: {summary.error instanceof Error ? summary.error.message : 'Unknown error'}
          </p>
        )}
        {summary.data && (
          <div className="overflow-hidden rounded-lg border border-gray-200">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wide text-gray-500">
                    Category
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wide text-gray-500">
                    {summary.data.period}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                <tr>
                  <td className="px-6 py-4 text-sm text-gray-900">Total Income</td>
                  <td className="px-6 py-4 text-right text-sm font-medium text-green-700">
                    {formatCurrency(summary.data.income)}
                  </td>
                </tr>
                <tr>
                  <td className="px-6 py-4 text-sm text-gray-900">Total Expenses</td>
                  <td className="px-6 py-4 text-right text-sm font-medium text-red-700">
                    {formatCurrency(summary.data.expenses)}
                  </td>
                </tr>
                <tr className="bg-gray-50">
                  <td className="px-6 py-4 text-sm font-semibold text-gray-900">Net Income</td>
                  <td
                    className={`px-6 py-4 text-right text-sm font-semibold ${
                      summary.data.net >= 0 ? 'text-green-700' : 'text-red-700'
                    }`}
                  >
                    {formatCurrency(summary.data.net)}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Outstanding Invoices */}
      <section>
        <h2 className="text-lg font-medium text-gray-900 mb-4">Outstanding Invoices</h2>
        {outstanding.isLoading && <p className="text-sm text-gray-500">Loading…</p>}
        {outstanding.isError && (
          <p className="text-sm text-red-600">
            Could not load outstanding invoices: {outstanding.error instanceof Error ? outstanding.error.message : 'Unknown error'}
          </p>
        )}
        {outstanding.data && outstanding.data.length === 0 && (
          <p className="text-sm text-gray-500">No outstanding invoices.</p>
        )}
        {outstanding.data && outstanding.data.length > 0 && (
          <div className="overflow-hidden rounded-lg border border-gray-200">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wide text-gray-500">Customer</th>
                  <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wide text-gray-500">Invoice</th>
                  <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wide text-gray-500">Due Date</th>
                  <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wide text-gray-500">Outstanding</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {outstanding.data.map((inv) => (
                  <tr key={inv.name}>
                    <td className="px-6 py-4 text-sm text-gray-900">{inv.customer}</td>
                    <td className="px-6 py-4 text-sm text-gray-500">{inv.name}</td>
                    <td className="px-6 py-4 text-sm text-gray-500">{inv.due_date}</td>
                    <td className="px-6 py-4 text-right text-sm font-medium text-red-700">
                      {formatCurrency(inv.outstanding_amount)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
