---
uid: library-words-processing-overview
title: Words Processing — Overview
description: aspirational library doc
---

# Words Processing — Overview

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](xref:adr-0021-reporting-pipeline-policy) for the reporting pipeline policy.

## Overview

Words processing covers programmatic authoring, reading, and modification of
word-processing documents — primarily `.docx` (Office Open XML) and `.rtf`
(Rich Text Format), with `.txt` and `.html` as lower-fidelity formats.

The target authoring surface:

- **Document structure** — sections, headers, footers, page setup (size,
  orientation, margins, columns).
- **Block content** — paragraphs, headings, lists (ordered, unordered, multi-level),
  tables (with nested tables, merged cells, repeat-header rows), images, text boxes.
- **Inline content** — runs with font, size, weight, color, highlight,
  underline, strikethrough, hyperlinks, bookmarks, fields, and inline images.
- **Styles** — named paragraph and character styles, style inheritance, a
  style-catalog-based theme.
- **Fields and mail-merge** — `MERGEFIELD`, `IF`, `DATE`, `PAGE`, `SECTION`,
  `TOC`, and custom fields with runtime substitution for mail-merge workflows.
- **Track changes and comments** — reading and writing revision marks, inserted
  comments, and author metadata.
- **Tables of contents and indexes** — generation from headings, refreshable
  after content edits.
- **Bookmarks, cross-references, and hyperlinks** — intra-document and external.

Export to PDF is a distinct operation covered in
[PDF Export](pdf-export.md).

## Telerik parity reference

- Telerik package: `Telerik.Windows.Documents` (specifically
  `Telerik.Windows.Documents.Flow` for the flow document model.)
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radwordsprocessing/overview>

## Sunfish plans

No Sunfish equivalent exists today. [ADR 0021](xref:adr-0021-reporting-pipeline-policy)
routes DOCX through `IDocxExportWriter` with **NPOI** as the default pure-OSS
adapter (`Sunfish.Reporting.Npoi`) and optional commercial adapters including
`Sunfish.Reporting.Telerik` and `Sunfish.Reporting.Aspose`. The current contract
is **export-only** and targets semantic content (headings, paragraphs, tables,
fields) not a full document-model API.

A dedicated high-level "words processing" API equivalent to Telerik's
RadWordsProcessing — a mutable document model with full read and edit surfaces,
mail-merge, and revision tracking — is in the deferred second-phase scope of the
reporting work and is not yet in flight. Bundle authors that need document-model
operations on `.docx` today should use the chosen adapter's native API.

## Related

- [Overview](overview.md)
- [PDF Export](pdf-export.md)
