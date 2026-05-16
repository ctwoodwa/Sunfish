# `Sunfish.Blocks.FinancialTax`

Tax-domain block: jurisdiction hierarchies, tax codes, effective-dated
rate history, a calculation engine (`OnSubtotal` / `Compound` /
`Inclusive`), and the Schedule E line-mapping seed that drives the
downstream Schedule E report.

This package implements **Stage 02 §3.12–§3.14 + §6.4** of the
`blocks-financial-*` cluster design and **ADR 0088 §1** (Anchor all-
in-one local-first runtime — no external tax-engine dependency; the
calculation runs in-process).

## Entities

- **`TaxJurisdiction`** — A federal / state / county / city / district / special
  node. Tree-shaped via `ParentJurisdictionId`. The `ITaxJurisdictionResolver`
  walks an address (`TaxLocationContext`) to a most-local-first ordered list.
- **`TaxCode`** — Chart-scoped tax code: `TaxKind` (Sales / VAT / GST / Exempt /
  …) × `TaxApplication` (OnSubtotal / Compound / Inclusive). Carries a `Version`
  bumped on every upsert (CRDT §3).
- **`TaxRate`** — Effective-dated rate for a `(TaxCode, Jurisdiction)` pair.
  Append-only per CRDT §4 — rate changes are new rows; the prior row's
  `ExpiryDate` is set via `ITaxRateLookup.SupersedeAsync`.
- **`TaxFormLineMap`** — Maps a chart-of-accounts subset (via
  `TaxAccountSelector` rows) to a single line on a specific tax form for a
  specific year. The Schedule E generator (in `Sunfish.Blocks.Reports.Tax`)
  consumes these to aggregate amounts per form line.

## Tax calculation

`ITaxCalculationService.CalculateAsync(input)` returns a structured
`TaxCalculationResult` with a per-rate breakdown the caller can split
into per-jurisdiction GL postings. Three application modes:

- **OnSubtotal** — `tax_i = subtotal × rate_i` (each rate independent).
- **Compound** — `tax_i = (subtotal + Σ_{j<i} tax_j) × rate_i`, ordered
  outermost-first per `JurisdictionLevel.OrderIndex` (Country / Federal first,
  then State, County, City, District, Special).
- **Inclusive** — Subtotal is gross (price-with-tax); back out the tax:
  `tax = subtotal × total_rate / (1 + total_rate)`. The last breakdown row
  absorbs the rounding residual so `Σ Breakdown.TaxAmount == TaxAmount` to
  the cent.

**Banker's rounding** at the 2-decimal minor-unit boundary
(`MidpointRounding.ToEven`) prevents systematic over-collection vs.
round-half-up. See Stage 02 §6.4 + PR #909's council-reviewed
regression battery for the canonical examples.

## Jurisdiction hierarchy

Example tree for a Virginia rental property:

```
US (Federal)
└── US-VA (State)
    └── Frederick County
        └── Winchester (City)
```

`ITaxJurisdictionResolver.ResolveAsync(context)` returns `[Winchester,
Frederick County, US-VA, US-Federal]` (most-local-first). Compound-tax
callers re-sort via `OrderIndex()` ascending.

## Schedule E mapping

`DefaultTaxFormLineMap.ScheduleE(chartId, taxYear=2026)` returns the 20-row
v1 provisional seed (Lines 3–22 of IRS Schedule E (Form 1040)). Income
lines (3–4) + expense lines (5–19) reference exact account codes from
`DefaultChartTemplates.RentalRealEstate`; totals + result lines (20–22)
carry empty selector lists because they're computed by the Schedule E
generator.

**Provisional discipline:** every seeded row carries `IsProvisional = true`
+ a `ProvisionalRationale` pointing at the in-flight ONR (Office of
Notarial Research) regulatory input at
`icm/02_architecture/regulatory-us-rental-tax-input.md`. The forthcoming
`blocks-financial-tax-onr-ratification-addendum-stage06-handoff.md` flips
the bit + adjusts rows ONR specifies.

`ITaxFormLineMapStore.SeedScheduleEAsync` is **idempotent** — calling it
on an already-seeded `(chart, year)` pair returns `0` and preserves any
user edits.

## Property tax

**Property tax is not a `TaxCode`.** It's a recurring vendor bill via
`blocks-financial-ap`, mapped to **Schedule E Line 16** through the
chart-of-accounts code `6100` (Property Tax). The `TaxFormLineMap` row
for Line 16 declares `AccountCode: "6100"`; the Schedule E generator
picks up property-tax bills from the regular AP posting path. Don't add
`TaxKind.PropertyTax` to the enum.

## Events

The package emits five cross-cluster events via `IDomainEventPublisher`
wrapped in `DomainEventEnvelope<T>` (canonical envelope per
`_shared/engineering/cross-cluster-event-bus-design.md` §1):

