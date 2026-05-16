/**
 * ReceiptPDF template — single-page payment receipt rendered with
 * `@react-pdf/renderer`. Phase 1 scaffold.
 *
 * Inspiration (Apache 2.0, attributed in repo NOTICE): Apache OFBiz
 * `accounting/template/payment.fo` structure — header + payment
 * details + allocation table + footer.
 */

import { Document, Page, View, Text, StyleSheet } from '@react-pdf/renderer'
import type { Receipt } from './types'

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
  brand: { width: '60%' },
  brandName: {
    fontSize: 18,
    fontFamily: 'Helvetica-Bold',
    marginBottom: 6,
  },
  brandAddress: { color: '#475569', fontSize: 9 },
  meta: { width: '40%', textAlign: 'right' },
  metaLabel: {
    fontSize: 20,
    fontFamily: 'Helvetica-Bold',
    letterSpacing: 2,
    textTransform: 'uppercase',
    marginBottom: 6,
  },
  metaNumber: { fontFamily: 'Helvetica-Bold', fontSize: 11, marginBottom: 4 },
  metaRow: { fontSize: 9, color: '#475569' },
  partiesRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 24,
  },
  partyBlock: { width: '48%' },
  partyLabel: {
    fontSize: 8,
    fontFamily: 'Helvetica-Bold',
    letterSpacing: 1.2,
    color: '#64748b',
    textTransform: 'uppercase',
    marginBottom: 4,
  },
  partyName: { fontFamily: 'Helvetica-Bold', marginBottom: 2 },
  partyAddressLine: { fontSize: 9, color: '#475569' },
  amountBlock: {
    marginBottom: 24,
    paddingVertical: 16,
    paddingHorizontal: 18,
    backgroundColor: '#f1f5f9',
    border: '0.5pt solid #cbd5e1',
  },
  amountLabel: {
    fontSize: 8,
    fontFamily: 'Helvetica-Bold',
    letterSpacing: 1.2,
    color: '#64748b',
    textTransform: 'uppercase',
    marginBottom: 4,
  },
  amountValue: {
    fontSize: 24,
    fontFamily: 'Helvetica-Bold',
    marginBottom: 6,
  },
  amountMetaRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 12,
    marginTop: 4,
  },
  amountMetaCell: { fontSize: 9, color: '#475569' },
  amountMetaCellStrong: {
    fontSize: 9,
    color: '#0f172a',
    fontFamily: 'Helvetica-Bold',
  },
  sectionHead: {
    fontSize: 8,
    fontFamily: 'Helvetica-Bold',
    letterSpacing: 1.2,
    color: '#64748b',
    textTransform: 'uppercase',
    marginBottom: 6,
  },
  table: { marginBottom: 16 },
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
  cellInv: { width: '40%' },
  cellApplied: { width: '30%', textAlign: 'right' },
  cellBal: { width: '30%', textAlign: 'right' },
  footer: {
    position: 'absolute',
    bottom: 40,
    left: 48, // css-lp:ignore — react-pdf uses physical coords; inset-inline-* unsupported
    right: 48, // css-lp:ignore
    borderTop: '0.5pt solid #e2e8f0',
    paddingTop: 10,
  },
  footerBody: { fontSize: 9, color: '#475569' },
})

function fmtMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount)
  } catch {
    return `$${amount.toFixed(2)}`
  }
}

function fmtDate(iso: string): string {
  const [y, m, d] = iso.split('-').map((n) => parseInt(n, 10))
  const dt = new Date(Date.UTC(y, m - 1, d))
  return dt.toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric', timeZone: 'UTC' })
}

export interface ReceiptTemplateProps {
  receipt: Receipt
}

export function ReceiptTemplate({ receipt }: ReceiptTemplateProps) {
  return (
    <Document
      title={`Receipt ${receipt.receiptNumber}`}
      author={receipt.receiver.name}
      subject={`Receipt for ${receipt.payer.name}`}
    >
      <Page size="LETTER" style={styles.page}>
        {/* ── Header ──────────────────────────────────────────────────── */}
        <View style={styles.header}>
          <View style={styles.brand}>
            <Text style={styles.brandName}>{receipt.receiver.name}</Text>
            {receipt.receiver.address.map((line, i) => (
              <Text key={i} style={styles.brandAddress}>{line}</Text>
            ))}
          </View>
          <View style={styles.meta}>
            <Text style={styles.metaLabel}>Receipt</Text>
            <Text style={styles.metaNumber}>{receipt.receiptNumber}</Text>
            <Text style={styles.metaRow}>Received · {fmtDate(receipt.receiptDate)}</Text>
          </View>
        </View>

        {/* ── Received-from / Received-by ─────────────────────────────── */}
        <View style={styles.partiesRow}>
          <View style={styles.partyBlock}>
            <Text style={styles.partyLabel}>Received from</Text>
            <Text style={styles.partyName}>{receipt.payer.name}</Text>
            {receipt.payer.address.map((line, i) => (
              <Text key={i} style={styles.partyAddressLine}>{line}</Text>
            ))}
          </View>
          <View style={styles.partyBlock}>
            <Text style={styles.partyLabel}>Received by</Text>
            <Text style={styles.partyName}>{receipt.receiver.name}</Text>
            {receipt.receiver.address.map((line, i) => (
              <Text key={i} style={styles.partyAddressLine}>{line}</Text>
            ))}
          </View>
        </View>

        {/* ── Amount block ────────────────────────────────────────────── */}
        <View style={styles.amountBlock}>
          <Text style={styles.amountLabel}>Amount received</Text>
          <Text style={styles.amountValue}>{fmtMoney(receipt.amount, receipt.currency)}</Text>
          <View style={styles.amountMetaRow}>
            <Text style={styles.amountMetaCell}>
              Method · <Text style={styles.amountMetaCellStrong}>{receipt.paymentMethod}</Text>
            </Text>
            {receipt.paymentReference && (
              <Text style={styles.amountMetaCell}>
                Ref · <Text style={styles.amountMetaCellStrong}>{receipt.paymentReference}</Text>
              </Text>
            )}
          </View>
        </View>

        {/* ── Allocation table ────────────────────────────────────────── */}
        {receipt.allocations.length > 0 && (
          <>
            <Text style={styles.sectionHead}>Applied to</Text>
            <View style={styles.table}>
              <View style={styles.tableHeader}>
                <Text style={[styles.tableHeaderCell, styles.cellInv]}>Invoice</Text>
                <Text style={[styles.tableHeaderCell, styles.cellApplied]}>Amount applied</Text>
                <Text style={[styles.tableHeaderCell, styles.cellBal]}>Remaining balance</Text>
              </View>
              {receipt.allocations.map((a) => (
                <View key={a.invoiceNumber} style={styles.tableRow}>
                  <Text style={styles.cellInv}>{a.invoiceNumber}</Text>
                  <Text style={styles.cellApplied}>
                    {fmtMoney(a.amountApplied, receipt.currency)}
                  </Text>
                  <Text style={styles.cellBal}>
                    {fmtMoney(a.remainingBalance, receipt.currency)}
                  </Text>
                </View>
              ))}
            </View>
          </>
        )}

        {/* ── Footer ──────────────────────────────────────────────────── */}
        {receipt.notes && (
          <View style={styles.footer}>
            <Text style={styles.footerBody}>{receipt.notes}</Text>
          </View>
        )}
      </Page>
    </Document>
  )
}
