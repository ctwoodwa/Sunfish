---
uid: block-tax-reporting-entity-model
title: Tax Reporting — Entity Model
description: TaxReport and its discriminated body subtypes — Schedule E, 1099-NEC, and state personal property.
keywords:
  - tax-report
  - discriminated-union
  - schedule-e
  - 1099-nec
  - state-personal-property
---

# Tax Reporting — Entity Model

## Overview

The tax-reporting block centres on a single aggregate record — `TaxReport` — that carries its body via a discriminated union (`TaxReportBody` with three subtypes). This page walks every entity in the package, its fields, and how the pieces relate.

## TaxReport

```csharp
public sealed record TaxReport(
    TaxReportId Id,
    TaxYear Year,
    TaxReportKind Kind,
    EntityId? PropertyId,
    TaxReportStatus Status,
    Instant GeneratedAtUtc,
    string? SignatureValue,
    TaxReportBody Body);
```

| Field | Type | Notes |
|---|---|---|
| `Id` | `TaxReportId` | Strong-typed id. |
| `Year` | `TaxYear` | Strong-typed tax year. |
| `Kind` | `TaxReportKind` | Discriminant — matches `Body.Kind`. |
| `PropertyId` | `EntityId?` | Optional property scope. `null` = aggregate. |
| `Status` | `TaxReportStatus` | Lifecycle state. |
| `GeneratedAtUtc` | `Instant` | Creation timestamp. |
| `SignatureValue` | `string?` | Canonical-JSON SHA-256 hex string; set on `FinalizeAsync`; overwritten on `SignAsync` with the consumer-provided signature string. |
| `Body` | `TaxReportBody` | Polymorphic body. |

## TaxReportKind discriminant

| Enum value | Body subtype |
|---|---|
| `ScheduleE` | `ScheduleEBody` |
| `Form1099Nec` | `Form1099NecBody` |
| `StatePersonalProperty` | `StatePersonalPropertyBody` |

The convention is that `TaxReport.Kind == TaxReport.Body.Kind` always holds; consumers can pattern-match on either side.

## TaxReportStatus lifecycle

```
Draft ──FinalizeAsync──▶ Finalized ──SignAsync──▶ Signed
                           │                         │
                           └──────AmendAsync─────────┘
                                     │
                          original → Superseded, new Draft emitted
```

- `Draft` — content mutable.
- `Finalized` — content locked; `SignatureValue` set to the SHA-256 of canonical JSON.
- `Signed` — consumer-provided signature string recorded verbatim.
- `Amended` — transient conceptual state (the enum value exists but is set on the *superseded* predecessor in the in-memory impl, which uses `Superseded` instead; see code for exact bookkeeping).
- `Superseded` — retained for audit; do not file.

## ScheduleEBody

```csharp
public sealed record ScheduleEBody(
    IReadOnlyList<SchedulePropertyRow> Properties,
    decimal TotalRents,
    decimal TotalExpenses,
    decimal NetIncomeOrLoss) : TaxReportBody;
```

Per-property rows aggregate into body-level totals. The service computes the totals automatically from `Properties`.

### SchedulePropertyRow

| Field | Type | Notes |
|---|---|---|
| `PropertyId` | `EntityId` | Reference to the property. |
| `Address` | `string` | Display string for the form. |
| `RentsReceived` | `decimal` | Total receipts. |
| `MortgageInterest` | `decimal` | Deductible mortgage interest. |
| `Taxes` | `decimal` | Real-estate taxes. |
| `Insurance` | `decimal` | Insurance premiums. |
| `Repairs` | `decimal` | Repair/maintenance. |
| `Depreciation` | `decimal` | Depreciation deduction. |
| `OtherExpenses` | `decimal` | Catch-all. |

Derived properties on the row:

- `TotalExpenses` — sum of the six expense fields.
- `NetIncomeOrLoss` — `RentsReceived - TotalExpenses`.

## Form1099NecBody

```csharp
public sealed record Form1099NecBody(
    IReadOnlyList<Nec1099Recipient> Recipients) : TaxReportBody;
```

`Generate1099NecAsync` automatically drops recipients whose `TotalPaid` is below the IRS $600 threshold.

### Nec1099Recipient

