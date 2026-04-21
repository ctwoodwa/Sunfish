---
uid: library-spread-processing-overview
title: Spread Processing — Overview
description: aspirational library doc
---

# Spread Processing — Overview

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](xref:adr-0021-reporting-pipeline-policy) for the reporting pipeline policy.

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

## Telerik parity reference

- Telerik package: `Telerik.Documents.Spreadsheet`
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radspreadprocessing/overview>

## Sunfish plans

No Sunfish equivalent exists today. [ADR 0021](xref:adr-0021-reporting-pipeline-policy)
routes XLSX through `IXlsxExportWriter` with **ClosedXML** as the default pure-OSS
adapter (`Sunfish.Reporting.ClosedXml`) and optional commercial adapters including
`Sunfish.Reporting.Telerik`. A dedicated high-level "spread processing" API
equivalent to Telerik's RadSpreadProcessing — covering rich document models
beyond the export-writer surface — is in the deferred second-phase scope of the
reporting work and is not yet in flight. Bundle authors that need document-model
operations today should use the chosen adapter's native API behind the
`IXlsxExportWriter` seam.

## Related

- [Overview](overview.md)
- [Formulas](formulas.md)
- [Styling](styling.md)
- [Templates](templates.md)
- [Import and Export](import-export.md)
