---
uid: library-spread-processing-import-export
title: Spread Processing — Import and Export
description: Aspirational library doc — format import / export surface (XLSX, CSV, PDF) against the ADR 0021 reporting pipeline.
keywords:
  - import export
  - XLSX
  - CSV
  - PDF
  - aspirational
  - ADR 0021
---

# Spread Processing — Import and Export

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0021-reporting-pipeline-policy.md) for the reporting pipeline policy.

## Overview

Import and export cover the set of file formats a spread-processing library can
read and write. The target surface is:

| Format | Extension | Read | Write | Notes |
|---|---|---|---|---|
| **Office Open XML** | `.xlsx` | yes | yes | Primary modern format; lossless round-trip. |
| **CSV** | `.csv` | yes | yes | Configurable delimiter, quote character, and encoding. Loses formatting and formulas; preserves values. |
| **PDF** | `.pdf` | no | yes | Export-only; renders the active worksheet(s) to PDF with print-layout options. |
| **Legacy Excel** | `.xls` | yes | yes (optional) | BIFF8 binary format; round-trip is best-effort for modern features. |

Per-format controls:

- **XLSX** — configurable flags for preserving macros (`.xlsm`), precalculating
  formulas on save, and embedding images.
- **CSV** — delimiter (`,` / `;` / `\t`), quote style, line ending, UTF-8 BOM
  toggle, date-format string, and number culture.
- **PDF** — page size, orientation, margins, scaling (fit-to-page), print area,
  page-break honoring, and PDF/A compliance for archival output.

## Telerik parity reference

- Telerik package: `Telerik.Documents.Spreadsheet` (plus
  `Telerik.Documents.SpreadsheetStreaming` for large-data scenarios; see
  [Spread Stream Processing](../spread-stream-processing/overview.md).)
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radspreadprocessing/formats-and-conversion/general-information>

## Sunfish plans

No Sunfish equivalent exists today. [ADR 0021](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0021-reporting-pipeline-policy.md)
defines the format-contract surface:

- **XLSX** — `IXlsxExportWriter` (default: `Sunfish.Reporting.ClosedXml`).
- **CSV** — `ICsvExportWriter` (default: `Sunfish.Reporting.CsvHelper`).
- **PDF** — `IPdfExportWriter` (default: `Sunfish.Reporting.PdfSharp`; commercial
  alternatives include QuestPDF, Telerik, Syncfusion, Aspose).

All three surfaces are **export-only** on the current contract set. An
`IXlsxImportReader` and `ICsvImportReader` — read-side contracts that materialize
a document model or stream rows into the bundle — are in the deferred second-phase
scope of the reporting work. Bundle authors that need to **read** spreadsheets
today are expected to use the chosen adapter's native reading API directly.

## Related

- [Overview](overview.md)
- [Formulas](formulas.md)
- [Styling](styling.md)
- [Templates](templates.md)
- [Spread Stream Processing — Overview](../spread-stream-processing/overview.md)