| Field | Type | Notes |
|---|---|---|
| `RecipientName` | `string` | Full legal name. |
| `RecipientTaxId` | `string` | Masked TIN, e.g. `"XXX-XX-1234"`. |
| `RecipientAddress` | `string` | Mailing address. |
| `TotalPaid` | `decimal` | Nonemployee compensation for the year. |
| `AccountNumber` | `string?` | Optional reference. |

Helpers:

- `IrsThreshold` — `600m` constant.
- `MeetsThreshold` — `TotalPaid >= IrsThreshold`.
- `Validate()` — throws `InvalidOperationException` if below threshold.

## StatePersonalPropertyBody

```csharp
public sealed record StatePersonalPropertyBody(
    string StateCode,
    IReadOnlyList<PersonalPropertyRow> Items) : TaxReportBody;
```

`StateCode` is a two-letter US abbreviation. Per-state templates, line mappings, and submission flow are deferred — this body is a schema carrier.

### PersonalPropertyRow

| Field | Type | Notes |
|---|---|---|
| `Description` | `string` | Asset description. |
| `AcquisitionYear` | `int` | Year acquired. |
| `OriginalCost` | `decimal` | Cost basis. |
| `ReportedValue` | `decimal` | Reported value for the period. |

## Persistence note

The block does not ship an `ISunfishEntityModule`. Persistence, if you need it, is up to the host today — the in-memory service is the only shipped backing store.

A persistence-backed implementation must serialise `TaxReport.Body` using a polymorphic serializer (System.Text.Json with type discrimination or a hand-rolled converter) because EF Core does not natively support polymorphic records without explicit configuration. One common approach:

```csharp
// In a custom entity configuration
builder.Property(r => r.Body)
    .HasConversion(
        body => TaxReportCanonicalJson.Compute(body),
        bytes => DeserializeBody(bytes));  // caller-owned dispatch on Kind
```

Storing canonical-JSON bytes instead of a richer polymorphic column is an easy path; the body reads out bit-identically to what `FinalizeAsync` hashed, which keeps the content hash stable through round-trip.

## Pattern-matching on body subtypes

C# pattern matching on the `TaxReportBody` hierarchy is the idiomatic consumer pattern:

```csharp
string Summarise(TaxReport report) => report.Body switch
{
    ScheduleEBody se => $"Net income: {se.NetIncomeOrLoss:C}",
    Form1099NecBody nec => $"{nec.Recipients.Count} recipient(s) meeting the $600 threshold",
    StatePersonalPropertyBody sp => $"{sp.StateCode}: {sp.Items.Count} asset(s)",
    _ => throw new InvalidOperationException("Unknown body subtype."),
};
```

The `report.Kind == report.Body.Kind` invariant is asserted in tests; callers may rely on it.

## Tax year primitive

`TaxYear` is a strong-typed struct wrapping an `int`. It is constructed from a plain `int` (`new TaxYear(2025)`) and formatted as `"2025"` when rendered. The primitive exists so that a "tax year" is not confused with an arbitrary integer anywhere in the service surface.

## Derived totals on `SchedulePropertyRow`

`SchedulePropertyRow` exposes two computed properties on top of the eight scalar fields:

- **`TotalExpenses`** — `MortgageInterest + Taxes + Insurance + Repairs + Depreciation + OtherExpenses`.
- **`NetIncomeOrLoss`** — `RentsReceived - TotalExpenses`.

These are value-level derivations and are recomputed on every access; they are not stored.

The aggregate `ScheduleEBody.TotalRents`, `TotalExpenses`, `NetIncomeOrLoss` fields are computed once in `GenerateScheduleEAsync` by summing the row-level values. They are stored on the body so they're stable across edits to the row list (amendment flow) and so the canonical-JSON hash covers them explicitly.

## 1099-NEC threshold filtering

`GenerateNec1099Async` drops any `Nec1099Recipient` whose `TotalPaid < IrsThreshold` (600). This is a deliberate filter, not a validation — amounts below the threshold are simply not reported on a 1099-NEC per IRS rules. If you need the full list (e.g. for an internal reconciliation report) generate it outside the block from your source data.

`Nec1099Recipient.Validate()` on the record itself throws `InvalidOperationException` when below threshold. It is a convenience for callers who want a hard check; the service's own filter is silent.

## Related

- [Overview](overview.md)
- [Signed Hash Export](signed-hash-export.md)
