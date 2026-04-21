---
uid: library-spread-processing-formulas
title: Spread Processing — Formulas
description: aspirational library doc
---

# Spread Processing — Formulas

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](xref:adr-0021-reporting-pipeline-policy) for the reporting pipeline policy.

## Overview

Formula support in a spread-processing library covers both **persisting** formulas
into XLSX cells (so Excel or another spreadsheet host evaluates them on open) and
**evaluating** formulas in-process (so the library produces the computed value
without requiring Excel).

The target categories mirror Telerik's RadSpreadProcessing formula engine scope:

- **Math and trigonometry** — `SUM`, `AVERAGE`, `ROUND`, `ABS`, `POWER`,
  `SQRT`, `SIN`/`COS`/`TAN`, `LOG`, and the rest of the standard arithmetic
  surface.
- **Financial** — `PMT`, `FV`, `PV`, `NPV`, `IRR`, `RATE`, depreciation
  functions (`SLN`, `DDB`), and bond-math functions.
- **Date and time** — `NOW`, `TODAY`, `DATE`, `YEAR`/`MONTH`/`DAY`,
  `DATEDIF`, `NETWORKDAYS`, `WORKDAY`, time-component extraction.
- **Lookup and reference** — `VLOOKUP`, `HLOOKUP`, `INDEX`, `MATCH`,
  `XLOOKUP`, `OFFSET`, `INDIRECT`, `CHOOSE`.
- **Logical** — `IF`, `IFS`, `IFERROR`, `AND`, `OR`, `NOT`, `SWITCH`.
- **Text** — `CONCAT`/`CONCATENATE`, `LEFT`/`RIGHT`/`MID`, `LEN`, `TRIM`,
  `UPPER`/`LOWER`/`PROPER`, `TEXT`, `VALUE`.
- **Statistical** — `COUNT`/`COUNTA`/`COUNTIF`, `MAX`/`MIN`, `MEDIAN`,
  `STDEV`, `VAR`, `PERCENTILE`, `RANK`.

Cross-sheet references (`Sheet2!A1`), named ranges, absolute vs. relative
references, and array formulas are all in the target scope.

## Telerik parity reference

- Telerik package: `Telerik.Documents.Spreadsheet`
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radspreadprocessing/features/formulas/overview>

## Sunfish plans

No Sunfish equivalent exists today. The default XLSX adapter chosen by
[ADR 0021](xref:adr-0021-reporting-pipeline-policy) — **ClosedXML** — persists
formulas into the file and relies on the opening host to evaluate them. A first-party
in-process formula evaluator is **not** planned for the default adapter; deployers
that need in-process evaluation are expected to adopt `Sunfish.Reporting.Telerik`
or a comparable commercial adapter that ships an engine. The formula surface
described above documents the parity target for those commercial adapters, not a
Sunfish-owned engine.

## Related

- [Overview](overview.md)
- [Styling](styling.md)
- [Templates](templates.md)
- [Import and Export](import-export.md)
