/**
 * Visual-verification fixtures for the Invoice + Receipt PDF templates.
 *
 * Sender is `Sunfish Properties Sample LLC` — a generic stand-in so this
 * scaffold doesn't embed any real-LLC data. The importer wired through
 * Task C will swap real data in at runtime; for `/internal/reports-
 * preview` (dev-only) we just want representative shape coverage:
 * multi-line items, mixed quantities, tax, partial-payment allocation.
 */

import type { Invoice, Party, Receipt } from './types'

const SAMPLE_SENDER: Party = {
  id: 'party-sender-001',
  name: 'Sunfish Properties Sample LLC',
  address: [
    '12 Quay Street',
    'Suite 4',
    'Portsmouth, NH 03801',
  ],
  contacts: [
    { kind: 'email', value: 'billing@sunfish-sample.example' },
    { kind: 'phone', value: '+1 603 555 0142' },
  ],
}

const SAMPLE_CUSTOMER: Party = {
  id: 'party-customer-001',
  name: 'Atlantic Tenant Holdings, LLC',
  address: [
    '108 Atlantic Ave',
    'Unit 2B',
    'Hampton, NH 03842',
  ],
  contacts: [
    { kind: 'email', value: 'ap@atlantic-tenant.example' },
  ],
}

export const SAMPLE_INVOICE: Invoice = {
  id: 'inv-001',
  invoiceNumber: 'INV-2026-0042',
  sender: SAMPLE_SENDER,
  recipient: SAMPLE_CUSTOMER,
  issueDate: '2026-05-01',
  dueDate: '2026-05-31',
  currency: 'USD',
  lines: [
    {
      id: 'line-001',
      lineNumber: 1,
      description: 'Monthly rent — Unit 2B, 108 Atlantic Ave',
      quantity: 1,
      unitPrice: 2_400.0,
      amount: 2_400.0,
      taxAmount: 0,
    },
    {
      id: 'line-002',
      lineNumber: 2,
      description: 'CAM (common-area maintenance), May 2026',
      quantity: 1,
      unitPrice: 185.0,
      amount: 185.0,
      taxAmount: 0,
    },
    {
      id: 'line-003',
      lineNumber: 3,
      description: 'Snow-removal cost share (Feb–Apr, prorated)',
      quantity: 1,
      unitPrice: 92.5,
      amount: 92.5,
      taxAmount: 0,
    },
    {
      id: 'line-004',
      lineNumber: 4,
      description: 'Late fee — April invoice (5 days overdue)',
      quantity: 1,
      unitPrice: 75.0,
      amount: 75.0,
      taxAmount: 0,
    },
  ],
  subtotal: 2_752.5,
  taxTotal: 0,
  total: 2_752.5,
  amountPaid: 0,
  balance: 2_752.5,
  status: 'Issued',
  termsLabel: 'Net 30',
  notes:
    'Pay by check to the address above, or via ACH to routing 011500120 / acct 0042-1856. ' +
    'Late fees of $25 + 1.5% per month accrue after the due date.',
}

export const SAMPLE_RECEIPT: Receipt = {
  id: 'rcpt-001',
  receiptNumber: 'RCPT-2026-0019',
  receiver: SAMPLE_SENDER,
  payer: SAMPLE_CUSTOMER,
  receiptDate: '2026-05-12',
  amount: 2_752.5,
  currency: 'USD',
  paymentMethod: 'Check',
  paymentReference: 'Check #1184',
  allocations: [
    {
      invoiceNumber: 'INV-2026-0042',
      amountApplied: 2_752.5,
      remainingBalance: 0,
    },
  ],
  notes: 'Thank you. Receipt acknowledges payment in full for the period covered.',
}
