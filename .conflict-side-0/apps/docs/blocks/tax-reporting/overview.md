---
uid: block-tax-reporting-overview
title: Tax Reporting — Overview
description: Introduction to the blocks-tax-reporting package — Schedule E, 1099-NEC, signed hash export, and amendment workflow.
keywords:
  - tax
  - schedule-e
  - 1099-nec
  - personal-property
  - content-hash
  - canonical-json
  - tamper-evident
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
- No persistence — the block ships an in-memory service only. EF Core entity module is a follow-up.

## Where things live in the package

| Path (under `packages/blocks-tax-reporting/`) | Purpose |
|---|---|
| `Models/TaxReport.cs` | Aggregate report record. |
| `Models/TaxReportBody.cs` | Discriminated base. |
| `Models/ScheduleEBody.cs`, `SchedulePropertyRow.cs` | Schedule E shape. |
| `Models/Form1099NecBody.cs`, `Nec1099Recipient.cs` | 1099-NEC shape. |
| `Models/StatePersonalPropertyBody.cs`, `PersonalPropertyRow.cs` | State personal property shape. |
| `Models/TaxReportKind.cs`, `TaxReportStatus.cs` | Discriminant and lifecycle enums. |
| `Services/ITaxReportingService.cs` | Framework-agnostic contract. |
| `Services/InMemoryTaxReportingService.cs` | Thread-safe in-memory implementation with per-report locking. |
| `Services/TaxReportCanonicalJson.cs` | Canonical-JSON bytes + SHA-256 helper. |
| `Services/ScheduleEGenerationRequest.cs`, `Nec1099GenerationRequest.cs` | Input DTOs. |
| `Rendering/ITaxReportTextRenderer.cs`, `TaxReportTextRenderer.cs` | Plain-text preview renderer. |
| `DependencyInjection/TaxReportingServiceCollectionExtensions.cs` | `AddInMemoryTaxReporting` extension. |
| `tests/TaxReportingServiceTests.cs` | Lifecycle and state-transition tests. |
| `tests/TaxReportTextRendererTests.cs` | Renderer regression tests. |

## End-to-end example

```csharp
using Sunfish.Blocks.TaxReporting.Models;
using Sunfish.Blocks.TaxReporting.Services;
using Sunfish.Blocks.TaxReporting.Rendering;

// Host wiring
services.AddInMemoryTaxReporting();

// Generate Schedule E for 2025
var svc = sp.GetRequiredService<ITaxReportingService>();
var renderer = sp.GetRequiredService<ITaxReportTextRenderer>();

var request = new ScheduleEGenerationRequest(
    Year: new TaxYear(2025),
    Properties: new[]
    {
        new SchedulePropertyRow(
            PropertyId: EntityId.NewId(),
            Address: "123 Main St",
            RentsReceived: 18_000m,
            MortgageInterest: 6_200m,
            Taxes: 2_400m,
            Insurance: 900m,
            Repairs: 1_050m,
            Depreciation: 3_000m,
            OtherExpenses: 400m),
    });

var draft = await svc.GenerateScheduleEAsync(request, ct);
var final = await svc.FinalizeAsync(draft.Id, ct);          // computes SHA-256
var text  = renderer.Render(final);                          // for human review
var signed = await svc.SignAsync(final.Id, "approver@example.com:2026-04-21", ct);
```

## Independence from other blocks

The block accepts opaque input DTOs and does not read from any other block. Consumers translate their domain data (`blocks-accounting` journal entries, `blocks-rent-collection` invoices, a spreadsheet import) into `SchedulePropertyRow` or `Nec1099Recipient` records in application code. This keeps the block free of cross-block coupling and allows it to ship on its own cadence.

## ADRs in effect

- **ADR 0004 — Post-quantum signature migration** — motivates the `SignatureValue` content-hash-first approach and the deferred Ed25519 pass; real signing will flow through Foundation's `PrincipalId`.
- **ADR 0022 — Example catalog + docs taxonomy** — governs this docs page set.

## Narrow scope, on purpose

`blocks-tax-reporting` is deliberately small:

- **Three forms.** Schedule E, Form 1099-NEC, and a state-personal-property schema carrier. New IRS or state forms require an explicit add — do not expect the block to cover any US tax form.
- **One aggregate.** `TaxReport` is the only persistent-ish entity. There is no taxpayer record, no filing history, no batch wrapper; those are consumer territory.
- **Three transitions.** Draft → Finalized → Signed, with Amend producing a new draft and superseding the original. No partial signatures, no co-signers, no rejection flow.
- **No filing.** The block prepares and seals content; it does not submit or e-file. Integration with IRS Modernized e-File, state DOR portals, or tax-prep software is consumer scope.

Stretching the block to cover other forms is technically possible — add a new `TaxReportKind`, a new body subtype, and a new `GenerateXxxAsync` method — but the canonical-JSON and signing infrastructure is the main value and is designed to accommodate additional bodies cleanly.

## Related

- [Entity Model](entity-model.md)
- [Signed Hash Export](signed-hash-export.md)
