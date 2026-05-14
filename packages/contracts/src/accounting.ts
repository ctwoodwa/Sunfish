/**
 * Accounting domain contracts.
 * Mirrors ERPNext General Ledger + Journal Entry + Bank Transaction shapes
 * as exposed by the Bridge `/api/v1/erpnext/accounting/*` endpoints (W#60 Phase 2).
 */

export type ReconciliationStatus = 'Unreconciled' | 'Reconciled' | 'Partial' | 'Excluded'

/** One income or expense line item in a Profit & Loss report. */
export interface PLLineItem {
  account: string
  amount: number
}

/**
 * Year-to-date (or period) P&L summary.
 * Returned by Bridge `GET /api/v1/erpnext/accounting/summary`.
 */
export interface PLSummary {
  period: string
  income: number
  expenses: number
  net: number
  incomeLines?: PLLineItem[]
  expenseLines?: PLLineItem[]
}

/**
 * A single General Ledger entry.
 * Mirrors ERPNext GL Entry doctype fields.
 */
export interface LedgerEntry {
  name: string
  date: string
  account: string
  debit: number
  credit: number
  balance: number
  voucher?: string
  remarks?: string
}

/** ERPNext Journal Entry (multi-leg accounting transaction). */
export interface JournalEntry {
  name: string
  date: string
  title: string
  debitTotal: number
  creditTotal: number
  status: 'Draft' | 'Submitted' | 'Cancelled'
  entries: LedgerEntry[]
}

/** Bank statement transaction pending or completed reconciliation. */
export interface BankTransaction {
  name: string
  date: string
  description: string
  amount: number
  currency: string
  reconciliationStatus: ReconciliationStatus
  matchedJournalEntry?: string
}

/** Outstanding invoice returned by Bridge `GET /api/v1/erpnext/accounting/outstanding`. */
export interface OutstandingInvoice {
  name: string
  customer: string
  outstandingAmount: number
  dueDate: string
  status: string
}
