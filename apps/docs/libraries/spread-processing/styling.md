---
uid: library-spread-processing-styling
title: Spread Processing — Styling
description: Aspirational library doc — cell and range styling surface targets against the ADR 0021 reporting pipeline.
keywords:
  - styling
  - cell style
  - XLSX formatting
  - conditional formatting
  - aspirational
  - ADR 0021
---

# Spread Processing — Styling

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0021-reporting-pipeline-policy.md) for the reporting pipeline policy.

## Overview

Styling in a spread-processing library covers the visual properties applied to
cells, ranges, rows, and columns. The target surface includes:

- **Fonts** — family, size, weight (bold), italics, underline, strike-through,
  subscript / superscript, per-run rich-text formatting within a single cell.
- **Foreground and fill** — font color and cell background (solid, pattern,
  gradient) with theme-color and tint support.
- **Borders** — per-edge (top / bottom / left / right / diagonal) style and
  color, including `Thin`, `Medium`, `Thick`, `Dotted`, `Dashed`, and
  `Double` line styles.
- **Alignment** — horizontal (`Left`, `Center`, `Right`, `Justify`), vertical
  (`Top`, `Middle`, `Bottom`), text rotation, indent, and wrap-text.
- **Number formats** — built-in (`General`, `Number`, `Currency`, `Date`,
  `Percent`, `Scientific`) and custom format strings (`#,##0.00;[Red]-#,##0.00`).
- **Cell styles** — named style objects that bundle font, fill, border, and
  number format into a reusable unit, applied by style name.
- **Conditional formatting** — value-based, formula-based, color-scale,
  data-bar, and icon-set rules attached to ranges.
- **Merged cells** and **frozen panes** for layout control.

## Telerik parity reference

- Telerik package: `Telerik.Documents.Spreadsheet`
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radspreadprocessing/features/styling/cell-styling>

## Sunfish plans

No Sunfish equivalent exists today. [ADR 0021](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0021-reporting-pipeline-policy.md)
routes XLSX output through `IXlsxExportWriter`; concrete styling is whatever the
chosen adapter exposes. **ClosedXML** (the default pure-OSS adapter) supports the
full styling surface described above through its fluent `IXLStyle` API;
**`Sunfish.Reporting.Telerik`** exposes Telerik's `CellStyle` and theme model
through the adapter. A Sunfish-owned, library-neutral styling DTO that flows across
the `IXlsxExportWriter` seam is in the deferred second-phase scope of the reporting
work. Until then, bundle authors that need rich cell styling are expected to reach
into the concrete adapter's API at composition time.

## Related

- [Overview](overview.md)
- [Formulas](formulas.md)
- [Templates](templates.md)
- [Import and Export](import-export.md)
