---
id: 21
title: Document and Report Generation Pipeline
status: Accepted
date: 2026-04-20
tier: foundation
concern:
  - operations
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0021 — Document and Report Generation Pipeline

**Status:** Accepted
**Date:** 2026-04-20
**Supersedes:** —
**Superseded by:** —

## Context

Sunfish bundles generate documents in five formats: **PDF** (invoices, inspection reports, certificates, statements), **XLSX** (financial exports, roll-ups, data dumps), **DOCX** (contract templates, letters, form generation), **PPTX** (board decks, bundle-authored presentations), and **CSV** (data exchange, tabular exports). Different bundles have different format mixes and different license tolerances.

Three realities shape the decision:

**No single pure-managed OSS library covers all five formats.** The mature pure-OSS stack is a mix: PDFsharp+MigraDoc (MIT) for PDF, ClosedXML (MIT) or NPOI (Apache-2.0) for XLSX, NPOI for DOCX and PPTX (Apache-2.0), CsvHelper (Apache-2.0) for CSV. Each is well-maintained, pure managed, and compatible with Sunfish's MIT license.

**QuestPDF's license has a commercial gate.** QuestPDF — the current PDF library used by the in-flight `PlaywrightPdfExportWriter` companion work — uses a **Community MIT license** that is free only for organizations under $1M annual gross revenue, non-profits, FOSS projects, and transitive dependencies; Professional and Enterprise tiers apply above that threshold. The gate attaches to the **downstream consumer**, not the distributor, so a large enterprise adopting Sunfish is forced into QuestPDF's commercial tier regardless of Sunfish's own MIT license. QuestPDF's fluent API is the best in the .NET ecosystem, but its license is not compatible with a no-strings-attached OSS platform story.

**Commercial document libraries are legitimate choices for some deployers.** Telerik Document Processing Libraries (part of Telerik DevCraft), Syncfusion Essential Studio, Aspose Words/Cells/Slides, and GemBox.Document all offer richer APIs, superior format fidelity, and vendor support — at the cost of per-seat or per-project commercial licensing. Bundle authors serving enterprise deployers may prefer these; hobbyist and small-business deployers may not. This is a deployer-level choice, not a platform-level mandate.

Bundle authors need a way to say "my bundle emits a PDF invoice" without hard-coding which library renders it. Deployers need a way to swap implementations — for license reasons, format-fidelity reasons, or support reasons — without forking Sunfish.

## Decision

Sunfish adopts a **contract-and-adapter model** for document generation, mirroring the patterns established by ADR 0013 (Foundation.Integrations + provider neutrality) and ADR 0014 (UI adapter parity).

### 1. Format contracts live in Foundation

A new package — `Sunfish.Foundation.Reporting` — defines one interface per format. All interfaces are framework-agnostic; none reference a specific library's concrete types.

- `IPdfExportWriter` — the interface already drafted in `packages/ui-adapters-blazor/Components/DataDisplay/DataGrid/Export/` during the in-flight DataGrid export rename; this ADR promotes it to Foundation and splits it from the Blazor adapter.
- `IXlsxExportWriter`
- `IDocxExportWriter`
- `IPptxExportWriter`
- `ICsvExportWriter`

Each interface exposes format-neutral primitives (a stream-in, stream-out API with semantic content objects) and does not leak library-specific types across the boundary.

### 2. Pure-OSS permissive-licensed defaults ship as first-party implementations

Each contract has a default adapter using a library that is MIT or Apache-2.0 licensed, pure managed, actively maintained, and without a revenue threshold:

| Format | Default library | License | Package |
|---|---|---|---|
| PDF | **PDFsharp + MigraDoc** | MIT | `Sunfish.Reporting.PdfSharp` |
| XLSX | **ClosedXML** | MIT | `Sunfish.Reporting.ClosedXml` |
| DOCX | **NPOI** | Apache-2.0 | `Sunfish.Reporting.Npoi` (shared with PPTX) |
| PPTX | **NPOI** or **ShapeCrawler** | Apache-2.0 / MIT | `Sunfish.Reporting.Npoi` or `Sunfish.Reporting.ShapeCrawler` |
| CSV | **CsvHelper** | Apache-2.0 + MS-PL | `Sunfish.Reporting.CsvHelper` |

A default Sunfish deployment with only these packages has zero revenue-gated dependencies.

### 3. Alternative adapters ship as opt-in packages

Adapters that trade off license or runtime characteristics for richer APIs or higher fidelity ship as separate packages that bundle authors and deployers opt into:

| Adapter package | Underlying library | Tradeoff |
|---|---|---|
| `Sunfish.Reporting.QuestPDF` | QuestPDF | Best fluent API; Community MIT with $1M annual-revenue commercial gate |
| `Sunfish.Reporting.Playwright` | PlaywrightSharp + headless Chromium | HTML→PDF via browser rendering; highest CSS fidelity; NOT pure-managed (ships Chromium runtime) |
| `Sunfish.Reporting.Telerik` | Telerik Document Processing Libraries | Richer APIs, vendor support, commercial license per seat |
| `Sunfish.Reporting.Syncfusion` | Syncfusion Essential Studio | Broad format coverage, free community edition for small orgs, commercial otherwise |
| `Sunfish.Reporting.Aspose` | Aspose.Words / Aspose.Cells / Aspose.Slides | Highest format fidelity in the .NET ecosystem; commercial per-project |
| `Sunfish.Reporting.GemBox` | GemBox.Document / Spreadsheet / Presentation | Lightweight commercial alternative; free tier with limits |

Commercial adapters are **expected community contributions**, not maintained by the core project. Each adapter's NuGet description must prominently state its license and activation requirements.

### 4. Bundle manifests declare formats, not implementations

