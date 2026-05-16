/**
 * Dev-only preview surface for the React-PDF templates. Routes at
 * `/internal/reports-preview`, behind an `import.meta.env.DEV` guard
 * so production builds tree-shake it out (no SSR for this app — the
 * route just no-ops in prod).
 *
 * Visual verification only. The buttons round-trip the fixture types
 * through JSON to confirm the entity shapes serialize cleanly, and
 * offer a manual browser download so we can inspect the resulting
 * PDF without the Tauri-fs write path.
 */

import { useMemo, useState } from 'react'
import { PDFViewer } from '@react-pdf/renderer'
import { InvoiceTemplate } from '@/services/reports/InvoiceTemplate'
import { ReceiptTemplate } from '@/services/reports/ReceiptTemplate'
import { renderReportToPdf } from '@/services/reports/render'
import { SAMPLE_INVOICE, SAMPLE_RECEIPT } from '@/services/reports/fixtures'
import type { Invoice, Receipt } from '@/services/reports/types'

type Which = 'invoice' | 'receipt'

function roundTrip<T>(v: T): T {
  return JSON.parse(JSON.stringify(v)) as T
}

export function InternalReportsPreviewPage() {
  const [which, setWhich] = useState<Which>('invoice')

  // Round-trip the fixtures through JSON so the preview also serves as
  // a smoke test that the entity shapes are serialization-safe.
  const invoice = useMemo<Invoice>(() => roundTrip(SAMPLE_INVOICE), [])
  const receipt = useMemo<Receipt>(() => roundTrip(SAMPLE_RECEIPT), [])

  const handleDownload = async () => {
    const tpl =
      which === 'invoice'
        ? <InvoiceTemplate invoice={invoice} />
        : <ReceiptTemplate receipt={receipt} />
    const blob = await renderReportToPdf(tpl)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download =
      which === 'invoice'
        ? `${invoice.invoiceNumber}.pdf`
        : `${receipt.receiptNumber}.pdf`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Reports preview (dev-only)</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Renders the React-PDF Invoice + Receipt scaffolds against
          fixture data. Visual verification surface for the
          <code className="ml-1 rounded bg-muted px-1 py-0.5">{'services/reports/'}</code>
          templates; not shipped in production builds.
        </p>
      </div>

      <div className="flex items-center gap-3">
        <div className="inline-flex rounded-md border border-border bg-muted p-1 text-sm">
          <button
            type="button"
            onClick={() => setWhich('invoice')}
            className={`rounded px-3 py-1 ${
              which === 'invoice' ? 'bg-background font-medium text-foreground shadow-sm' : 'text-muted-foreground'
            }`}
          >
            Invoice
          </button>
          <button
            type="button"
            onClick={() => setWhich('receipt')}
            className={`rounded px-3 py-1 ${
              which === 'receipt' ? 'bg-background font-medium text-foreground shadow-sm' : 'text-muted-foreground'
            }`}
          >
            Receipt
          </button>
        </div>
        <button
          type="button"
          onClick={handleDownload}
          className="rounded bg-primary px-3 py-1.5 text-sm text-primary-foreground hover:bg-primary/90"
        >
          Download PDF
        </button>
      </div>

      <div className="h-[80vh] overflow-hidden rounded-lg border border-border">
        <PDFViewer width="100%" height="100%" showToolbar style={{ border: 0 }}>
          {which === 'invoice'
            ? <InvoiceTemplate invoice={invoice} />
            : <ReceiptTemplate receipt={receipt} />}
        </PDFViewer>
      </div>
    </div>
  )
}
