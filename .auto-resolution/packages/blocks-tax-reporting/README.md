# Sunfish.Blocks.TaxReporting

Tax-reporting block — entity types for IRS Schedule E + 1099-NEC + state personal-property forms; in-memory service; canonical-JSON signed-hash export; text renderer.

**First pass — Option B (templates + signed-hash export only).** Defers: per-jurisdiction state forms, PDF rendering, e-filing, automatic JE rollup.

## What this ships

### Models

- **`TaxReport`** — top-level container; references `TaxReportBody` (one of the form-shape unions below).
- **`TaxReportBody`** — discriminated body (Schedule E / 1099-NEC / State Personal-Property).
- **`ScheduleEBody`** + **`SchedulePropertyRow`** — IRS Form 1040 Schedule E (rental real estate income).
- **`Form1099NecBody`** + **`Nec1099Recipient`** — IRS Form 1099-NEC (non-employee compensation; vendor payments ≥ $600).
- **`StatePersonalPropertyBody`** + **`PersonalPropertyRow`** — generic state personal-property tax (varies by jurisdiction; jurisdiction-specific forms deferred).

### Services

- **`ITaxReportingService`** + `InMemoryTaxReportingService` — CRUD + export.
- **`SignedHashExporter`** — produces canonical-JSON signed-hash export for downstream filing systems; the hash pins the form snapshot for audit purposes.
- **`TextRenderer`** — flat-text projection for human review of generated reports.

## DI

```csharp
services.AddInMemoryTaxReporting();
```

## Deferred follow-ups

- Per-jurisdiction state-form templates (varies widely; needs general-counsel guidance)
- PDF rendering (requires the form layout templates)
- E-filing adapter (varies by IRS eFile / FIRE / state portals)
- Automatic JE rollup from `blocks-rent-collection` + `blocks-accounting` into the form pre-populator

## ADR map

- [ADR 0049](../../docs/adrs/0049-foundation-audit.md) — signed-hash export pattern (audit trail for filed forms)

## See also

- [apps/docs Overview](../../apps/docs/blocks/tax-reporting/overview.md)
- [Sunfish.Blocks.Accounting](../blocks-accounting/README.md) — JE source for tax-form pre-population
- [Sunfish.Blocks.RentCollection](../blocks-rent-collection/README.md) — rental income source for Schedule E
- [Sunfish.Blocks.Maintenance](../blocks-maintenance/README.md) — vendor payment source for 1099-NEC (W#18 Phase 4 W9Document is the upstream TIN source)
