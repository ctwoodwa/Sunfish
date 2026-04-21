---
uid: library-words-processing-pdf-export
title: Words Processing — PDF Export
description: aspirational library doc
---

# Words Processing — PDF Export

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](xref:adr-0021-reporting-pipeline-policy) for the reporting pipeline policy.

## Overview

Word-to-PDF conversion takes a `.docx` or `.rtf` document and renders it to a
PDF byte stream with the page layout, styling, fonts, and embedded images
preserved.

The target flow:

1. Load a `.docx` / `.rtf` into the document model (optionally produced
   directly by the same library — see [Overview](overview.md)).
2. Resolve fonts — embedded in the source, looked up in the system font
   collection, or provided by a configured font source.
3. Lay out each section's pages, honoring page size, margins, columns, page
   breaks, and widow/orphan rules.
4. Render block and inline content to PDF content streams — text as glyphs
   with font subsets embedded, images as XObjects, tables and borders as
   vector paths.
5. Emit the finished PDF as a stream with optional metadata (title, author,
   subject), encryption (owner / user passwords), and PDF/A compliance.

Per-export controls in the target scope:

- **Font handling** — embed-all, embed-subset, substitute-missing.
- **Image handling** — preserve, downsample to target DPI, or recompress.
- **PDF/A profile** — PDF/A-1a, PDF/A-2b, PDF/A-3b for archival.
- **Metadata** — document title, author, subject, keywords, producer.
- **Security** — user password, owner password, permissions mask (print,
  copy, edit).
- **Bookmarks** — derive from document headings.

## Telerik parity reference

- Telerik package: `Telerik.Windows.Documents.Fixed` (PDF format provider)
  plus `Telerik.Windows.Documents.Flow.FormatProviders.Pdf` (the
  flow-document-to-PDF converter.)
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radwordsprocessing/formats-and-conversion/pdf/pdfformatprovider>

## Sunfish plans

No Sunfish equivalent exists today. [ADR 0021](xref:adr-0021-reporting-pipeline-policy)
defines two separate contracts:

- `IDocxExportWriter` — authoring a `.docx` from semantic content.
- `IPdfExportWriter` — authoring a `.pdf` from semantic content.

Both are export-only and author directly from Sunfish semantic content objects.
There is **no Sunfish-owned DOCX-to-PDF conversion contract today.** A deployer
that needs DOCX-to-PDF conversion is expected to either:

1. Chain the contracts — render the semantic content through `IPdfExportWriter`
   directly, bypassing the DOCX intermediate, if the source is Sunfish-authored.
2. Adopt a commercial adapter — `Sunfish.Reporting.Telerik`,
   `Sunfish.Reporting.Aspose`, or `Sunfish.Reporting.Syncfusion` — that wraps
   the vendor's native DOCX-to-PDF converter behind an adapter-specific API.

A Sunfish-owned `IDocumentConvertWriter` or `IDocxToPdfConverter` contract is in
the deferred second-phase scope of the reporting work.

## Related

- [Overview](overview.md)
