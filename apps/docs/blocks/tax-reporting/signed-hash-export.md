---
uid: block-tax-reporting-signed-hash-export
title: Tax Reporting — Signed Hash Export
description: Canonical-JSON SHA-256 hashing, the Finalize → Sign lifecycle, and the plain-text renderer used for export.
---

# Tax Reporting — Signed Hash Export

## Overview

When a `TaxReport` transitions from `Draft` to `Finalized`, the service computes a SHA-256 hex string over the canonical-JSON serialization of the report body and stores it in `TaxReport.SignatureValue`. When the report is then `Signed`, a consumer-supplied signature string is written over the top. This page walks both halves of that pipeline and the plain-text renderer used to produce a human-readable export.

## TaxReportCanonicalJson

The canonical-JSON helper is a static type — it needs no DI registration.

```csharp
public static class TaxReportCanonicalJson
{
    public static byte[] Compute(TaxReportBody body);
    public static string ComputeHash(TaxReportBody body);
}
```

### Canonicalization rules

- Compact JSON — no whitespace, no indentation.
- Properties emitted in a stable, alphabetically-sorted order (ordinal string comparison).
- Achieved via a two-pass approach: serialize with `System.Text.Json` (to pick up existing id converters like `TaxReportIdJsonConverter`), deserialize into a generic element tree, rebuild with `SortedDictionary<string, object?>` for objects, re-serialize.
- `decimal` values are preserved via `element.GetDecimal()` during the sort pass to avoid precision loss.
- Arrays preserve element order; only object properties are sorted.

The same logical body always produces the same bytes, regardless of property declaration order in the CLR types. This is the property that makes `ComputeHash` a stable content hash.

### Important caveat

`SignatureValue` is a **content hash**, not a cryptographic digital signature. It proves content integrity (the body bytes have not changed since finalization) but it does not prove authorship. Real Ed25519 signing over the canonical bytes, using Foundation's `PrincipalId` and private-key facility, is a future pass — there is a `TODO` on `TaxReport.SignatureValue` tracking it.

## FinalizeAsync lifecycle

```csharp
var draft    = await svc.GenerateScheduleEAsync(request, ct);      // Draft
var final    = await svc.FinalizeAsync(draft.Id, ct);              // Finalized, SignatureValue set
```

`FinalizeAsync`:

1. Requires `Status == Draft`. Throws `InvalidOperationException` otherwise.
2. Computes `TaxReportCanonicalJson.ComputeHash(report.Body)`.
3. Emits a new `TaxReport` record with `Status = Finalized` and `SignatureValue = <hex>`.
4. Holds a per-report lock while the update is applied so concurrent finalize attempts serialise.

## SignAsync lifecycle

```csharp
var signed   = await svc.SignAsync(final.Id, signatureValue: "ed25519:…", ct);
```

`SignAsync`:

1. Requires `Status == Finalized`. Throws `InvalidOperationException` otherwise.
2. Overwrites `SignatureValue` with the caller-supplied string.
3. The service does not validate the supplied signature; consumers are expected to produce a real signature (or a simple approval token) and record it verbatim.

Rationale: the block does not want to lock in a specific signing scheme until Foundation ships its `PrincipalId` + private-key primitives.

## AmendAsync lifecycle

```csharp
var amendment = await svc.AmendAsync(signed.Id, amendmentReason: "corrected depreciation", ct);
```

`AmendAsync`:

1. Requires `Status == Signed` or `Status == Finalized`.
2. Marks the original as `Superseded` — retained for audit, **do not file**.
3. Returns a new `Draft` with a fresh `Id`, same `Year` / `Kind` / `PropertyId`, and the original body.
4. The `amendmentReason` is a `string` parameter today. Future passes may surface it as a structured field on the report record.

## Plain-text renderer

`ITaxReportTextRenderer` produces a plain-text export from a `TaxReport`. The default implementation is `TaxReportTextRenderer`.

```csharp
public interface ITaxReportTextRenderer
{
    string Render(TaxReport report);
}
```

Output sections:

- **Header** — `TAX REPORT`, ID, Year, Kind, Status, optional Property, Generated timestamp, and SHA-256 content hash when present.
- **Body** — dispatched on `TaxReportBody` subtype:
  - `ScheduleEBody` — per-property rows with tab-aligned columns and a totals row.
  - `Form1099NecBody` — per-recipient blocks with name, masked TIN, address, amount, optional account number.
  - `StatePersonalPropertyBody` — state code, description / year / cost / reported value rows, plus an explicit "per-state form templates are deferred" note.

The renderer is suitable for previewing a report, for producing a readable attachment to an audit log, or for comparing two reports visually. It is **not** a production e-file format.

## Typical workflow

```csharp
// 1. Generate a Draft
var draft = await svc.GenerateScheduleEAsync(new ScheduleEGenerationRequest(
    Year: new TaxYear(2025),
    Properties: rows), ct);

// 2. Finalize (computes SHA-256 over canonical JSON).
var final = await svc.FinalizeAsync(draft.Id, ct);
Console.WriteLine($"Content hash: {final.SignatureValue}");

// 3. Render preview for human approval.
var text = renderer.Render(final);
Console.WriteLine(text);

// 4. Sign (record the approver's signature string).
var signed = await svc.SignAsync(final.Id, signatureValue: approverSignature, ct);
```

## Related

- [Overview](overview.md)
- [Entity Model](entity-model.md)