| Event | Source operation | Idempotency key |
|---|---|---|
| `Financial.TaxCodeAdded` | `ITaxCodeStore.UpsertAsync` first-insert | `{type}|{tenant}|{codeId}|added` |
| `Financial.TaxCodeUpdated` | `ITaxCodeStore.UpsertAsync` re-upsert | `{type}|{tenant}|{codeId}|v{newVersion}` |
| `Financial.TaxRateAdded` | `ITaxRateLookup.UpsertAsync` + `SupersedeAsync` new-rate | `{type}|{tenant}|{rateId}|added` |
| `Financial.TaxRateExpired` | `ITaxRateLookup.SupersedeAsync` old-rate | `{type}|{tenant}|{rateId}|expired` |
| `Reports.TaxFormLineMapEdited` | `ITaxFormLineMapStore.UpsertAsync` real edit | `{type}|{tenant}|{mapId}|v{newVersion}` |

The `Reports.*` prefix on the last one is intentional even though the
storage lives in this package — the *consumer* is the reports-tax
cluster (event-bus §3.5).

The default DI registration is `NoopDomainEventPublisher`; the
canonical `Sunfish.Foundation.Events.IDomainEventPublisher` (when
foundation-events lands) is wired ahead of `AddBlocksFinancialTax()`
via `TryAddSingleton`, so the per-cluster sweep PR migrates every
cluster atomically.

## Journal posting integration

`IJournalTaxLineGenerator.GenerateAsync(preTaxLines, date, location)`
takes a set of pre-tax journal lines (each optionally carrying a
`TaxCodeId`) and returns the balanced union with one tax-payable line
per `TaxRateBreakdownLine`. Per-jurisdiction emission (rather than
aggregated per-code) is the canonical pattern per Stage 02 §6.4 — it
preserves the audit trail.

**Important:** This service does **not** modify
`JournalPostingService` — callers invoke it BEFORE constructing the
final `JournalEntry`. The posting service remains tax-agnostic per the
hand-off scope discipline.

## Quickstart

```csharp
// 1. Compose the package (in your app's Startup / Program.cs):
services.AddBlocksFinancialLedger();    // provides IAccountResolver
services.AddBlocksFinancialTax();

// 2. Seed a jurisdiction + a code + a rate (test-fixture only — production
//    seeding lands via an admin UI or the ERPNext importer):
var jurisdictionStore = sp.GetRequiredService<ITaxJurisdictionStore>();
var codeStore = sp.GetRequiredService<ITaxCodeStore>();
var rates = sp.GetRequiredService<ITaxRateLookup>();
var calc = sp.GetRequiredService<ITaxCalculationService>();

var virginia = TaxJurisdiction.Create(
    JurisdictionLevel.State, isoCountry: "US", name: "Virginia", region: "US-VA");
await jurisdictionStore.UpsertAsync(virginia);

var code = TaxCode.Create(
    chartId: chart, code: "US-VA-SALES", name: "Virginia sales tax",
    kind: TaxKind.Sales, application: TaxApplication.OnSubtotal);
await codeStore.UpsertAsync(code);

await rates.UpsertAsync(TaxRate.Create(
    code.Id, virginia.Id, ratePercent: 5.3m,
    effectiveDate: new DateOnly(2026, 1, 1), payableAccountId: salesTaxPayable.Id));

// 3. Calculate tax on a $100 line:
var result = await calc.CalculateAsync(new TaxCalculationInput(
    TaxCodeId: code.Id,
    Subtotal: 100m,
    TransactionDate: new DateOnly(2026, 6, 1),
    Location: new TaxLocationContext("US", "US-VA")));

// result.TaxAmount = 5.30m
// result.Breakdown[0].PayableAccountId = salesTaxPayable.Id
// result.Breakdown[0].TaxAmount = 5.30m
```

## Related

- [`blocks-financial-ledger`](../blocks-financial-ledger/overview.md) — chart of accounts, GL accounts, journal-entry posting; supplies `IAccountResolver`
- [`blocks-financial-ar`](../blocks-financial-ar/overview.md) — invoices + receipts; invokes `IJournalTaxLineGenerator` on posting
- [`blocks-financial-ap`](../blocks-financial-ap/overview.md) — bills (incl. property-tax recurring bills mapped to Schedule E Line 16)
- [`blocks-financial-periods`](../blocks-financial-periods/overview.md) — fiscal-period soft-close gating
- [`blocks-reports-tax`](../blocks-reports-tax/overview.md) — Schedule E generator + 1099 form rendering

## Known limitations / pending work

- v1 Schedule E mapping is **provisional** (ONR ratification pending; see `regulatory-us-rental-tax-input.md`).
- No automated property-tax-bill ingestion (Phase 2).
- No multi-currency conversion in tax calc (v1 = single currency per chart).
- SQLite persistence is in-memory in v1; persistent store lands with the foundation-localfirst SQLite write-path hand-off.
- 1099 form mappings (NEC + MISC) deferred until ONR + `blocks-people-*` ship.
- Canonical `IDomainEventPublisher` lives in this cluster locally until `foundation-events` ships; the per-cluster sweep PR migrates atomically.
