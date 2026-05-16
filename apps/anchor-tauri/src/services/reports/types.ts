/**
 * Minimal Invoice + Receipt models for Phase 1 PDF rendering.
 *
 * These mirror the canonical shapes in
 * `icm/02_architecture/blocks-financial-schema-design.md` §3.5 (Invoice)
 * and §3.11 (Receipt), reduced to what the InvoicePDF / ReceiptPDF
 * templates consume. Proper shared types — with brands like InvoiceId,
 * full state machines, journal-entry FKs, etc. — land via Task C once
 * the cross-cluster TS-derivation conventions are settled. Until then,
 * this file is the local source of truth for `services/reports/`.
 *
 * Numeric amounts use plain `number` (USD minor units rounded at the
 * call site). When the canonical `Money` type lands we'll swap.
 */

export type ISODate = string

/** Party = customer or vendor reference. Simplified from §3 Party.  */
export interface Party {
  /** Stable internal id (UUID/ULID). */
  id: string
  /** Display name on the invoice/receipt. */
  name: string
  /** Multi-line postal address. Render line-by-line. */
  address: string[]
  /** Optional contact-channel labels (email, phone). */
  contacts?: ReadonlyArray<{ kind: 'email' | 'phone' | 'website'; value: string }>
}

/** One row on an invoice — quantity x unit price, optionally with tax. */
export interface InvoiceLineItem {
  /** Stable internal id (UUID/ULID). */
  id: string
  /** 1-based line number for display + audit ordering. */
  lineNumber: number
  description: string
  /** Permits decimal quantities (e.g. 1.5 hours). */
  quantity: number
  /** USD; minor-unit precision (will become `Money` later). */
  unitPrice: number
  /** quantity * unitPrice, rounded at write time. */
  amount: number
  /** Optional pre-resolved tax amount for the line. */
  taxAmount?: number
}

export type InvoiceStatus =
  | 'Draft'
  | 'Issued'
  | 'PartiallyPaid'
  | 'Paid'
  | 'Overdue'
  | 'Voided'
  | 'WrittenOff'

export interface Invoice {
  id: string
  /** Human-readable; unique within the issuing org. */
  invoiceNumber: string
  /** Issuing entity — the sender ("From"). */
  sender: Party
  /** Receiving entity — the bill-to ("To"). */
  recipient: Party
  issueDate: ISODate
  dueDate: ISODate
  /** ISO 4217 (e.g. "USD"). Phase 1 renderer assumes USD formatting. */
  currency: string
  lines: ReadonlyArray<InvoiceLineItem>
  /** Sum of line amounts (pre-tax). */
  subtotal: number
  /** Sum of line tax amounts. */
  taxTotal: number
  /** subtotal + taxTotal. */
  total: number
  /** Running total of applied payments. */
  amountPaid: number
  /** total - amountPaid. */
  balance: number
  status: InvoiceStatus
  /** Net-N terms label (e.g. "Net 30") or a custom string. */
  termsLabel: string | null
  /** Free-form footer note (payment instructions, thanks, etc.). */
  notes: string | null
}

/** One row in a payment-application list on a receipt. */
export interface ReceiptAllocation {
  /** invoiceNumber for display traceability (FK to Invoice.invoiceNumber). */
  invoiceNumber: string
  /** Portion of the payment applied to this invoice. */
  amountApplied: number
  /** Snapshot of the remaining balance after this allocation. */
  remainingBalance: number
}

export type PaymentMethod =
  | 'Cash'
  | 'Check'
  | 'ACH'
  | 'Wire'
  | 'Card'
  | 'Other'

export interface Receipt {
  id: string
  /** Human-readable; sequential within issuing org. */
  receiptNumber: string
  /** "Received by" — the org accepting payment. */
  receiver: Party
  /** "Received from" — the party making payment. */
  payer: Party
  receiptDate: ISODate
  amount: number
  currency: string
  paymentMethod: PaymentMethod
  /** Check number, wire confirmation, etc. */
  paymentReference: string | null
  /** Which invoices this payment was applied against. May be empty
      (e.g. an upfront deposit not yet matched to an invoice). */
  allocations: ReadonlyArray<ReceiptAllocation>
  notes: string | null
}
