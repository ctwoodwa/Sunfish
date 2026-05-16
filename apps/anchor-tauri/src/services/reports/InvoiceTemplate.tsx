/**
 * InvoicePDF template — single-page A4-ish invoice rendered with
 * `@react-pdf/renderer`. Phase 1 scaffold; final shape ships when the
 * design system + real data importer land.
 *
 * Inspiration (Apache 2.0, attributed in repo NOTICE): Apache OFBiz
 * `accounting/template/invoice.fo` — header + bill-to + line-items +
 * totals + footer layout, adapted to the Anchor visual style.
 */

import { Document, Page, View, Text, StyleSheet } from '@react-pdf/renderer'
import type { Invoice } from './types'

const styles = StyleSheet.create({
  page: {
    backgroundColor: '#ffffff',
    color: '#0f172a',
    padding: 48,
    fontSize: 10,
    fontFamily: 'Helvetica',
    lineHeight: 1.4,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 28,
    borderBottom: '1pt solid #e2e8f0',
    paddingBottom: 16,
  },
  brand: {
    width: '60%',
  },
  brandName: {
    fontSize: 18,
    fontFamily: 'Helvetica-Bold',
    marginBottom: 6,
  },
  brandAddress: {
    color: '#475569',
    fontSize: 9,
  },
  invoiceMeta: {
    width: '40%',
    textAlign: 'right',
  },
  invoiceLabel: {
    fontSize: 20,
    fontFamily: 'Helvetica-Bold',
    letterSpacing: 2,
    textTransform: 'uppercase',
    marginBottom: 6,
  },
  invoiceNumber: {
    fontFamily: 'Helvetica-Bold',
    fontSize: 11,
    marginBottom: 4,
  },
  metaRow: {
    fontSize: 9,
    color: '#475569',
  },
  partiesRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 24,
  },
  partyBlock: {
    width: '48%',
  },
  partyLabel: {
    fontSize: 8,
    fontFamily: 'Helvetica-Bold',
    letterSpacing: 1.2,
    color: '#64748b',
    textTransform: 'uppercase',
    marginBottom: 4,
  },
  partyName: {
    fontFamily: 'Helvetica-Bold',
    marginBottom: 2,
  },
  partyAddressLine: {
    fontSize: 9,
    color: '#475569',
  },
  partyContact: {
    marginTop: 4,
    fontSize: 9,
    color: '#475569',
  },
  table: {
    marginBottom: 16,
  },
  tableHeader: {
    flexDirection: 'row',
    backgroundColor: '#f1f5f9',
    paddingVertical: 6,
    paddingHorizontal: 8,
    borderTop: '1pt solid #e2e8f0',
    borderBottom: '1pt solid #e2e8f0',
  },
  tableHeaderCell: {
    fontSize: 8,
    fontFamily: 'Helvetica-Bold',
    letterSpacing: 0.8,
    textTransform: 'uppercase',
    color: '#64748b',
  },
  tableRow: {
    flexDirection: 'row',
    paddingVertical: 6,
    paddingHorizontal: 8,
    borderBottom: '0.5pt solid #e2e8f0',
  },
  cellLine: { width: '6%', color: '#94a3b8' },
  cellDesc: { width: '54%' },
  cellQty: { width: '10%', textAlign: 'right' },
  cellUnit: { width: '15%', textAlign: 'right' },
  cellAmt: { width: '15%', textAlign: 'right' },
  totalsRow: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    marginTop: 12,
  },
  totalsBlock: {
    width: '40%',
  },
  totalLine: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 3,
  },
  totalLabel: {
    color: '#475569',
  },
  totalValue: {
    fontFamily: 'Helvetica',
  },
  grandTotalLine: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 8,
    marginTop: 4,
    borderTop: '1pt solid #0f172a',
    borderBottom: '2pt solid #0f172a',
  },
  grandTotalLabel: {
    fontFamily: 'Helvetica-Bold',
    fontSize: 11,
    letterSpacing: 0.8,
    textTransform: 'uppercase',
  },
  grandTotalValue: {
    fontFamily: 'Helvetica-Bold',
    fontSize: 11,
  },
  footer: {
    position: 'absolute',
    bottom: 40,
    left: 48, // css-lp:ignore — react-pdf uses physical coords; inset-inline-* unsupported
    right: 48, // css-lp:ignore
    borderTop: '0.5pt solid #e2e8f0',
    paddingTop: 10,
  },
  footerHead: {
    fontSize: 8,
    fontFamily: 'Helvetica-Bold',
    letterSpacing: 1.2,
    textTransform: 'uppercase',
    color: '#64748b',
    marginBottom: 4,
  },
  footerBody: {
    fontSize: 9,
    color: '#475569',
  },
  status: {
    paddingHorizontal: 6,
    paddingVertical: 2,
    fontSize: 8,
    fontFamily: 'Helvetica-Bold',
    letterSpacing: 0.8,
    textTransform: 'uppercase',
  },
})

function fmtMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount)
  } catch {
    return `$${amount.toFixed(2)}`
  }
}

function fmtDate(iso: string): string {
  // ISO 'YYYY-MM-DD' -> 'May 1, 2026'. Keep the parser narrow so we
  // don't surprise on TZ-shifted Date construction.
  const [y, m, d] = iso.split('-').map((n) => parseInt(n, 10))
  const dt = new Date(Date.UTC(y, m - 1, d))
  return dt.toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric', timeZone: 'UTC' })
}

