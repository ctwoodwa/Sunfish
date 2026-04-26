---
uid: library-spread-processing-templates
title: Spread Processing — Templates
description: Aspirational library doc — template-based spreadsheet generation against the ADR 0021 reporting pipeline.
keywords:
  - templates
  - XLSX template
  - named range
  - placeholder tokens
  - aspirational
  - ADR 0021
---

# Spread Processing — Templates

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0021-reporting-pipeline-policy.md) for the reporting pipeline policy.

## Overview

Template-based spreadsheet generation is a workflow where a designer-prepared
XLSX (with headers, styling, merged cells, formulas, and charts already laid
out) acts as a shell, and code fills the data region, preserving all surrounding
formatting.

The target workflow:

1. A designer authors `invoice-template.xlsx` in Excel, sets up the branding,
   logo image, header styling, column widths, and footer formulas.
2. Bundle code opens the template, navigates to a known anchor (a named
   range, a specific row, or a marker cell), and writes the per-invoice data.
3. The library recalculates formulas, adjusts row heights if the data region
   grew, and writes the finished workbook to a stream.

Features in the target scope:

- **Named ranges as anchors** — `Workbook.Names["InvoiceLines"]` resolves to
  a range the code writes into, so the template can move without breaking code.
- **Formula propagation** — inserting rows into a data region propagates
  totals-row formulas automatically.
- **Style preservation** — data written into styled rows inherits the
  template's styling without the code having to restate it.
- **Placeholder tokens** — string tokens like `{{CustomerName}}` in header
  cells are replaced with runtime values.
- **Image placeholders** — anchored images (logos, signatures) are either
  preserved as-is or swapped by anchor name.

## Telerik parity reference

- Telerik package: `Telerik.Documents.Spreadsheet`
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radspreadprocessing/features/protection/workbook-protection>
  (template workflow is composed from workbook, named-range, and style-preservation
  features.)

## Sunfish plans

No Sunfish equivalent exists today. [ADR 0021](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0021-reporting-pipeline-policy.md)'s
reporting pipeline routes XLSX output through `IXlsxExportWriter`. A
template-aware contract — an `IXlsxTemplateWriter` or similar that takes a
template stream plus a data binding and produces a finished workbook — is
**not yet designed**; it is in the deferred second-phase scope. Bundle authors
that need template-based generation today are expected to use the adapter's
native template API (ClosedXML supports template copy-and-fill; Telerik, Aspose,
and GemBox offer richer template engines with placeholder tokens) behind their
own abstraction.

## Related

- [Overview](overview.md)
- [Formulas](formulas.md)
- [Styling](styling.md)
- [Import and Export](import-export.md)
