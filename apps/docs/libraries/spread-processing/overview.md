---
uid: library-spread-processing-overview
title: Spread Processing — Overview
description: Aspirational library doc — spreadsheet authoring capability target against the ADR 0021 reporting pipeline.
keywords:
  - spread processing
  - xlsx
  - spreadsheet
  - ClosedXML
  - aspirational
  - ADR 0021
  - reporting
---

# Spread Processing — Overview

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0021-reporting-pipeline-policy.md) for the reporting pipeline policy.

## Overview

Spread processing covers programmatic creation, reading, and modification of
spreadsheet documents without a running Excel install or UI. The target capability
spans the full authoring surface:

- Creating workbooks and worksheets from scratch
- Reading existing XLSX / XLS / CSV files into an in-memory document model
- Writing cell values and typed content (numbers, dates, booleans, text)
- Applying formatting, styles, merged cells, freeze panes, and print settings
- Evaluating and persisting formulas (see [Formulas](formulas.md))
- Cell and range styling (see [Styling](styling.md))
- Template-based document generation (see [Templates](templates.md))
- Round-tripping between supported formats (see [Import and Export](import-export.md))

This sits on the Sunfish reporting pipeline's XLSX track. Bundle authors target
the `IXlsxExportWriter` contract from `Sunfish.Foundation.Reporting`; the concrete
spread-processing engine is a deployer choice.

## Where this fits in the ADR 0021 contract set

ADR 0021 specifies five format contracts under `Sunfish.Foundation.Reporting`:

| Contract | Default adapter | License |
|---|---|---|
| `IPdfExportWriter` | `Sunfish.Reporting.PdfSharp` (PDFsharp + MigraDoc) | MIT |
| `IXlsxExportWriter` | `Sunfish.Reporting.ClosedXml` (ClosedXML) | MIT |
| `IDocxExportWriter` | `Sunfish.Reporting.Npoi` (NPOI) | Apache-2.0 |
| `IPptxExportWriter` | `Sunfish.Reporting.Npoi` or `Sunfish.Reporting.ShapeCrawler` | Apache-2.0 / MIT |
| `ICsvExportWriter` | `Sunfish.Reporting.CsvHelper` (CsvHelper) | Apache-2.0 + MS-PL |

All five contracts are export-only in the current ADR scope. The spread-processing
pages catalog the richer document-model surface that a Telerik-like library ships
(read, write, formulas, styling, templates) — a target capability rather than a
landed feature.

## Telerik parity reference

- Telerik package: `Telerik.Documents.Spreadsheet`
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radspreadprocessing/overview>

## Sunfish plans

No Sunfish equivalent exists today. [ADR 0021](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0021-reporting-pipeline-policy.md)
routes XLSX through `IXlsxExportWriter` with **ClosedXML** as the default pure-OSS
adapter (`Sunfish.Reporting.ClosedXml`) and optional commercial adapters including
`Sunfish.Reporting.Telerik`. A dedicated high-level "spread processing" API
equivalent to Telerik's RadSpreadProcessing — covering rich document models
beyond the export-writer surface — is in the deferred second-phase scope of the
reporting work and is not yet in flight. Bundle authors that need document-model
operations today should use the chosen adapter's native API behind the
`IXlsxExportWriter` seam.

## Adapter swap example (ADR 0021 pattern)

The contract-and-adapter split means deployers choose XLSX engines at composition
time without changing bundle code:

```csharp
// default pure-OSS
services.AddClosedXmlXlsxExport();

// or opt-in commercial
services.AddTelerikXlsxExport();
```

Bundle code resolves `IXlsxExportWriter` without knowing which adapter is active.
When a Sunfish-owned high-level spread-processing API lands, it will sit **above**
this contract — a bundle author reaches for document-model semantics, the library
resolves the active `IXlsxExportWriter` to persist the result.

## Related

- [Overview](overview.md)
- [Formulas](formulas.md)
- [Styling](styling.md)
- [Templates](templates.md)
- [Import and Export](import-export.md)
- [ADR 0021 — Reporting Pipeline Policy](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0021-reporting-pipeline-policy.md)
</content>
</invoke>