export interface InvoiceTemplateProps {
  invoice: Invoice
}

export function InvoiceTemplate({ invoice }: InvoiceTemplateProps) {
  return (
    <Document
      title={`Invoice ${invoice.invoiceNumber}`}
      author={invoice.sender.name}
      subject={`Invoice for ${invoice.recipient.name}`}
    >
      <Page size="LETTER" style={styles.page}>
        {/* ── Header ──────────────────────────────────────────────────── */}
        <View style={styles.header}>
          <View style={styles.brand}>
            <Text style={styles.brandName}>{invoice.sender.name}</Text>
            {invoice.sender.address.map((line, i) => (
              <Text key={i} style={styles.brandAddress}>{line}</Text>
            ))}
            {invoice.sender.contacts?.map((c, i) => (
              <Text key={`s-${i}`} style={styles.brandAddress}>{c.value}</Text>
            ))}
          </View>
          <View style={styles.invoiceMeta}>
            <Text style={styles.invoiceLabel}>Invoice</Text>
            <Text style={styles.invoiceNumber}>{invoice.invoiceNumber}</Text>
            <Text style={styles.metaRow}>Issued · {fmtDate(invoice.issueDate)}</Text>
            <Text style={styles.metaRow}>Due · {fmtDate(invoice.dueDate)}</Text>
          </View>
        </View>

        {/* ── Bill-to + sender contact ────────────────────────────────── */}
        <View style={styles.partiesRow}>
          <View style={styles.partyBlock}>
            <Text style={styles.partyLabel}>Bill to</Text>
            <Text style={styles.partyName}>{invoice.recipient.name}</Text>
            {invoice.recipient.address.map((line, i) => (
              <Text key={i} style={styles.partyAddressLine}>{line}</Text>
            ))}
            {invoice.recipient.contacts?.map((c, i) => (
              <Text key={`r-${i}`} style={styles.partyContact}>{c.value}</Text>
            ))}
          </View>
          <View style={styles.partyBlock}>
            <Text style={styles.partyLabel}>Status</Text>
            <Text style={styles.status}>{invoice.status}</Text>
          </View>
        </View>

        {/* ── Line items table ────────────────────────────────────────── */}
        <View style={styles.table}>
          <View style={styles.tableHeader}>
            <Text style={[styles.tableHeaderCell, styles.cellLine]}>#</Text>
            <Text style={[styles.tableHeaderCell, styles.cellDesc]}>Description</Text>
            <Text style={[styles.tableHeaderCell, styles.cellQty]}>Qty</Text>
            <Text style={[styles.tableHeaderCell, styles.cellUnit]}>Unit price</Text>
            <Text style={[styles.tableHeaderCell, styles.cellAmt]}>Amount</Text>
          </View>
          {invoice.lines.map((line) => (
            <View key={line.id} style={styles.tableRow}>
              <Text style={styles.cellLine}>{line.lineNumber}</Text>
              <Text style={styles.cellDesc}>{line.description}</Text>
              <Text style={styles.cellQty}>{line.quantity}</Text>
              <Text style={styles.cellUnit}>{fmtMoney(line.unitPrice, invoice.currency)}</Text>
              <Text style={styles.cellAmt}>{fmtMoney(line.amount, invoice.currency)}</Text>
            </View>
          ))}
        </View>

        {/* ── Totals ──────────────────────────────────────────────────── */}
        <View style={styles.totalsRow}>
          <View style={styles.totalsBlock}>
            <View style={styles.totalLine}>
              <Text style={styles.totalLabel}>Subtotal</Text>
              <Text style={styles.totalValue}>{fmtMoney(invoice.subtotal, invoice.currency)}</Text>
            </View>
            {invoice.taxTotal > 0 && (
              <View style={styles.totalLine}>
                <Text style={styles.totalLabel}>Tax</Text>
                <Text style={styles.totalValue}>{fmtMoney(invoice.taxTotal, invoice.currency)}</Text>
              </View>
            )}
            <View style={styles.grandTotalLine}>
              <Text style={styles.grandTotalLabel}>Total due</Text>
              <Text style={styles.grandTotalValue}>{fmtMoney(invoice.balance, invoice.currency)}</Text>
            </View>
            {invoice.amountPaid > 0 && (
              <View style={styles.totalLine}>
                <Text style={styles.totalLabel}>Paid to date</Text>
                <Text style={styles.totalValue}>{fmtMoney(invoice.amountPaid, invoice.currency)}</Text>
              </View>
            )}
          </View>
        </View>

        {/* ── Footer — payment terms + notes ──────────────────────────── */}
        <View style={styles.footer}>
          {invoice.termsLabel && (
            <>
              <Text style={styles.footerHead}>Terms</Text>
              <Text style={styles.footerBody}>{invoice.termsLabel}</Text>
            </>
          )}
          {invoice.notes && (
            <>
              <Text style={[styles.footerHead, { marginTop: 8 }]}>Notes</Text>
              <Text style={styles.footerBody}>{invoice.notes}</Text>
            </>
          )}
        </View>
      </Page>
    </Document>
  )
}
