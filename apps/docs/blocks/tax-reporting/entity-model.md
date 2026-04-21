---
uid: block-tax-reporting-entity-model
title: Tax Reporting — Entity Model
description: TaxReport and its discriminated body subtypes — Schedule E, 1099-NEC, and state personal property.
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

## Related

- [Overview](overview.md)
- [Signed Hash Export](signed-hash-export.md)
