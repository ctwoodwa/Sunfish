---
uid: block-tax-reporting-overview
title: Tax Reporting — Overview
description: Introduction to the blocks-tax-reporting package — Schedule E, 1099-NEC, signed hash export, and amendment workflow.
---

# Tax Reporting — Overview

## Overview

The `blocks-tax-reporting` package produces structured, stage-aware tax reports from domain data. It ships three report bodies (`ScheduleEBody`, `Form1099NecBody`, `StatePersonalPropertyBody`) under a common discriminated `TaxReportBody` base, a state-machine service that moves reports through the Draft → Finalized → Signed (→ Amended → Superseded) lifecycle, canonical-JSON content hashing for integrity, and a plain-text renderer for previewing reports.

The block is intentionally standalone. It does not read from `blocks-accounting` directly — consumers translate journal entries (or other domain data) into the block's opaque input DTOs. This keeps tax-reporting independently mergeable alongside accounting work.

## Package path

`packages/blocks-tax-reporting` — assembly `Sunfish.Blocks.TaxReporting`.

## When to use it

- You need to generate IRS Schedule E (rental income and loss) and Form 1099-NEC (nonemployee compensation) from per-property or per-recipient input data.
- You want a content-hash "signature" (SHA-256 over canonical JSON) on finalized reports for tamper-evidence, without pulling in a real signing infrastructure yet.
- You want a state machine that enforces lifecycle transitions (`FinalizeAsync` requires Draft; `SignAsync` requires Finalized; `AmendAsync` requires Signed or Finalized) with clear exception semantics.

## Key entities

- **`TaxReport`** — the report record: `Id`, `Year`, `Kind`, optional `PropertyId`, `Status`, `GeneratedAtUtc`, `SignatureValue`, `Body`.
- **`TaxReportBody`** — discriminated base with concrete subtypes:
  - **`ScheduleEBody`** — Supplemental Income and Loss with per-property rows and aggregate totals.
  - **`Form1099NecBody`** — list of `Nec1099Recipient` rows (automatically filtered to those meeting the $600 threshold).
  - **`StatePersonalPropertyBody`** — state abbreviation plus a list of `PersonalPropertyRow` items; per-state templates are deferred.
- **`TaxReportKind`** — discriminant enum matching the body subtypes.
- **`TaxReportStatus`** — lifecycle enum: `Draft`, `Finalized`, `Signed`, `Amended`, `Superseded`.

## Key services

- **`ITaxReportingService`** — core contract for generation, state transitions, and querying.
- **`InMemoryTaxReportingService`** — thread-safe in-memory implementation with per-report locking.
- **`TaxReportCanonicalJson`** — static helper that produces canonical UTF-8 JSON bytes and a SHA-256 hex string for a report body. See [Signed Hash Export](signed-hash-export.md).
- **`ITaxReportTextRenderer`** / **`TaxReportTextRenderer`** — plain-text renderer that produces a tab-aligned preview of a `TaxReport`.

## DI wiring

```csharp
services.AddInMemoryTaxReporting();
```

Registers a singleton `ITaxReportingService` backed by `InMemoryTaxReportingService`, plus a singleton `ITaxReportTextRenderer` backed by `TaxReportTextRenderer`. `TaxReportCanonicalJson` is a static helper and needs no registration.

## Independence from `blocks-accounting`

`ITaxReportingService` accepts opaque input DTOs (`ScheduleEGenerationRequest`, `Nec1099GenerationRequest`). Consumers translate their domain data (e.g. accounting journal entries) into those DTOs in their application code. This keeps the two blocks orthogonal and independently mergeable.

## Status and deferred items

- `SignatureValue` is a SHA-256 hex string over canonical-JSON — a **content hash**, not a real digital signature. Real Ed25519 signing via Foundation's `PrincipalId` primitive is a future pass.
- Per-state personal-property form templates and line mappings are deferred; `StatePersonalPropertyBody` is a schema carrier.
- Amendment reasons are currently not stored as a structured field on the report record — they are a `string` parameter to `AmendAsync` that future passes may surface explicitly.

## Related

- [Entity Model](entity-model.md)
- [Signed Hash Export](signed-hash-export.md)
