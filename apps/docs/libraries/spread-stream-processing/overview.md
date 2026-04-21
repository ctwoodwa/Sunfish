---
uid: library-spread-stream-processing-overview
title: Spread Stream Processing — Overview
description: Aspirational library doc — streaming XLSX writer capability target against the ADR 0021 reporting pipeline.
keywords:
  - spread stream processing
  - streaming XLSX
  - large data export
  - OpenXML
  - aspirational
  - ADR 0021
---

# Spread Stream Processing — Overview

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](xref:adr-0021-reporting-pipeline-policy) for the reporting pipeline policy.

## Overview

Spread stream processing is the write-path specialization for XLSX output that
does **not** materialize the whole workbook in memory. It writes OpenXML parts
directly to a target stream as rows are produced, with constant memory use
independent of worksheet size.

The target surface is intentionally narrower than a full spread-processing
document model:

- **Writer-only.** There is no in-memory document model and no read path; this
  is an emit-as-you-go XLSX writer.
- **Row-at-a-time API.** Cells are written left-to-right within a row; rows are
  closed before the next row begins. There is no back-editing of an already-written
  row.
- **Styles are defined up-front** as a named style table referenced by index on
  each cell; there is no per-cell style authoring after the row closes.
- **Formulas are persisted as-authored**; there is no in-process evaluation — the
  opening host computes values on load.
- **Merged cells, frozen panes, column widths, sheet names, and print settings**
  are declared through the writer's configuration surface before row emission
  begins.

This maps directly to the streaming OpenXML writer pattern — the same pattern
used by Telerik's `SpreadsheetStreaming`, OpenXML SDK's `SpreadsheetDocument` in
write mode, and EPPlus's `ExcelPackage` in streaming mode.

See [Export Large Data](export-large-data.md) for when to choose streaming over a
full document model.

## Telerik parity reference

- Telerik package: `Telerik.Documents.Spreadsheet.FormatProviders.OpenXml`
  (specifically the `RadSpreadsheetStreaming` / `SpreadsheetStreaming` writer
  surface.)
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radspreadstreamprocessing/overview>

## Sunfish plans

No Sunfish equivalent exists today. [ADR 0021](xref:adr-0021-reporting-pipeline-policy)
defines `IXlsxExportWriter` as the format contract; the current default
**ClosedXML** adapter uses an in-memory `XLWorkbook` and is not streaming. A
streaming-capable sibling contract — `IXlsxStreamExportWriter` or a
`SupportsStreaming` capability flag on `IXlsxExportWriter` — is in the deferred
second-phase scope of the reporting work. Deployers with large-data export
requirements today are expected to adopt `Sunfish.Reporting.Telerik` (Telerik's
`SpreadsheetStreaming` is the reference implementation), or a community adapter
wrapping OpenXML SDK's write-mode API.

## Related

- [Overview](overview.md)
- [Export Large Data](export-large-data.md)
- [Spread Processing — Overview](../spread-processing/overview.md)