Per ADR 0007, a bundle manifest's `reporting[]` field lists the formats the bundle emits:

```yaml
reporting:
  - format: pdf
    examples: [invoice, receipt, statement]
  - format: xlsx
    examples: [monthly-close, rent-roll]
```

The bundle does **not** declare which library renders those formats. That is a deployer concern, resolved at composition time via DI: the deployer registers one implementation per contract (`services.AddPdfSharpExport()`, or `services.AddQuestPdfExport()`, or `services.AddTelerikReporting()`), and the bundle's code resolves `IPdfExportWriter` without knowing or caring which is active.

### 5. Foundation has no reporting-library dependency

`Sunfish.Foundation.Reporting` contains contracts, DTOs, and semantic content types only. It has zero references to PDFsharp, QuestPDF, NPOI, Telerik, or any other library. Library-specific code lives exclusively in adapter packages.

### 6. Parity expectations

Adapters for the same format must produce **observably equivalent output** — the same semantic document, same fields populated, same conditional sections rendered. Byte-equal output is explicitly not required; a DOCX rendered by NPOI will not byte-match one rendered by Telerik, and that is acceptable. Parity tests (per ADR 0014 pattern) verify observable equivalence against a fixture set of semantic content.

## Consequences

### Positive

- **License-clean default distribution.** A Sunfish deployment pulling only default adapters has zero revenue-gated dependencies. Enterprise adopters can operate under pure MIT/Apache terms without a commercial check.
- **Bundle authors pick ergonomics, deployers pick license and support.** A bundle author happy with QuestPDF's fluent API authors their bundle using `IPdfExportWriter` and documents QuestPDF as the recommended adapter. A deployer uncomfortable with the $1M gate swaps in PDFsharp+MigraDoc at composition time — no bundle code changes.
- **Commercial-vendor integration is a contribution path, not a gatekeeping moment.** Telerik, Syncfusion, Aspose, and GemBox integrations land as community adapter packages without requiring core platform changes. Sunfish does not endorse or reject commercial libraries; it just provides a clean integration surface.
- **Upgrade path is non-breaking.** If QuestPDF's license tightens further, if PDFsharp stagnates, or if a new pure-OSS entrant with a better API appears, swapping the default adapter is a patch-level change. Bundles that declare only the format are unaffected.
- **Consistent with existing Sunfish patterns.** The same contract-and-adapter model is already used for UI (ADR 0014), integrations (ADR 0013), and CSS providers. Bundle authors and deployers are not learning a new pattern.

### Negative

- **More packages to maintain.** One `foundation-reporting` plus N first-party default adapters plus M community adapter packages increases the overall surface area. Each requires its own CI, versioning, and changelog.
- **Default adapter APIs are less modern than QuestPDF's.** PDFsharp+MigraDoc's layout model is older and less declarative than QuestPDF's fluent API. Bundle authors writing PDF-heavy bundles may reasonably choose the QuestPDF adapter and accept the license. This is a tradeoff the policy makes visible rather than hides.
- **Observable-equivalence parity is a weaker contract than byte-equivalence.** A deployer switching adapters for the same format may see cosmetic differences in output. This is acceptable for most reporting scenarios (the semantic content is the value) but can be surprising. Documented per-adapter known-differences is a follow-up deliverable.
- **Commercial-adapter licensing is the deployer's responsibility.** Sunfish ships the adapter shim; the deployer holds the Telerik / Syncfusion / Aspose / GemBox license. Adapter documentation must make this unambiguous.
- **Kitchen-sink must demonstrate the contract, not just one implementation.** The `apps/kitchen-sink` demo uses the default adapters and documents how to swap. Demoing commercial adapters in kitchen-sink is not a default (no license assumptions), but the swap pattern is demoed with two pure-OSS adapters side-by-side to prove the contract works.

## References

- [ADR 0007 — Bundle Manifest Schema](0007-bundle-manifest-schema.md) — where `reporting[]` is declared.
- [ADR 0013 — Foundation.Integrations + Provider-Neutrality Policy](0013-foundation-integrations.md) — the same contract-and-adapter pattern applied to external integrations.
- [ADR 0014 — UI Adapter Parity Policy](0014-adapter-parity-policy.md) — the parity model adopted here.
- [`_shared/product/compatibility-policy.md`](../../_shared/product/compatibility-policy.md) — public-API stability guarantees for Foundation contracts.
- [`_shared/product/sustainability.md`](../../_shared/product/sustainability.md) — why revenue-gated transitive dependencies matter for the platform's adoption story.
- **QuestPDF license** — <https://www.questpdf.com/license/guide.html> (Community MIT, $1M annual-revenue threshold, Professional / Enterprise tiers above).
- **PDFsharp + MigraDoc** — <https://www.pdfsharp.net/> (MIT).
- **ClosedXML** — <https://github.com/ClosedXML/ClosedXML> (MIT).
- **NPOI** — <https://github.com/nissl-lab/npoi> (Apache-2.0); the .NET port of Apache POI; covers XLS / XLSX / DOCX / PPTX.
- **ShapeCrawler** — <https://github.com/ShapeCrawler/ShapeCrawler> (MIT); modern idiomatic C# PPTX manipulation.
- **CsvHelper** — <https://joshclose.github.io/CsvHelper/> (Apache-2.0 + MS-PL).
- **Telerik Document Processing Libraries** — <https://www.telerik.com/document-processing-libraries> (commercial; part of Telerik DevCraft bundle).
- **Syncfusion Essential Studio** — <https://www.syncfusion.com/document-processing> (commercial; Community License for <$1M organizations).
- **Aspose** — <https://products.aspose.com/> (commercial, per-project).
- **GemBox** — <https://www.gemboxsoftware.com/> (commercial; free tier with size limits).
