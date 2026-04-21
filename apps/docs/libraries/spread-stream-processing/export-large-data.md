---
uid: library-spread-stream-processing-export-large-data
title: Spread Stream Processing — Export Large Data
description: Aspirational library doc — large-data XLSX export guidance for streaming vs. document model.
keywords:
  - large data export
  - streaming
  - XLSX
  - memory constrained
  - aspirational
  - ADR 0021
---

# Spread Stream Processing — Export Large Data

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](xref:adr-0021-reporting-pipeline-policy) for the reporting pipeline policy.

## Overview

Spread stream processing exists specifically for **large-data XLSX export** —
scenarios where the row count is large enough that materializing the full
workbook in memory is unacceptable.

### When to use streaming

Reach for streaming when the export has any of these characteristics:

- **Roughly 50,000 rows or more.** An in-memory workbook of 50k rows with
  modest styling is tens to low-hundreds of MB; streaming is constant memory
  regardless of row count.
- **Unknown row count at start.** Queries that project from a database or a
  generator where total rows are not known up front.
- **Row-at-a-time production.** The rows arrive as an `IAsyncEnumerable<T>` or
  similar pull-based source and can be emitted as they are produced, with no
  buffering.
- **Memory-constrained host.** Containers, serverless functions, or mobile
  clients where peak working set must stay bounded.
- **Long-running or cancelable exports.** Streaming lets the writer flush bytes
  to disk or to the HTTP response as rows are produced, and honor
  `CancellationToken` without discarding work already written.

### When the full document model is the right choice

Use a full spread-processing document model (see
[Spread Processing — Overview](../spread-processing/overview.md)) when:

- Row count is small (under ~10,000) and styling richness matters.
- You need to **read** an existing workbook as well as write one.
- The export depends on post-hoc operations — adding a summary sheet at the
  end that references totals from the data sheet, rearranging sheets, or
  applying conditional formatting based on the full dataset range.
- You need in-process formula evaluation for values to be correct in downstream
  consumers that cannot recalculate.

### Memory characteristics

Streaming XLSX writers hold approximately:

- One in-flight row worth of cell buffers.
- The shared-string table (if enabled); set to inline strings to cap this.
- Style definitions, defined up-front and constant-size.
- The open OpenXML package writer's ZIP deflate state.

Peak working-set is typically in the low-to-mid MB range regardless of how
many rows are written.

## Telerik parity reference

- Telerik package: `Telerik.Documents.Spreadsheet.FormatProviders.OpenXml`
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radspreadstreamprocessing/overview>

## Sunfish plans

No Sunfish equivalent exists today. [ADR 0021](xref:adr-0021-reporting-pipeline-policy)
has not yet defined a streaming XLSX contract; the default `IXlsxExportWriter`
adapter is in-memory. Deployers with 50k+ row exports today are expected to
either adopt `Sunfish.Reporting.Telerik`, which wraps Telerik's streaming writer,
or author a community adapter against OpenXML SDK's write-mode
`SpreadsheetDocument`. A native `IXlsxStreamExportWriter` contract and a default
pure-OSS streaming adapter are both in the deferred second-phase scope.

## Related

- [Overview](overview.md)
- [Spread Processing — Import and Export](../spread-processing/import-export.md)
