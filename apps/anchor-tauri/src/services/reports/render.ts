/**
 * PDF rendering wrappers.
 *
 * `renderReportToPdf` returns a Blob suitable for browser download or
 * preview; `renderReportToFile` writes the blob to a path via Tauri's
 * fs plugin. Both are template-agnostic — pass any React element that
 * renders a `<Document>` from `@react-pdf/renderer`.
 *
 * Tauri-side prerequisite: `renderReportToFile` requires
 * `tauri-plugin-fs` to be added to `src-tauri/Cargo.toml` +
 * registered in `src-tauri/src/lib.rs` + a write capability in
 * `src-tauri/capabilities/default.json`. dev-win owns src-tauri/
 * changes per W#60 P4 routing — this scaffold ships the React API
 * shape with a clear pre-flight error so the caller can branch.
 */

import { pdf, type DocumentProps } from '@react-pdf/renderer'
import type { ReactElement } from 'react'

/** A React element that renders a React-PDF `<Document>`. */
export type PdfDocumentElement = ReactElement<DocumentProps>

/** Render a React-PDF document to a Blob (browser + Tauri webview). */
export async function renderReportToPdf(template: PdfDocumentElement): Promise<Blob> {
  return pdf(template).toBlob()
}

function inTauri(): boolean {
  return typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window
}

/**
 * Render a report and write it to a local path via Tauri's plugin-fs.
 * Returns the absolute path written.
 *
 * Throws when:
 *   - not running inside the Tauri webview (use `renderReportToPdf`
 *     + a manual browser download instead);
 *   - the `@tauri-apps/plugin-fs` module isn't installed (dev-win
 *     adds it when `tauri-plugin-fs` lands in src-tauri/).
 */
export async function renderReportToFile(
  template: PdfDocumentElement,
  path: string,
): Promise<string> {
  if (!inTauri()) {
    throw new Error(
      'renderReportToFile requires the Tauri runtime; ' +
        'use renderReportToPdf + a browser download instead.',
    )
  }
  // The JS binding is installed as a regular npm dep, but the Rust
  // plugin (tauri-plugin-fs) must also be registered in src-tauri/ +
  // a write capability granted in capabilities/default.json before
  // writeFile actually succeeds. Until dev-win lands that, the call
  // will throw a Tauri-runtime error at the IPC boundary.
  const { writeFile } = await import('@tauri-apps/plugin-fs')
  const blob = await renderReportToPdf(template)
  const bytes = new Uint8Array(await blob.arrayBuffer())
  await writeFile(path, bytes)
  return path
}
