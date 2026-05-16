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
        <h1 className="text-2xl font-semibold text-foreground">Accounting</h1>
        <p className="mt-1 text-sm text-muted-foreground">Year-to-date summary from ERPNext General Ledger</p>
      </div>

      {/* P&L Summary */}
      <section>
        <h2 className="text-lg font-medium text-foreground mb-4">P&amp;L Summary</h2>
        {summary.isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
        {summary.isError && (
          <p className="text-sm text-destructive">
            Could not load accounting summary: {summary.error instanceof Error ? summary.error.message : 'Unknown error'}
          </p>
        )}
        {summary.data && (
          <div className="overflow-hidden rounded-lg border border-border">
            <table className="min-w-full divide-y divide-border">
              <thead className="bg-muted">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    Category
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    {summary.data.period}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border bg-card">
                <tr>
                  <td className="px-6 py-4 text-sm text-foreground">Total Income</td>
                  <td className="px-6 py-4 text-right text-sm font-medium text-financial-positive">
                    {formatCurrency(summary.data.income)}
                  </td>
                </tr>
                <tr>
                  <td className="px-6 py-4 text-sm text-foreground">Total Expenses</td>
                  <td className="px-6 py-4 text-right text-sm font-medium text-financial-negative">
                    {formatCurrency(summary.data.expenses)}
                  </td>
                </tr>
                <tr className="bg-muted">
                  <td className="px-6 py-4 text-sm font-semibold text-foreground">Net Income</td>
                  <td
                    className={`px-6 py-4 text-right text-sm font-semibold ${
                      summary.data.net >= 0 ? 'text-financial-positive' : 'text-financial-negative'
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
        <h2 className="text-lg font-medium text-foreground mb-4">Outstanding Invoices</h2>
        {outstanding.isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
        {outstanding.isError && (
          <p className="text-sm text-destructive">
            Could not load outstanding invoices: {outstanding.error instanceof Error ? outstanding.error.message : 'Unknown error'}
          </p>
        )}
        {outstanding.data && outstanding.data.length === 0 && (
          <p className="text-sm text-muted-foreground">No outstanding invoices.</p>
        )}
        {outstanding.data && outstanding.data.length > 0 && (
          <div className="overflow-hidden rounded-lg border border-border">
            <table className="min-w-full divide-y divide-border">
              <thead className="bg-muted">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wide text-muted-foreground">Customer</th>
                  <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wide text-muted-foreground">Invoice</th>
                  <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wide text-muted-foreground">Due Date</th>
                  <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wide text-muted-foreground">Outstanding</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border bg-card">
                {outstanding.data.map((inv) => (
                  <tr key={inv.name}>
                    <td className="px-6 py-4 text-sm text-foreground">{inv.customer}</td>
                    <td className="px-6 py-4 text-sm text-muted-foreground">{inv.name}</td>
                    <td className="px-6 py-4 text-sm text-muted-foreground">{inv.due_date}</td>
                    <td className="px-6 py-4 text-right text-sm font-medium text-financial-negative">
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
