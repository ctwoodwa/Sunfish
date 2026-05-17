# Hand-off — `blocks-reports` read-side report cartridge cluster (Phase 3, MVP cut)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build` (gated — see §Gate conditions below)
**Workstream:** W#60 P4 — Path II native domain, reports cluster (Phase 3, MVP scope)
**Cluster:** `blocks-reports`
**Spec source:** [`icm/02_architecture/blocks-reports-schema-design.md`](../../02_architecture/blocks-reports-schema-design.md) §1 (posture), §4 (catalog of standard reports), §6 (rendering pipeline), §8 (Schedule E mapping), §9 (cross-cluster contracts), §10 (FOSS citations), §11 (open questions), §Appendix (Phase 1 cut)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) §1 (cluster grouping; `blocks-reports-*` Phase 1 MVP cluster), §2 (license posture), §3 (clean-room + classification discipline)
**Conventions:**
- [`icm/02_architecture/path-ii-crdt-schema-conventions.md`](../../02_architecture/path-ii-crdt-schema-conventions.md) — ULID, posted-then-immutable (snapshot markers), version+revisionVector (definitions only; reports are read-side and emit no domain mutations)
- [`icm/02_architecture/path-ii-cross-cluster-event-bus.md`](../../02_architecture/path-ii-cross-cluster-event-bus.md) — `DomainEventEnvelope<T>`; **note:** this cluster is a pure consumer; it does not emit `Reports.*` events in this hand-off (see §"Idempotency-key catalog" below)
- [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md) — §1 (ULID); §6 (posted-then-immutable applies to the *upstream* clusters reports read — reports themselves are stateless functions)
**Pipeline:** `sunfish-feature-change`
**Pipeline variant routing:** new feature (clean-room MIT) → standard `00_intake → 02_architecture → 03_package-design → 06_build → 07_review → 08_release` (Stage 02 done; this hand-off skips 03 + 05 because the cartridge contract is the only API surface and is specified here)
**Estimated effort:** ~12–16h sunfish-PM (7 PRs; ~70–90 tests + docs page + DI extension)
**PR count:** **7 PRs**
**Pre-merge council:**
- **security-engineering: OPTIONAL.** Reports are read-side; the only security concern is **tenant-isolation in parameter validation** — every cartridge MUST reject entity IDs (property/customer/vendor/account) that belong to a different tenant than the caller. **Spot-check on PR 1** (the substrate `IReportCartridge<,>` contract — the tenant-id binding convention is established once and inherited) is sufficient; per-cartridge PRs (2–6) do not require council unless a halt fires.
- **.NET architect: OPTIONAL except on PR 1 substrate.** The `IReportCartridge<TParams, TResult>` contract + `ReportCartridgeRegistry` + `IReportRunner` is the most-reused surface in this cluster — get it right once. A 30-minute architect spot-check on PR 1 before merge is the cluster's only mandatory architect review. PRs 2–7 are pattern-lift from PR 1.
- **No idempotency-bus council needed** (reports emit nothing).
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-reports"
grep -rn "Sunfish.Blocks.Reports" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages 2>/dev/null | head
```
Expected: nothing matching `blocks-reports`. The package name is greenfield. (Note: `blocks-tax-reporting/` already exists and is a Path I cluster; per Stage 02 §11 Q8 it is **NOT** the home for this work and is NOT touched by this hand-off. ADR 0088 names `blocks-reports-*` as the canonical Phase 1 cluster; the Path I `blocks-tax-reporting/` package retires separately under a future re-home hand-off.)

---

## Gate conditions

This hand-off is **gated on five predecessors**. COB must verify each before opening the gated PR. Some gates apply per-PR (not cluster-wide), so partial progress is allowed.

### Cluster-wide gates (must clear before PR 1)

1. **`foundation-events` shipped.** Provides `DomainEventEnvelope<T>` and the cross-cluster event bus surface. Reports do not *emit* events in this hand-off, but PR 1's cartridge contract references the `TenantId` strong type that lives in `foundation-events` (or its predecessor). If the type is in `foundation` itself, point at that instead.

2. **`blocks-financial-ledger` shipped.** Provides `IGeneralLedgerReadModel`, `IChartOfAccountsRepository`, `IJournalEntryRepository`, and the `GLAccountId` / `JournalEntryId` / `ChartOfAccountsId` strong types. PRs 2, 5 consume these directly.

### Per-PR gates

3. **`blocks-financial-ar` shipped** — gate for **PR 3** (AR Aging cartridge). Provides `IInvoiceRepository`, `IArAgingService`, `InvoiceId`, `ArAgingReport`, `ArAgingRow`.

4. **`blocks-financial-ap` shipped** — gate for **PR 4** (AP Aging cartridge). Provides `IBillRepository`, `IApAgingService`, `BillId`, `ApAgingReport`, `ApAgingRow`. The AP hand-off is currently `ready-to-build` (sibling). Per the sibling AP hand-off PASS gate, AP ships with all 4 PRs merged; PR 4 of THIS hand-off is gated on the AP hand-off's PR 4 (`IApAgingService` lands in AP's PR 3).

5. **`blocks-financial-periods` shipped** — gate for **PR 2** (Trial Balance cartridge) AND **PR 5** (P&L by Property cartridge). Provides `IFiscalPeriodRepository`, `FiscalPeriodId`, `FiscalPeriodStatus`. Trial Balance + P&L are period-locked: parameters bind a `FiscalPeriodId`; the cartridge MUST refuse to run if the period status is `Locked` and the requested `asOf` falls inside it — actually, the inverse: **Locked is the canonical "safe to report" state**; for `Open` or `SoftClosed` periods the cartridge returns a result but tags it `IsProvisional = true` so the UI can warn the user.

6. **`blocks-leases` already on main** — gate for **PR 6** (Rent Roll v2). Provides `ILeaseRepository` / `ILeaseService` / `Lease` / `LeaseId` / `PartyId`. Already on main (per the existing `packages/blocks-leases/` shipped via earlier W#60 phases). **Plus:** the canonical `blocks-people-foundation` Party (`IPartyReadModel` + `PartyId`) is preferred for tenant/owner lookup. If `blocks-people-foundation` is shipped at the time PR 6 opens, import it; if not, use the in-cluster `blocks-leases.PartyId` directly with a TODO comment for the cross-cluster relocation (mirrors the AR/AP pattern of local Party stubs).

If any cluster-wide gate is unmet, **STOP** — drop a `cob-question-*` beacon naming the unmet gate. If a per-PR gate is unmet, **skip that PR** and proceed to the next (PRs 2, 3, 4, 5, 6 are independent of one another after PR 1 is in; gate-deferred PRs land as follow-up commits or under a separate ledger row).

---

## Context

### Phase 3 critical-path position

Per ADR 0088 §1 + the Stage 02 design §Appendix, the Phase 1 reports MVP cut is the **closing slice of the Wave/Rentler/Mac-ERPNext replacement loop**. This hand-off does NOT ship the full Stage 02 §4 catalog (14 report kinds). It ships **7 read-side cartridges** that together satisfy the Phase 1 MVP requirement and establish the cartridge-substrate pattern that subsequent cartridges (Schedule E, Balance Sheet, Cash Flow, 1099 variants, Statement, the invoice/receipt/quote/bill PDFs, AR/AP statements) will follow in dedicated follow-on hand-offs.

```
blocks-financial-ledger    (Chart + Journal core)               ✓ shipped
blocks-financial-periods   (FiscalYear + FiscalPeriod)          ✓ shipped
blocks-financial-tax       (TaxCode + TaxRate + TaxJurisdiction)✓ shipped
blocks-financial-ar        (Invoice + InvoiceLine + AR aging)   ✓ shipped
blocks-financial-ap        (Bill + BillLine + AP aging)         (sibling — gate for PR 4)
blocks-financial-payments  (Payment + PaymentApplication)       (follow-on)
blocks-leases              (Lease + Tenant linkage)             ✓ shipped (Path I; consumed via interface)
blocks-people-foundation   (Party + PartyRole + IPartyReadModel)(sibling — optional gate)

blocks-reports             ← THIS HAND-OFF (substrate + 6 cartridges)
blocks-docs                ← parallel Phase 3 hand-off (independent; can run concurrently)
```

`blocks-reports` is **read-side only**. It owns:

1. The cartridge contract — `IReportCartridge<TParams, TResult>` — and the registry that resolves a `ReportKind` to a cartridge implementation.
2. The runner — `IReportRunner` — that validates params, captures a snapshot marker, dispatches to the cartridge, and returns a typed result.
3. Six concrete cartridges for the Phase 1 MVP cut (Trial Balance, AR Aging, AP Aging, P&L by Property, Rent Roll v2, plus a 7th DI/docs/exemplar PR).

It does NOT own (per Stage 02 §11 + this hand-off's scope cut):

- **PDF generation.** `@react-pdf/renderer` integration and template files (`schedule-e.tsx`, `invoice.tsx`, etc.) land in a separate `blocks-reports-pdf` hand-off (Phase 3 follow-on). This hand-off ships cartridges that return **structured result DTOs**; the UI / PDF layer renders those DTOs.
- **Chart rendering.** No SVG, no Recharts, no canvas. Cartridge results are tabular + summary numbers.
- **Caching beyond per-request memo.** No `mutationVersion` cache key, no `ReportArtifact` storage, no inline-vs-filesystem split. The Stage 02 §6.3 caching design is a **future hand-off**.
- **Report runs / persistence.** `ReportRun`, `ReportArtifact`, `ReportSchedule`, `ReportSubscription`, `Dashboard`, `DashboardWidget`, `KPI`, `KPISnapshot` entities from Stage 02 §3 are **all deferred** to follow-on hand-offs. Cartridges in this hand-off are stateless functions; each call is a fresh evaluation.
- **The `Report` definition entity and the user-authored `custom` report kind.** Phase 1 MVP ships only the system reports as direct cartridge implementations; user-authored composable reports land later.
- **The `TaxFormLineMap` entity + Schedule E generation.** Deferred to a `blocks-reports-tax` follow-on hand-off (per Stage 02 §11 Q8 — same future hand-off retires `blocks-tax-reporting/`).
- **PDF cartridges (Invoice/Receipt/Quote/Bill).** Deferred — these are presentation, not analysis. They land in `blocks-reports-pdf`.
- **Loro CRDT integration.** Report cartridge implementations are pure functions of (params + snapshot of upstream cluster state). No Loro-synced entities ship in this hand-off.

### Why MVP-cut and not full Stage 02

Stage 02 catalogs 14 report kinds and 12 schema entities (Report, ReportTemplate, ReportRun, ReportArtifact, ReportSchedule, ReportSubscription, Dashboard, DashboardWidget, WidgetDataSource, KPI, KPISnapshot, TaxFormLineMap). Building it whole would be a 4–6 week cluster, gated on PDF templating, scheduling infrastructure, and dashboard UX — none of which are needed to close the Wave-replacement loop.

The MVP cut prioritizes:

1. **The cartridge substrate** — once `IReportCartridge<,>` + `IReportRunner` + registry land, every future cartridge is an additive PR with no architectural decisions to make.
2. **The five reports CO needs to run a 4-LLC property business right now**: Trial Balance (to verify the chart balances), AR Aging (rent collection status), AP Aging (vendor obligations), P&L by Property (per-LLC profitability), Rent Roll v2 (current occupancy + rent collected; supersedes the v1 thin slice that shipped in W#60 Phase 5).
3. **The relocation of Rent Roll v1** — `@sunfish/contracts` shipped a thin Rent Roll DTO via PR #847; v2 here is the canonical implementation (see §"Rent Roll v1 → v2 migration" below).

Schedule E, the PDF cartridges, dashboards, scheduling, KPIs, statements all land in named follow-on hand-offs. **Each follow-on is an additive PR** that registers a new cartridge with the existing registry; no breaking changes to the substrate.

### Architecture summary (Stage 02 distillation)

#### `IReportCartridge<TParams, TResult>` — the contract

```csharp
public interface IReportCartridge<TParams, TResult>
    where TParams : class
    where TResult : class
{
    /// <summary>
    /// Stable identifier for this cartridge. Used by the runner to dispatch.
    /// Convention: kebab-case kind, e.g., "trial-balance", "ar-aging".
    /// </summary>
    ReportKind Kind { get; }

    /// <summary>
    /// Pure function: same inputs (params + snapshotMarker + tenant scope)
    /// MUST produce identical outputs. This invariant is testable; see §"Determinism" below.
    /// </summary>
    Task<TResult> ExecuteAsync(
        ReportExecutionContext context,
        TParams parameters,
        CancellationToken ct = default);
}
```

`ReportExecutionContext` provides:
- `TenantId` — bound by the runner; cartridges never accept loose tenant IDs.
- `SnapshotMarker` — captured at run start; passed to cluster read APIs for as-of-snapshot semantics.
- `AsOfUtc` — the wall-clock instant of run start; for "as of today" semantics.
- `PrincipalId` — the caller (for audit logging only; cartridges do not branch on principal).

**Key property:** there is **no `IDomainEventPublisher` ctor parameter on any cartridge.** The contract enforces read-side discipline at the type-system level — a cartridge cannot accidentally emit a mutation event because there is no injection point to do so.

#### `IReportRunner` — the dispatch surface

```csharp
public interface IReportRunner
{
    Task<ReportRunResult<TResult>> RunAsync<TParams, TResult>(
        ReportKind kind,
        TParams parameters,
        TenantId tenantId,
        PrincipalId requestedBy,
        CancellationToken ct = default)
        where TParams : class
        where TResult : class;
}

public sealed record ReportRunResult<TResult>(
    ReportKind Kind,
    TResult Result,
    Instant RunAtUtc,
    string SnapshotMarker,
    Duration RunDuration,
    bool IsProvisional,            // true if any upstream period was Open or SoftClosed
    IReadOnlyList<string> Warnings  // e.g., "Period 2026-04 is SoftClosed; values may shift on close"
);
```

The runner is the single entry point. It:
1. Resolves `kind` → `IReportCartridge<TParams, TResult>` via the registry (throws `UnknownReportKindException` if absent).
2. Validates `parameters` for tenant-scope (rejects entity IDs whose `tenantId` differs from `context.TenantId`).
3. Captures a snapshot marker (delegates to a cluster-supplied `ISnapshotMarkerSource`).
4. Constructs `ReportExecutionContext`.
5. Invokes the cartridge.
6. Returns a `ReportRunResult` with timing + provisional/warning metadata.

#### `ReportCartridgeRegistry` — kind → cartridge lookup

```csharp
public sealed class ReportCartridgeRegistry
{
    public void Register<TParams, TResult>(IReportCartridge<TParams, TResult> cartridge);
    public IReportCartridge<TParams, TResult> Resolve<TParams, TResult>(ReportKind kind);
    public bool TryResolve<TParams, TResult>(ReportKind kind, out IReportCartridge<TParams, TResult>? cartridge);
    public IReadOnlyList<ReportKind> RegisteredKinds { get; }
}
```

Registration is via DI; `AddBlocksReports()` calls `Register(...)` for each of the 6 built-in cartridges.

#### Determinism

The cartridge contract is **deterministic by design**: same `(params, snapshotMarker, tenantId)` MUST produce identical `TResult`. This is the cluster's single most important invariant and is **per-cartridge testable**:

```csharp
// Pattern (shipped in PR 1; lifted by PRs 2–6):
[Fact]
public async Task TrialBalance_IsDeterministic_AcrossRepeatedRuns()
{
    var fixture = await BuildFixtureAsync();
    var ctx = new ReportExecutionContext(tenantId, snapshotMarker, asOf, principalId);
    var first  = await cartridge.ExecuteAsync(ctx, parameters);
    var second = await cartridge.ExecuteAsync(ctx, parameters);
    Assert.Equal(first, second);  // sealed record value equality
}
```

Result DTOs are sealed records; equality is value-based; cartridges that don't satisfy this fail at the test level (caught early). Floating-point arithmetic is forbidden in result fields (monetary values are `decimal`); ordering must be deterministic (cartridges that sort by an `IEnumerable<>` MUST apply a stable secondary sort by ULID).

### CRDT-friendly conventions applied

Because reports own no mutable entities, the CRDT conventions apply to this cluster only as **read-side consumers**:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | Cartridge result DTOs use ULID-typed FKs (`PropertyId`, `LeaseId`, etc.) returned from upstream clusters; no new ULIDs are minted by this cluster |
| §2 Soft-delete tombstones | Cartridges read upstream tombstones and filter out soft-deleted rows by default (`includeDeleted: false` is the default parameter on every cartridge that returns entity rows; tests cover both the on and off paths) |
| §6 Posted-then-immutable | Snapshot-marker semantics — upstream cluster repositories honor the marker and return as-of state; cartridges call read APIs that do not mutate |
| §7 State-machine-under-CRDT | N/A — reports own no state machines |

### Rent Roll v1 → v2 migration

W#60 Phase 5 shipped a thin Rent Roll v1 in `@sunfish/contracts` (PR #847) and a Bridge endpoint `GET /api/v1/reports/rent-roll` (PR #848). That v1 is intentionally a **TypeScript-shaped, Bridge-fetched DTO** with a small column set (property / unit / tenant / lease-dates / monthly-rent / last-payment / balance / status). It served the immediate React-UI need without taking on the cartridge substrate.

v2 in `blocks-reports` is the **canonical .NET implementation**. It:

- Lives in `Sunfish.Blocks.Reports.Cartridges.RentRoll`.
- Returns a richer `RentRollResult` DTO with: prepaid balance, delinquency aging bucket, projected next-month rent, lease-expiration-in-window flag, vacancy reason, and per-property + per-portfolio rollups.
- Reads from `ILeaseRepository` directly (no Bridge round-trip).
- Honors as-of-date (v1 is implicitly today-only).
- Is tenant-scoped at the cartridge surface (v1 inherits tenant scope from the Bridge auth context; v2 makes it explicit + testable).

**Migration path:**

1. **This hand-off:** ship v2 in `blocks-reports`. Both v1 and v2 coexist on main.
2. **Follow-on (sunfish-PM, separate PR after this hand-off's ledger flip):** the React UI's `RentRoll.tsx` page is rewired to call v2 via a new Bridge endpoint that delegates to `IReportRunner.RunAsync<RentRollParameters, RentRollResult>(...)`. v1's `GET /api/v1/reports/rent-roll` endpoint is retained but marked `[Obsolete]` and re-implemented as a thin adapter that calls v2 and projects down to the v1 column set.
3. **Follow-on (one minor version after step 2 — say 0.3.x → 0.4.x):** v1's `@sunfish/contracts` Rent Roll types are deleted; v1's Bridge endpoint returns `410 Gone` with a migration note. The DTO surface is fully canonical.

The retirement schedule is documented in the cluster's `apps/docs/blocks/reports/README.md` (PR 7). **This hand-off does NOT touch `@sunfish/contracts` or the Bridge endpoint;** the rewire is its own follow-on hand-off authored after PR 7 merges.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify `blocks-financial-ledger` is built.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Services/IGeneralLedgerReadModel.cs 2>&1
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Models/GLAccountId.cs 2>&1
   ```
   Expected: both exist. If absent, halt + file `cob-question-*` naming the missing predecessor.

2. **Verify `foundation-events` is built (or `TenantId` is in `foundation`).**
   ```bash
   grep -rln "record struct TenantId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/foundation* 2>/dev/null
   ```
   Expected: at least one hit. If absent, use the cluster-local `TenantId` shim convention from the AR hand-off and file a NOTICE comment in PR 1's `ReportExecutionContext.cs`.

3. **Verify no parallel session is touching `blocks-reports*`.**
   ```bash
   gh pr list --state open --search "blocks-reports in:title,body"
   gh pr list --state open --search "Sunfish.Blocks.Reports in:title,body"
   ```
   Expected: empty. If anything else is open, file `cob-question-*`.

4. **Verify no `Sunfish.Blocks.Reports` namespace exists.**
   ```bash
   grep -rn "Sunfish.Blocks.Reports" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages 2>/dev/null | head
   ```
   Expected: zero hits. Greenfield package.

5. **Verify per-PR gates.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-periods/Services/IFiscalPeriodRepository.cs 2>&1   # PR 2, PR 5
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ar/Services/IArAgingService.cs 2>&1                # PR 3
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ap/Services/IApAgingService.cs 2>&1                # PR 4
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Services/ILeaseService.cs 2>&1                        # PR 6 (already on main)
   ```
   Record which gates are unmet; the affected PRs are deferred or skipped, not blocked-from-merge.

6. **Confirm ADR 0088 is accepted (or operative).**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `Proposed` or `Accepted`. The hand-off is `ready-to-build` regardless (CO directive operative; sibling-hand-off precedent).

7. **Clean-room discipline — no copyleft sources open.**
   Per Stage 02 §11 + ADR 0088 §3.2: before any PR work, **close any editor session** that has Beancount, GnuCash, ERPNext, Akaunting, or Metabase source open. The cartridge formulas in this hand-off are derived from:
   - Standard accounting formulas (Trial Balance = sum of debits − sum of credits per account; balance is reported by side per account-type).
   - Aging bucket boundaries from GnuCash convention (0/30/60/90+ — uncopyrightable convention; cite GnuCash as inspiration only).
   - Property P&L by dimension from ERPNext cost-center grouping pattern (uncopyrightable taxonomy).
   - Rent roll column set from ERPNext `rent_roll` + the v1 Rent Roll already shipped in `@sunfish/contracts` (Sunfish original).

8. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

---

## Per-PR deliverables

This hand-off splits into **7 PRs** by responsibility:

| PR | Scope | Effort | Gate |
|---|---|---|---|
| PR 1 | Package scaffold + `IReportCartridge<,>` + `IReportRunner` + `ReportCartridgeRegistry` + `ReportExecutionContext` + `ISnapshotMarkerSource` stub + base classes | ~3h | foundation-events + blocks-financial-ledger |
| PR 2 | Trial Balance cartridge — `TrialBalanceCartridge` + `TrialBalanceParameters` + `TrialBalanceResult` | ~2h | PR 1 + blocks-financial-periods |
| PR 3 | AR Aging Summary cartridge — `ArAgingSummaryCartridge` + per-property + per-customer rollups | ~1.5h | PR 1 + blocks-financial-ar |
| PR 4 | AP Aging Summary cartridge — `ApAgingSummaryCartridge` + per-property + per-vendor rollups | ~1.5h | PR 1 + **blocks-financial-ap (sibling — gate)** |
| PR 5 | P&L by Property cartridge — `ProfitAndLossByPropertyCartridge` + per-period dimensional aggregation | ~2.5h | PR 1 + PR 2 (Trial Balance lessons applied) |
| PR 6 | Rent Roll v2 cartridge — `RentRollCartridge` + current + projected + aging + vacancy | ~2.5h | PR 1 + blocks-leases (on main) |
| PR 7 | `AddBlocksReports()` DI extension + `apps/docs/blocks/reports/README.md` + Rent Roll v1→v2 migration note + cluster-level acceptance | ~1.5h | PRs 1–6 merged |

**Sequence:** PR 1 is strictly first. PRs 2–6 can land in any order after PR 1 is in, gated by their per-PR predecessors. PR 7 requires all of PRs 1–6 merged.

**Parallelization:** PRs 3, 4, 5, 6 can be authored concurrently after PR 1 is in. PR 2 (Trial Balance) is intentionally before PR 5 (P&L) because P&L's account-type-grouping pattern is a generalization of Trial Balance's account-side-grouping; lifting the Trial Balance pattern reduces P&L's design surface.

---

### PR 1 — Package scaffold + cartridge substrate

**Estimated effort:** ~3h
**Scope:** new package `blocks-reports`; `IReportCartridge<,>`; `IReportRunner` + `ReportRunner`; `ReportCartridgeRegistry`; `ReportExecutionContext`; `ReportKind` enum; `ISnapshotMarkerSource` + `MutableSnapshotMarkerSource` test fake; base exception types; substrate DI registration (no cartridges yet); package README
**Commit subject:** `feat(blocks-reports): scaffold cluster with IReportCartridge substrate + runner + registry per Stage 02 §6.1`
**Branch:** `cob/blocks-reports-substrate`
**Pre-merge council:** **security-engineering spot-check** (tenant-id binding convention) + **.NET architect spot-check** (cartridge contract surface) — both 30-minute reviews on the PR before merge. Halt and file `cob-question-*` if either reviewer requests substantive shape changes; this is the only PR in the cluster with mandatory architect review.

#### Package skeleton

```
packages/blocks-reports/
├── README.md
├── NOTICE.md                                       (clean-room attribution table — see §License posture)
├── Sunfish.Blocks.Reports.csproj
├── Models/
│   ├── ReportKind.cs                               (enum of cartridge kinds — exhaustive)
│   ├── ReportExecutionContext.cs                   (tenant + snapshot + asOf + principal)
│   ├── ReportRunResult.cs                          (generic result envelope)
│   └── ReportProvisionality.cs                     (struct: IsProvisional + Warnings list)
├── Services/
│   ├── IReportCartridge.cs                         (generic contract)
│   ├── IReportRunner.cs
│   ├── ReportRunner.cs                             (canonical implementation)
│   ├── ReportCartridgeRegistry.cs
│   ├── ISnapshotMarkerSource.cs                    (delegated; cluster-supplied)
│   ├── InMemorySnapshotMarkerSource.cs             (test fake; monotonic counter)
│   ├── ReportRunnerOptions.cs                      (timeout + warnings cap)
│   └── Exceptions/
│       ├── UnknownReportKindException.cs
│       ├── ReportParameterValidationException.cs
│       └── ReportCartridgeExecutionException.cs    (wraps inner exceptions for runner consumers)
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs              (AddBlocksReportsSubstrate(); cartridge registrations in PR 7)
└── tests/
    ├── Sunfish.Blocks.Reports.Tests.csproj
    ├── ReportKindTests.cs
    ├── ReportCartridgeRegistryTests.cs
    ├── ReportRunnerTests.cs
    ├── ReportExecutionContextTests.cs
    └── DeterminismHarnessTests.cs                  (shared test base; lifted by PRs 2–6 to assert per-cartridge determinism)
```

#### `Models/ReportKind.cs`

```csharp
namespace Sunfish.Blocks.Reports;

public enum ReportKind
{
    TrialBalance,             // PR 2
    ArAgingSummary,           // PR 3
    ApAgingSummary,           // PR 4
    ProfitAndLossByProperty,  // PR 5
    RentRoll,                 // PR 6
    // Reserved for follow-on hand-offs (do NOT remove this comment):
    //   BalanceSheet, CashFlow, Statement, ScheduleE,
    //   Form1099Nec, Form1099Misc, WorkOrderSummary, MaintenanceBacklog,
    //   LeaseExpiration, Vacancy, InvoicePdf, ReceiptPdf, QuotePdf, BillPdf
}

public static class ReportKindExtensions
{
    public static string ToKebab(this ReportKind kind) => kind switch
    {
        ReportKind.TrialBalance            => "trial-balance",
        ReportKind.ArAgingSummary          => "ar-aging-summary",
        ReportKind.ApAgingSummary          => "ap-aging-summary",
        ReportKind.ProfitAndLossByProperty => "profit-and-loss-by-property",
        ReportKind.RentRoll                => "rent-roll",
        _ => throw new InvalidOperationException($"Unmapped ReportKind: {kind}"),
    };
}
```

Adding a new cartridge in a follow-on hand-off = add an enum member + the `ToKebab` case. No other contract change.

#### `Models/ReportExecutionContext.cs`

```csharp
namespace Sunfish.Blocks.Reports;

using NodaTime;

public sealed record ReportExecutionContext(
    TenantId TenantId,
    string SnapshotMarker,    // opaque to cartridges; passed verbatim to upstream cluster read APIs
    Instant AsOfUtc,
    PrincipalId RequestedBy);
```

`TenantId` and `PrincipalId` come from `foundation-events` (or `foundation` if `-events` is not yet on main — see pre-build §2). **Tenant isolation invariant:** every cartridge MUST treat `context.TenantId` as the **sole tenant scope** for its execution; cartridge parameters that include entity IDs MUST validate those IDs belong to the same tenant.

#### `Models/ReportRunResult.cs`

```csharp
namespace Sunfish.Blocks.Reports;

using NodaTime;

public sealed record ReportRunResult<TResult>(
    ReportKind Kind,
    TResult Result,
    Instant RunAtUtc,
    string SnapshotMarker,
    Duration RunDuration,
    bool IsProvisional,
    IReadOnlyList<string> Warnings)
    where TResult : class;
```

#### `Services/IReportCartridge.cs`

```csharp
namespace Sunfish.Blocks.Reports;

public interface IReportCartridge<TParams, TResult>
    where TParams : class
    where TResult : class
{
    ReportKind Kind { get; }

    Task<TResult> ExecuteAsync(
        ReportExecutionContext context,
        TParams parameters,
        CancellationToken ct = default);
}
```

**No `IDomainEventPublisher` injection allowed.** This is the read-side discipline at compile time. Code review on every cartridge MUST reject any ctor injection of a publisher / repository-with-write-surface.

#### `Services/ReportCartridgeRegistry.cs`

```csharp
namespace Sunfish.Blocks.Reports;

public sealed class ReportCartridgeRegistry
{
    private readonly Dictionary<(ReportKind kind, Type paramsType, Type resultType), object> _cartridges = new();

    public void Register<TParams, TResult>(IReportCartridge<TParams, TResult> cartridge)
        where TParams : class
        where TResult : class
    {
        var key = (cartridge.Kind, typeof(TParams), typeof(TResult));
        if (_cartridges.ContainsKey(key))
            throw new InvalidOperationException($"Cartridge already registered for {key}");
        _cartridges[key] = cartridge;
    }

    public IReportCartridge<TParams, TResult> Resolve<TParams, TResult>(ReportKind kind)
        where TParams : class
        where TResult : class
    {
        if (_cartridges.TryGetValue((kind, typeof(TParams), typeof(TResult)), out var cartridge))
            return (IReportCartridge<TParams, TResult>)cartridge;
        throw new UnknownReportKindException(kind, typeof(TParams), typeof(TResult));
    }

    public bool TryResolve<TParams, TResult>(ReportKind kind, out IReportCartridge<TParams, TResult>? cartridge)
        where TParams : class
        where TResult : class
    {
        if (_cartridges.TryGetValue((kind, typeof(TParams), typeof(TResult)), out var raw))
        {
            cartridge = (IReportCartridge<TParams, TResult>)raw;
            return true;
        }
        cartridge = null;
        return false;
    }

    public IReadOnlyList<ReportKind> RegisteredKinds =>
        _cartridges.Keys.Select(k => k.kind).Distinct().ToList();
}
```

Keying by `(kind, paramsType, resultType)` defends against accidental param/result-type mismatch at registration time — a common bug source in generic-dispatch registries.

#### `Services/IReportRunner.cs` + `ReportRunner.cs`

```csharp
namespace Sunfish.Blocks.Reports;

using NodaTime;

public interface IReportRunner
{
    Task<ReportRunResult<TResult>> RunAsync<TParams, TResult>(
        ReportKind kind,
        TParams parameters,
        TenantId tenantId,
        PrincipalId requestedBy,
        CancellationToken ct = default)
        where TParams : class
        where TResult : class;
}

public sealed class ReportRunner : IReportRunner
{
    private readonly ReportCartridgeRegistry _registry;
    private readonly ISnapshotMarkerSource _markers;
    private readonly IClock _clock;
    private readonly ReportRunnerOptions _options;

    public ReportRunner(
        ReportCartridgeRegistry registry,
        ISnapshotMarkerSource markers,
        IClock clock,
        ReportRunnerOptions options)
    {
        _registry = registry;
        _markers = markers;
        _clock = clock;
        _options = options;
    }

    public async Task<ReportRunResult<TResult>> RunAsync<TParams, TResult>(
        ReportKind kind,
        TParams parameters,
        TenantId tenantId,
        PrincipalId requestedBy,
        CancellationToken ct = default)
        where TParams : class
        where TResult : class
    {
        var cartridge = _registry.Resolve<TParams, TResult>(kind);

        var asOfUtc = _clock.GetCurrentInstant();
        var marker = await _markers.CaptureAsync(tenantId, ct);
        var ctx = new ReportExecutionContext(tenantId, marker, asOfUtc, requestedBy);

        var startedAt = asOfUtc;
        TResult result;
        try
        {
            result = await cartridge.ExecuteAsync(ctx, parameters, ct);
        }
        catch (ReportParameterValidationException) { throw; }
        catch (Exception ex)
        {
            throw new ReportCartridgeExecutionException(kind, ex);
        }
        var endedAt = _clock.GetCurrentInstant();

        // Provisionality + warnings: cartridges that consume Open / SoftClosed periods
        // attach warnings via an internal context channel (PR 2 adds the channel).
        var (isProvisional, warnings) = ExtractProvisionality(result);

        return new ReportRunResult<TResult>(
            Kind: kind,
            Result: result,
            RunAtUtc: startedAt,
            SnapshotMarker: marker,
            RunDuration: endedAt - startedAt,
            IsProvisional: isProvisional,
            Warnings: warnings);
    }

    private static (bool, IReadOnlyList<string>) ExtractProvisionality<TResult>(TResult result)
    {
        // PR 1 ships a stub: results that implement IReportProvisionalityCarrier report directly;
        // otherwise (false, []). PR 2 introduces the interface; cartridges that need it implement it.
        if (result is IReportProvisionalityCarrier carrier)
            return (carrier.IsProvisional, carrier.Warnings);
        return (false, Array.Empty<string>());
    }
}

public interface IReportProvisionalityCarrier
{
    bool IsProvisional { get; }
    IReadOnlyList<string> Warnings { get; }
}
```

#### `Services/ISnapshotMarkerSource.cs` + `InMemorySnapshotMarkerSource.cs`

```csharp
public interface ISnapshotMarkerSource
{
    Task<string> CaptureAsync(TenantId tenantId, CancellationToken ct = default);
}

// Test fake (registered by AddBlocksReportsSubstrate by default; real impl is wired in
// PR 7's AddBlocksReports when a per-cluster marker source is available — initial release
// uses InMemorySnapshotMarkerSource since upstream cluster snapshot support is itself
// follow-on work).
public sealed class InMemorySnapshotMarkerSource : ISnapshotMarkerSource
{
    private long _counter = 0;
    public Task<string> CaptureAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var c = Interlocked.Increment(ref _counter);
        return Task.FromResult($"inmem:{tenantId.Value}:{c}");
    }
}
```

**Note on snapshot markers:** the real wal-position + Loro-version-vector marker described in Stage 02 §6.1 step 3 is not in scope for this hand-off. Upstream cluster read APIs currently ignore the marker argument; cartridges pass it through unchanged. When the per-cluster marker honor lands (future hand-off), cartridges automatically get coherent snapshots without any code change at this layer.

#### `Services/ReportRunnerOptions.cs`

```csharp
public sealed class ReportRunnerOptions
{
    /// <summary>Maximum number of warnings to attach to a ReportRunResult before truncation.</summary>
    public int MaxWarnings { get; set; } = 32;

    /// <summary>
    /// Hard timeout for any single cartridge execution. Beyond this, the runner cancels
    /// and throws ReportCartridgeExecutionException. Per Stage 02 §11 Q10 — Phase 1 default is 60s.
    /// </summary>
    public Duration HardTimeout { get; set; } = Duration.FromSeconds(60);
}
```

#### `DependencyInjection/ServiceCollectionExtensions.cs`

```csharp
namespace Sunfish.Blocks.Reports.DependencyInjection;

public static class ReportSubstrateServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cartridge substrate (registry + runner + snapshot marker stub).
    /// Does NOT register any cartridges. Cartridges register themselves via AddBlocksReports()
    /// in PR 7 or via per-cartridge AddXxxCartridge() extensions.
    /// </summary>
    public static IServiceCollection AddBlocksReportsSubstrate(
        this IServiceCollection services,
        Action<ReportRunnerOptions>? configure = null)
    {
        services.AddSingleton<ReportCartridgeRegistry>();
        services.AddSingleton<ISnapshotMarkerSource, InMemorySnapshotMarkerSource>();
        services.TryAddSingleton<IClock>(SystemClock.Instance);

        var options = new ReportRunnerOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<IReportRunner, ReportRunner>();
        return services;
    }
}
```

#### Tests (PR 1)

`tests/ReportKindTests.cs`:

- `ReportKind_HasAtLeastFiveMvpMembers`.
- `ReportKindExtensions_ToKebab_AllMembersMapped`.
- `ReportKindExtensions_ToKebab_ProducesLowercaseKebabIdentifiers`.

`tests/ReportCartridgeRegistryTests.cs`:

- `Register_NewKind_Succeeds`.
- `Register_DuplicateKey_Throws`.
- `Resolve_RegisteredKind_ReturnsCartridge`.
- `Resolve_UnregisteredKind_ThrowsUnknownReportKindException`.
- `Resolve_RegisteredKindWithWrongParamsType_ThrowsUnknownReportKindException` (type-mismatch is treated as not-registered, not a misroute).
- `TryResolve_UnregisteredKind_ReturnsFalseAndNullOut`.
- `RegisteredKinds_ReturnsDistinctSet`.

`tests/ReportRunnerTests.cs`:

- `RunAsync_DispatchesToRegisteredCartridge`.
- `RunAsync_UnknownKind_ThrowsUnknownReportKindException`.
- `RunAsync_BindsTenantIdIntoContext` (cartridge fake asserts `context.TenantId == passedIn`).
- `RunAsync_CapturesSnapshotMarkerBeforeCartridgeInvocation` (fake source increments a counter; assert non-empty marker).
- `RunAsync_BindsAsOfUtcFromClock`.
- `RunAsync_BindsPrincipalIdIntoContext`.
- `RunAsync_PropagatesCancellationTokenToCartridge`.
- `RunAsync_PopulatesRunDuration` (asserts `Duration > Zero`).
- `RunAsync_PopulatesSnapshotMarkerInResult` (asserts marker is the same as the one captured).
- `RunAsync_ResultImplementingProvisionalityCarrier_PropagatesIsProvisionalAndWarnings`.
- `RunAsync_ResultWithoutProvisionalityCarrier_DefaultsToFalseAndEmpty`.
- `RunAsync_CartridgeThrows_WrapsInReportCartridgeExecutionException`.
- `RunAsync_ParameterValidationException_PassesThroughUnwrapped` (callers see the original exception type).

`tests/ReportExecutionContextTests.cs`:

- `Context_Equality_IsValueBased` (sealed record equality).
- `Context_FieldsAreReadOnly` (compile-time enforced; smoke).

`tests/DeterminismHarnessTests.cs` — shared test base used by PRs 2–6:

```csharp
public abstract class ReportCartridgeDeterminismTests<TCartridge, TParams, TResult>
    where TCartridge : IReportCartridge<TParams, TResult>
    where TParams : class
    where TResult : class
{
    protected abstract TCartridge BuildCartridge();
    protected abstract TParams BuildParameters();
    protected abstract ReportExecutionContext BuildContext();

    [Fact]
    public async Task ExecuteAsync_IsDeterministic_AcrossRepeatedRuns()
    {
        var cartridge = BuildCartridge();
        var ctx = BuildContext();
        var p = BuildParameters();
        var r1 = await cartridge.ExecuteAsync(ctx, p);
        var r2 = await cartridge.ExecuteAsync(ctx, p);
        Assert.Equal(r1, r2);  // sealed record value equality
    }

    [Fact]
    public async Task ExecuteAsync_DifferentSnapshotMarkers_MayProduceDifferentResults_ButSameMarkerSameResult()
    {
        // Asserts the documented contract: snapshot marker is the sole upstream-state input.
        // Two runs with the same marker must produce equal results (covered above).
        // This test is a documentation test; it only asserts the same-marker-same-result property.
        var cartridge = BuildCartridge();
        var p = BuildParameters();

        var ctx1 = BuildContext();
        var ctx2 = ctx1 with { /* same fields; different object */ };
        var r1 = await cartridge.ExecuteAsync(ctx1, p);
        var r2 = await cartridge.ExecuteAsync(ctx2, p);
        Assert.Equal(r1, r2);
    }
}
```

Each cartridge PR (2–6) derives a concrete `XxxCartridgeDeterminismTests` from this base — three lines of per-cartridge wiring; the determinism invariant is enforced cluster-wide.

Total new tests this PR: ~25.

#### Verification

- `dotnet build` succeeds on the new package.
- All new tests pass.
- `AddBlocksReportsSubstrate()` resolves `IReportRunner` from `IServiceProvider` without runtime error.
- The cartridge contract surface compiles even with **no cartridges registered** (substrate-only state).

#### Do NOT in this PR

- Do NOT register any cartridge. PRs 2–6 each register their own; PR 7 ships the convenience `AddBlocksReports()` umbrella that calls all six.
- Do NOT add PDF generation, template files, or any presentation-layer logic.
- Do NOT add `ReportRun`, `ReportArtifact`, persistence, scheduling, or any of the Stage 02 §3 entities. All deferred.
- Do NOT add per-cartridge unique idempotency keys or event emission. Reports emit nothing.
- Do NOT couple to any specific upstream cluster yet. PR 1 must compile with `blocks-financial-ledger` as the only project reference (for `TenantId`/`PrincipalId` if they live there; otherwise zero upstream-cluster refs).

---

### PR 2 — Trial Balance cartridge

**Estimated effort:** ~2h
**Scope:** `TrialBalanceCartridge` + `TrialBalanceParameters` + `TrialBalanceResult` + period-aware as-of-date; consumes `IGeneralLedgerReadModel` from `blocks-financial-ledger` and `IFiscalPeriodRepository` from `blocks-financial-periods`
**Commit subject:** `feat(blocks-reports): TrialBalance cartridge per Stage 02 §4.2 (read-side; period-aware)`
**Depends on:** PR 1 merged + `blocks-financial-periods` on main
**Branch:** `cob/blocks-reports-trial-balance`

#### What Trial Balance is

A Trial Balance is the period-end (or as-of-date) listing of every active GL account with its closing balance, split into Debit-side and Credit-side columns. Total of Debit column MUST equal total of Credit column; if they don't, the chart is unbalanced (a posting bug).

Standard formula:

```
For each Account a:
  raw_balance(a, asOf) = sum of debits posted on or before asOf minus sum of credits, per a.id
  If a.NormalSide == Debit:  show in Debit column if raw_balance > 0, else in Credit column with absolute value
  If a.NormalSide == Credit: show in Credit column if raw_balance < 0, else in Debit column with absolute value
Result.TotalDebit  = sum of Debit-column values
Result.TotalCredit = sum of Credit-column values
Result.IsBalanced  = (TotalDebit == TotalCredit)
```

#### Parameters

```csharp
namespace Sunfish.Blocks.Reports.Cartridges.TrialBalance;

public sealed record TrialBalanceParameters
{
    public required ChartOfAccountsId ChartId { get; init; }

    /// <summary>
    /// Either bind a FiscalPeriodId (uses period.EndDate as asOf) or provide an explicit AsOfDate.
    /// Exactly one of (FiscalPeriodId, AsOfDate) must be set; setting both is a parameter validation error.
    /// </summary>
    public FiscalPeriodId? FiscalPeriodId { get; init; }
    public DateOnly? AsOfDate { get; init; }

    /// <summary>If true, accounts with zero balance are included in the result (default false).</summary>
    public bool IncludeZeroBalanceAccounts { get; init; } = false;

    /// <summary>If true, soft-deleted accounts (tombstoned in chart) are included (default false).</summary>
    public bool IncludeDeletedAccounts { get; init; } = false;
}
```

#### Result

```csharp
public sealed record TrialBalanceResult(
    ChartOfAccountsId ChartId,
    DateOnly AsOf,
    FiscalPeriodId? PeriodId,
    IReadOnlyList<TrialBalanceRow> Rows,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    bool IsProvisional,
    IReadOnlyList<string> Warnings)
    : IReportProvisionalityCarrier;

public sealed record TrialBalanceRow(
    GLAccountId AccountId,
    string AccountCode,
    string AccountName,
    AccountType AccountType,
    decimal DebitBalance,    // 0 if account balance is on credit side
    decimal CreditBalance);  // 0 if account balance is on debit side
```

`TrialBalanceRow` ordering: ascending by `AccountCode` (lexicographic), then ascending by `AccountId.ToString()` as stable tie-breaker.

#### Cartridge implementation sketch

```csharp
public sealed class TrialBalanceCartridge : IReportCartridge<TrialBalanceParameters, TrialBalanceResult>
{
    public ReportKind Kind => ReportKind.TrialBalance;

    private readonly IChartOfAccountsRepository _charts;
    private readonly IGeneralLedgerReadModel _ledger;
    private readonly IFiscalPeriodRepository _periods;

    public TrialBalanceCartridge(
        IChartOfAccountsRepository charts,
        IGeneralLedgerReadModel ledger,
        IFiscalPeriodRepository periods)
    {
        _charts = charts;
        _ledger = ledger;
        _periods = periods;
    }

    public async Task<TrialBalanceResult> ExecuteAsync(
        ReportExecutionContext context,
        TrialBalanceParameters parameters,
        CancellationToken ct = default)
    {
        // 1. Parameter validation
        if (parameters.FiscalPeriodId is null && parameters.AsOfDate is null)
            throw new ReportParameterValidationException(
                "TrialBalance requires either FiscalPeriodId or AsOfDate.");
        if (parameters.FiscalPeriodId is not null && parameters.AsOfDate is not null)
            throw new ReportParameterValidationException(
                "TrialBalance accepts FiscalPeriodId OR AsOfDate, not both.");

        // 2. Tenant-scope the chart
        var chart = await _charts.GetByIdAsync(parameters.ChartId, ct);
        if (chart is null || chart.TenantId != context.TenantId)
            throw new ReportParameterValidationException(
                $"ChartId {parameters.ChartId} not found in tenant {context.TenantId}.");

        // 3. Resolve as-of date + provisionality
        var warnings = new List<string>();
        bool isProvisional = false;
        DateOnly asOf;
        FiscalPeriodId? periodId = parameters.FiscalPeriodId;
        if (periodId is not null)
        {
            var period = await _periods.GetByIdAsync(periodId.Value, ct);
            if (period is null || period.TenantId != context.TenantId)
                throw new ReportParameterValidationException(
                    $"FiscalPeriodId {periodId} not found in tenant {context.TenantId}.");
            asOf = period.EndDate;
            if (period.Status != FiscalPeriodStatus.Locked)
            {
                isProvisional = true;
                warnings.Add($"Period {period.Label} is {period.Status}; values may shift on close.");
            }
        }
        else
        {
            asOf = parameters.AsOfDate!.Value;
        }

        // 4. Read accounts (filter by tombstone + include-zero)
        var accounts = await _charts.GetAccountsAsync(
            parameters.ChartId,
            includeDeleted: parameters.IncludeDeletedAccounts,
            ct);

        // 5. Read raw balances (delegates to ledger read model with snapshot marker)
        var balances = await _ledger.GetAccountBalancesAsOfAsync(
            parameters.ChartId, asOf, context.SnapshotMarker, ct);
        // balances: IReadOnlyDictionary<GLAccountId, decimal>  (raw debit - credit signed)

        // 6. Compose rows
        var rows = new List<TrialBalanceRow>();
        decimal totalDebit = 0m, totalCredit = 0m;
        foreach (var account in accounts.OrderBy(a => a.Code, StringComparer.Ordinal)
                                        .ThenBy(a => a.Id.ToString(), StringComparer.Ordinal))
        {
            balances.TryGetValue(account.Id, out var raw);
            if (raw == 0m && !parameters.IncludeZeroBalanceAccounts) continue;

            var (debit, credit) = ProjectToSides(account.NormalSide, raw);
            rows.Add(new TrialBalanceRow(
                AccountId:    account.Id,
                AccountCode:  account.Code,
                AccountName:  account.Name,
                AccountType:  account.Type,
                DebitBalance: debit,
                CreditBalance: credit));
            totalDebit  += debit;
            totalCredit += credit;
        }

        var isBalanced = totalDebit == totalCredit;
        if (!isBalanced)
            warnings.Add($"Chart is unbalanced: Debit {totalDebit:N2} != Credit {totalCredit:N2}.");

        return new TrialBalanceResult(
            ChartId:        parameters.ChartId,
            AsOf:           asOf,
            PeriodId:       periodId,
            Rows:           rows,
            TotalDebit:     totalDebit,
            TotalCredit:    totalCredit,
            IsBalanced:     isBalanced,
            IsProvisional:  isProvisional,
            Warnings:       warnings);
    }

    private static (decimal debit, decimal credit) ProjectToSides(NormalBalanceSide side, decimal raw)
    {
        // raw is signed (debit positive); project per normal side.
        return side switch
        {
            NormalBalanceSide.Debit  when raw >= 0 => (raw, 0m),
            NormalBalanceSide.Debit  when raw <  0 => (0m, -raw),
            NormalBalanceSide.Credit when raw <= 0 => (0m, -raw),
            NormalBalanceSide.Credit when raw >  0 => (raw, 0m),
            _ => (0m, 0m),
        };
    }
}
```

#### DI

```csharp
public static IServiceCollection AddTrialBalanceCartridge(this IServiceCollection services)
{
    services.AddSingleton<TrialBalanceCartridge>();
    services.AddSingleton<IReportCartridge<TrialBalanceParameters, TrialBalanceResult>>(sp => sp.GetRequiredService<TrialBalanceCartridge>());
    // Eagerly register with the cartridge registry on host startup:
    services.AddSingleton<ICartridgeRegistrar>(sp =>
        new CartridgeRegistrar<TrialBalanceParameters, TrialBalanceResult>(sp.GetRequiredService<TrialBalanceCartridge>()));
    return services;
}
```

The `ICartridgeRegistrar` indirection (also added in this PR) is the convention every cartridge follows so that a single startup task drains the registrars into the registry. PR 1 ships the `ICartridgeRegistrar` interface + the host-startup invocation pattern; this PR ships the first concrete registrar.

#### Tests (PR 2)

`tests/TrialBalanceCartridgeTests.cs`:

- **Edge case — empty chart:** `TrialBalance_EmptyChart_ReturnsZeroTotalsAndEmptyRows`.
- **Edge case — single record:** `TrialBalance_SingleAccountWithBalance_AppearsInCorrectSideColumn`.
- **Edge case — single record, normal-side debit, negative balance:** `TrialBalance_DebitNormalAccountWithNegativeBalance_ShowsAsCredit`.
- **Edge case — single record, normal-side credit, negative balance:** `TrialBalance_CreditNormalAccountWithCreditBalance_ShowsAsCredit`.
- **Edge case — period boundary:** `TrialBalance_AsOfDateMatchesPeriodEnd_IncludesPostingsOnThatDate`.
- **Edge case — period boundary:** `TrialBalance_AsOfDateOneDayBeforePeriodEnd_ExcludesPostingsOnPeriodEnd`.
- **Tenant-isolation:** `TrialBalance_ChartFromDifferentTenant_ThrowsReportParameterValidationException`.
- **Tenant-isolation:** `TrialBalance_FiscalPeriodFromDifferentTenant_ThrowsReportParameterValidationException`.
- **Balanced:** `TrialBalance_BalancedChart_IsBalancedTrue`.
- **Unbalanced (synthetic):** `TrialBalance_UnbalancedChart_IsBalancedFalse_EmitsWarning` (seed an orphan debit; assert warning text).
- **Zero-balance filtering:** `TrialBalance_IncludeZeroFalse_OmitsZeroBalanceAccounts`.
- **Zero-balance filtering:** `TrialBalance_IncludeZeroTrue_IncludesZeroBalanceAccounts`.
- **Tombstone filtering:** `TrialBalance_IncludeDeletedFalse_OmitsSoftDeletedAccounts`.
- **Tombstone filtering:** `TrialBalance_IncludeDeletedTrue_IncludesSoftDeletedAccounts`.
- **Provisional:** `TrialBalance_PeriodIsOpen_IsProvisionalTrue_WithWarning`.
- **Provisional:** `TrialBalance_PeriodIsSoftClosed_IsProvisionalTrue_WithWarning`.
- **Provisional:** `TrialBalance_PeriodIsLocked_IsProvisionalFalse_NoWarning`.
- **Provisional:** `TrialBalance_AsOfDateWithoutPeriod_IsProvisionalFalse` (explicit asOf = caller takes responsibility).
- **Parameter validation:** `TrialBalance_NeitherPeriodNorAsOfDate_ThrowsValidationException`.
- **Parameter validation:** `TrialBalance_BothPeriodAndAsOfDate_ThrowsValidationException`.
- **Ordering:** `TrialBalance_Rows_AreOrderedByCodeThenIdStable`.
- **Determinism (via shared harness):** `TrialBalanceDeterminismTests : ReportCartridgeDeterminismTests<...>`.

`tests/AddTrialBalanceCartridgeTests.cs`:

- `AddTrialBalanceCartridge_RegistersCartridgeWithRegistry` (assert `RegisteredKinds.Contains(ReportKind.TrialBalance)` after host startup).
- `AddTrialBalanceCartridge_RunnerResolvesCartridge` (smoke).

Total new tests this PR: ~24.

#### Verification

- `dotnet build` succeeds.
- All PR 1 + PR 2 tests pass.
- Spot-check: a 4-account chart (Cash Asset, AR Asset, Revenue Income, AP Liability) with one balanced JE produces a Trial Balance with 4 rows; `IsBalanced == true`; `TotalDebit == TotalCredit > 0`.

#### Do NOT in this PR

- Do NOT add per-account drill-down query. Cartridges return summary rows; drilldown is UI-driven via existing ledger read APIs.
- Do NOT add comparative-period column (prior-period side-by-side). Deferred.
- Do NOT touch the runner or registry — both ship complete in PR 1.

---

### PR 3 — AR Aging Summary cartridge

**Estimated effort:** ~1.5h
**Scope:** `ArAgingSummaryCartridge` + `ArAgingSummaryParameters` + `ArAgingSummaryResult` + per-property and per-customer rollups; delegates the heavy lifting to `IArAgingService` from `blocks-financial-ar`
**Commit subject:** `feat(blocks-reports): ArAgingSummary cartridge (per-customer + per-property rollups) per Stage 02 §4.14`
**Depends on:** PR 1 merged + `blocks-financial-ar` on main
**Branch:** `cob/blocks-reports-ar-aging-summary`

#### What AR Aging Summary is

`IArAgingService` from `blocks-financial-ar` already computes the per-invoice aging buckets (0 / 0–30 / 31–60 / 61–90 / 90+) per the AR hand-off PR 4. The cartridge layer adds **portfolio-level summary**:

1. A per-customer rollup row (`ArAgingSummaryRow`) showing the customer's total in each bucket — re-uses the service's output directly.
2. A per-property rollup row (one per property; one "Unassigned" row for invoices without `propertyId`) — re-grouped by re-querying `IArAgingService` with `groupBy: Property`.
3. A portfolio total row.
4. Worst-bucket-by-customer highlight (top 10 customers ranked by 90+ balance).

#### Parameters

```csharp
public sealed record ArAgingSummaryParameters
{
    public required ChartOfAccountsId ChartId { get; init; }
    public DateOnly? AsOfDate { get; init; }  // defaults to today (caller's clock) if null

    /// <summary>Optional filter: only include the named customers.</summary>
    public IReadOnlyList<PartyId>? CustomerIds { get; init; }

    /// <summary>Optional filter: only include the named properties.</summary>
    public IReadOnlyList<string>? PropertyIds { get; init; }

    /// <summary>How many top-90+-bucket customers to surface. Default 10. Capped at 100.</summary>
    public int TopDelinquentN { get; init; } = 10;
}
```

#### Result

```csharp
public sealed record ArAgingSummaryResult(
    ChartOfAccountsId ChartId,
    DateOnly AsOf,
    IReadOnlyList<ArAgingSummaryRow> ByCustomer,
    IReadOnlyList<ArAgingSummaryRow> ByProperty,
    ArAgingSummaryRow Totals,
    IReadOnlyList<TopDelinquentCustomer> TopDelinquent);

public sealed record ArAgingSummaryRow(
    string GroupKey,           // customer name or property id; "Unassigned" or "All"
    string GroupLabel,         // human-readable
    decimal Current,
    decimal Days0to30,
    decimal Days31to60,
    decimal Days61to90,
    decimal Days90Plus,
    decimal TotalOpen);

public sealed record TopDelinquentCustomer(
    PartyId CustomerId,
    string CustomerName,
    decimal Days90PlusBalance,
    decimal TotalOpenBalance);
```

#### Cartridge implementation sketch

```csharp
public sealed class ArAgingSummaryCartridge
    : IReportCartridge<ArAgingSummaryParameters, ArAgingSummaryResult>
{
    public ReportKind Kind => ReportKind.ArAgingSummary;

    private readonly IArAgingService _aging;
    private readonly IChartOfAccountsRepository _charts;
    private readonly IPartyReadModel _parties;

    public ArAgingSummaryCartridge(
        IArAgingService aging,
        IChartOfAccountsRepository charts,
        IPartyReadModel parties)
    {
        _aging = aging; _charts = charts; _parties = parties;
    }

    public async Task<ArAgingSummaryResult> ExecuteAsync(
        ReportExecutionContext context,
        ArAgingSummaryParameters parameters,
        CancellationToken ct = default)
    {
        // 1. Tenant-scope the chart
        var chart = await _charts.GetByIdAsync(parameters.ChartId, ct);
        if (chart is null || chart.TenantId != context.TenantId)
            throw new ReportParameterValidationException(
                $"ChartId {parameters.ChartId} not found in tenant {context.TenantId}.");

        if (parameters.TopDelinquentN < 0 || parameters.TopDelinquentN > 100)
            throw new ReportParameterValidationException("TopDelinquentN must be 0..100.");

        var asOf = parameters.AsOfDate ?? context.AsOfUtc.InUtc().Date.ToDateOnly();

        // 2. Pull both groupings
        var byCustomer = await _aging.ComputeAgingAsync(parameters.ChartId, asOf, AgingGroupBy.Customer, ct);
        var byProperty = await _aging.ComputeAgingAsync(parameters.ChartId, asOf, AgingGroupBy.Property, ct);

        // 3. Optional filtering (post-aggregation)
        var customerRows = ApplyCustomerFilter(byCustomer.Rows, parameters.CustomerIds);
        var propertyRows = ApplyPropertyFilter(byProperty.Rows, parameters.PropertyIds);

        // 4. Hydrate customer labels (party names)
        var customerSummary = await ToSummaryRowsAsync(customerRows, _parties, ct, isCustomer: true);
        var propertySummary = ToSummaryRows(propertyRows, isCustomer: false);

        // 5. Totals — re-compute from byCustomer rows for consistency
        var totals = SumToTotalsRow(customerRows);

        // 6. Top delinquents
        var topDelinquent = await BuildTopDelinquentAsync(
            customerRows, parameters.TopDelinquentN, _parties, ct);

        return new ArAgingSummaryResult(
            ChartId:        parameters.ChartId,
            AsOf:           asOf,
            ByCustomer:     customerSummary,
            ByProperty:     propertySummary,
            Totals:         totals,
            TopDelinquent:  topDelinquent);
    }
    // (helper methods omitted for brevity in this hand-off; canonical patterns)
}
```

#### Tests (PR 3)

`tests/ArAgingSummaryCartridgeTests.cs`:

- **Edge case — empty:** `ArAgingSummary_EmptyChart_ReturnsZeroRowsAndZeroTotals`.
- **Edge case — single record:** `ArAgingSummary_SingleInvoiceCurrent_AppearsInCurrentBucket`.
- **Edge case — single record:** `ArAgingSummary_SingleInvoice90PlusDays_AppearsInTopDelinquent`.
- **Customer rollup:** `ArAgingSummary_MultipleInvoicesSameCustomer_AggregatedInOneRow`.
- **Property rollup:** `ArAgingSummary_MultipleInvoicesSameProperty_AggregatedInOneRow`.
- **Property rollup:** `ArAgingSummary_InvoiceWithNullPropertyId_RolledIntoUnassigned`.
- **Customer-filter:** `ArAgingSummary_CustomerIdsFilter_OmitsOtherCustomers`.
- **Property-filter:** `ArAgingSummary_PropertyIdsFilter_OmitsOtherProperties`.
- **Tenant-isolation:** `ArAgingSummary_ChartFromDifferentTenant_ThrowsReportParameterValidationException`.
- **Tenant-isolation:** `ArAgingSummary_CustomerIdsFromDifferentTenant_AreSilentlyExcluded` (or throws — spec choice; pick exclude-silently with a warning emitted on the result).
- **Top-delinquent:** `ArAgingSummary_TopDelinquent_OrderedDescendingBy90Plus`.
- **Top-delinquent:** `ArAgingSummary_TopDelinquentN_RespectsCap`.
- **Top-delinquent:** `ArAgingSummary_TopDelinquentN_ZeroReturnsEmpty`.
- **Top-delinquent:** `ArAgingSummary_TopDelinquentN_OverCap_ThrowsValidationException` (>100).
- **Totals consistency:** `ArAgingSummary_Totals_EqualSumOfByCustomerRows`.
- **Totals consistency:** `ArAgingSummary_Totals_EqualSumOfByPropertyRows` (asserts the two rollups agree at the portfolio level).
- **Determinism (shared harness):** `ArAgingSummaryDeterminismTests : ReportCartridgeDeterminismTests<...>`.

`tests/AddArAgingSummaryCartridgeTests.cs`:

- `AddArAgingSummaryCartridge_RegistersCartridge`.

Total new tests this PR: ~18.

#### Verification

- `dotnet build` succeeds.
- All PR 1–3 tests pass.
- Spot-check: 3 customers × 3 invoices each at staggered due dates → 3 customer rows + 1 property row (assume single property) + portfolio totals match the sum.

#### Do NOT in this PR

- Do NOT re-implement the aging algorithm. The cartridge is a thin orchestrator over `IArAgingService`.
- Do NOT add per-invoice drill-down. UI calls `IInvoiceRepository.QueryOpenAsync(...)` directly when the user clicks a row.

---

### PR 4 — AP Aging Summary cartridge

**Estimated effort:** ~1.5h
**Scope:** `ApAgingSummaryCartridge` mirror of PR 3 for AP; per-vendor and per-property rollups; top-N most-overdue vendors
**Commit subject:** `feat(blocks-reports): ApAgingSummary cartridge (per-vendor + per-property rollups) per Stage 02 §4.14`
**Depends on:** PR 1 merged + **sibling `blocks-financial-ap` hand-off PR 4 merged** (this is the gate)
**Branch:** `cob/blocks-reports-ap-aging-summary`

This PR is structurally identical to PR 3 with `Bill`/`Vendor` substituted for `Invoice`/`Customer`. Key differences:

- Uses `IApAgingService` from `blocks-financial-ap` (which already excludes `Disputed` bills per the AP hand-off).
- Top-N row is "top delinquent vendors by 90+ balance" — which in AP context means "vendors we most owe and have most failed to pay" (operationally: vendors at greatest risk of cutoff / collections).
- `ApAgingSummaryParameters.VendorIds` replaces `CustomerIds`.
- Adds an emitted warning if any disputed bill exists for an included vendor: `"Vendor {name} has {N} bill(s) in Disputed status; values excluded from aging."` — uses `IBillRepository.QueryByStatusAsync(chart, [BillStatus.Disputed])` filtered to the result-vendor set.

#### Parameters / Result (delta from PR 3)

```csharp
public sealed record ApAgingSummaryParameters
{
    public required ChartOfAccountsId ChartId { get; init; }
    public DateOnly? AsOfDate { get; init; }
    public IReadOnlyList<PartyId>? VendorIds { get; init; }
    public IReadOnlyList<string>? PropertyIds { get; init; }
    public int TopDelinquentN { get; init; } = 10;
}

public sealed record ApAgingSummaryResult(
    ChartOfAccountsId ChartId,
    DateOnly AsOf,
    IReadOnlyList<ApAgingSummaryRow> ByVendor,
    IReadOnlyList<ApAgingSummaryRow> ByProperty,
    ApAgingSummaryRow Totals,
    IReadOnlyList<TopDelinquentVendor> TopDelinquent,
    IReadOnlyList<string> DisputedBillWarnings)  // visible warnings emitted into result
    : IReportProvisionalityCarrier
{
    public bool IsProvisional => false;  // AP aging is never provisional (no period gate)
    public IReadOnlyList<string> Warnings => DisputedBillWarnings;
}

public sealed record ApAgingSummaryRow(...);          // same shape as ArAgingSummaryRow
public sealed record TopDelinquentVendor(...);
```

#### Tests (PR 4)

Same test taxonomy as PR 3 (~18 tests), with the following AP-specific additions:

- **Disputed exclusion:** `ApAgingSummary_DisputedBillsExcludedFromAgingTotals`.
- **Disputed warning:** `ApAgingSummary_DisputedBillForIncludedVendor_EmitsWarning_WithCount`.
- **Disputed warning:** `ApAgingSummary_NoDisputedBills_EmitsNoWarning`.
- **Disputed warning:** `ApAgingSummary_DisputedBillForFilteredOutVendor_EmitsNoWarning`.

Total new tests this PR: ~22.

#### Do NOT in this PR

- Do NOT re-implement AP aging. The cartridge orchestrates `IApAgingService`.
- Do NOT add a paid-bill-by-vendor query — that is cash-out projection, separate cartridge.

---

### PR 5 — P&L by Property cartridge

**Estimated effort:** ~2.5h
**Scope:** `ProfitAndLossByPropertyCartridge` + period-locked + per-property dimensional aggregation; consumes ledger + period repository
**Commit subject:** `feat(blocks-reports): ProfitAndLossByProperty cartridge (period-locked; dimension on PropertyId) per Stage 02 §4.2`
**Depends on:** PR 1 merged + PR 2 merged (lifts Trial Balance period-binding pattern) + `blocks-financial-periods` on main
**Branch:** `cob/blocks-reports-pl-by-property`

#### What P&L by Property is

For a period `[startDate, endDate]` and a set of properties:

- For each property `p` (plus an "Unassigned" bucket for journal-lines without `propertyId`):
  - Sum of all `Income`-type account postings dated within the period, dimension-tagged to `p` → `IncomeByAccount[p][accountId]`.
  - Sum of all `Expense`-type account postings similarly → `ExpenseByAccount[p][accountId]`.
  - `NetIncome[p] = sum(IncomeByAccount[p].Values) - sum(ExpenseByAccount[p].Values)`.
- Portfolio totals = sum across properties.
- Comparative period column (optional via `ComparePriorPeriod`).

This cartridge depends on the ledger exposing a `GetJournalLinesByAccountTypeAndPeriodAsync(chartId, types, period, dimensionField, marker, ct)` query — which the ledger PR provides (per Stage 02 §6 + the ledger hand-off's `IGeneralLedgerReadModel`). If the method name differs, COB lifts the closest match and files a `cob-question-*` ONLY if no query supports the lookup.

#### Parameters

```csharp
public sealed record ProfitAndLossByPropertyParameters
{
    public required ChartOfAccountsId ChartId { get; init; }

    /// <summary>The period to report. MUST be set; explicit date ranges are deferred to a follow-on cartridge.</summary>
    public required FiscalPeriodId FiscalPeriodId { get; init; }

    /// <summary>Optional filter: only the named properties (omit = all properties touched in the period + Unassigned).</summary>
    public IReadOnlyList<string>? PropertyIds { get; init; }

    /// <summary>If true, attach a prior-period column. Default false.</summary>
    public bool ComparePriorPeriod { get; init; } = false;
}
```

#### Result

```csharp
public sealed record ProfitAndLossByPropertyResult(
    ChartOfAccountsId ChartId,
    FiscalPeriodId PeriodId,
    string PeriodLabel,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<PropertyPnLColumn> Properties,
    PropertyPnLColumn Totals,
    PropertyPnLColumn? PriorPeriodTotals,   // null unless ComparePriorPeriod true
    bool IsProvisional,
    IReadOnlyList<string> Warnings)
    : IReportProvisionalityCarrier;

public sealed record PropertyPnLColumn(
    string PropertyKey,      // PropertyId or "Unassigned" or "All"
    string PropertyLabel,
    IReadOnlyList<PnLAccountLine> Income,
    decimal IncomeTotal,
    IReadOnlyList<PnLAccountLine> Expenses,
    decimal ExpenseTotal,
    decimal NetIncome);

public sealed record PnLAccountLine(
    GLAccountId AccountId,
    string AccountCode,
    string AccountName,
    decimal Amount);
```

#### Provisionality

Same logic as Trial Balance: if `FiscalPeriod.Status != Locked` → `IsProvisional = true` + warning. If `ComparePriorPeriod` is true, also check the prior period — if either is not Locked, the result is provisional.

#### Tests (PR 5)

`tests/ProfitAndLossByPropertyCartridgeTests.cs`:

- **Edge case — empty:** `PnL_EmptyPeriod_AllPropertiesShowZero`.
- **Edge case — single record:** `PnL_OneIncomeJE_ShowsInIncomeAndNetIncome`.
- **Edge case — single record:** `PnL_OneExpenseJE_ShowsInExpenseAndReducesNetIncome`.
- **Edge case — period boundary:** `PnL_JEOnPeriodStartDate_Included`.
- **Edge case — period boundary:** `PnL_JEOnPeriodEndDate_Included`.
- **Edge case — period boundary:** `PnL_JEDayAfterPeriodEnd_Excluded`.
- **Dimension aggregation:** `PnL_TwoIncomeJEsSameProperty_AggregatedInOneColumn`.
- **Dimension aggregation:** `PnL_JEWithNullPropertyId_RolledIntoUnassignedColumn`.
- **Dimension aggregation:** `PnL_PropertyFilter_OmitsOtherProperties`.
- **Tenant-isolation:** `PnL_ChartFromDifferentTenant_ThrowsReportParameterValidationException`.
- **Tenant-isolation:** `PnL_FiscalPeriodFromDifferentTenant_ThrowsReportParameterValidationException`.
- **Tenant-isolation:** `PnL_PropertyIdsFromDifferentTenant_AreSilentlyExcluded_WithWarning`.
- **Totals:** `PnL_PortfolioTotals_EqualSumAcrossPropertyColumns`.
- **NetIncome math:** `PnL_NetIncome_EqualsIncomeMinusExpense_AtPropertyLevel`.
- **NetIncome math:** `PnL_NetIncome_EqualsIncomeMinusExpense_AtPortfolioLevel`.
- **Comparison:** `PnL_ComparePriorPeriodTrue_AttachesPriorPeriodTotals`.
- **Comparison:** `PnL_ComparePriorPeriodFalse_PriorPeriodTotalsIsNull`.
- **Comparison:** `PnL_ComparePriorPeriodTrueButNoPriorPeriod_PriorIsNull_WithWarning`.
- **Provisional:** `PnL_PeriodIsOpen_IsProvisionalTrue`.
- **Provisional:** `PnL_PeriodIsLocked_PriorIsOpen_AndComparing_IsProvisionalTrue`.
- **Provisional:** `PnL_PeriodIsLocked_NoComparison_IsProvisionalFalse`.
- **Account-ordering:** `PnL_IncomeAndExpenseLines_OrderedByCodeStable`.
- **Determinism (shared harness):** `ProfitAndLossByPropertyDeterminismTests : ReportCartridgeDeterminismTests<...>`.

`tests/AddProfitAndLossByPropertyCartridgeTests.cs`:

- `AddProfitAndLossByPropertyCartridge_RegistersCartridge`.

Total new tests this PR: ~25.

#### Verification

- `dotnet build` succeeds.
- All PR 1–5 tests pass.
- Spot-check: 2 properties × 1 month of synthetic postings (3 income lines + 4 expense lines per property; 1 expense with `null` PropertyId) → 3 columns (Prop A, Prop B, Unassigned) + Totals; `Totals.NetIncome == sum(prop.NetIncome for prop in [A, B, Unassigned])`.

#### Do NOT in this PR

- Do NOT add YTD aggregation across periods. P&L is single-period in this PR; YTD is a follow-on (P&L date-range cartridge).
- Do NOT add classification-level grouping (the inner-most categorization below account). Deferred.
- Do NOT compute closing-balance rolls. P&L is income-statement-only; balance sheet is its own cartridge (deferred).

---

### PR 6 — Rent Roll v2 cartridge

**Estimated effort:** ~2.5h
**Scope:** `RentRollCartridge` + per-property + per-portfolio rollups; as-of-date; current + projected; vacancy reason; aging delegation
**Commit subject:** `feat(blocks-reports): RentRoll v2 cartridge (canonical implementation; supersedes @sunfish/contracts v1) per Stage 02 §4.1`
**Depends on:** PR 1 merged + `blocks-leases` on main (already)
**Branch:** `cob/blocks-reports-rent-roll-v2`

#### What Rent Roll v2 is

Per Stage 02 §4.1, the rent roll is the **per-property snapshot of every unit + its lease + occupancy + current rent + prepaid balance + delinquency aging + tenant**. v2 adds (relative to v1 in `@sunfish/contracts`):

- Prepaid balance (rent paid in advance for future periods).
- Delinquency aging bucket (delegates to `IArAgingService` per-lease).
- Projected next-month rent (rolling forward today's rent for the lease assuming no escalator change).
- Lease-expiration-in-window flag (true if `LeaseEnd <= AsOfDate + 90 days`).
- Vacancy reason (for vacant units, the most recent reason field on the prior lease — turnover, end-of-term, eviction, never-leased).
- Per-portfolio totals (occupancy %, total monthly rent, total open balance).

#### Parameters

```csharp
public sealed record RentRollParameters
{
    public required ChartOfAccountsId ChartId { get; init; }   // for AR aging lookup
    public DateOnly? AsOfDate { get; init; }                   // defaults to today

    /// <summary>Optional filter: only the named properties.</summary>
    public IReadOnlyList<PropertyId>? PropertyIds { get; init; }

    /// <summary>Lookahead window for "expiring soon" flag (days). Default 90.</summary>
    public int ExpiringWindowDays { get; init; } = 90;

    /// <summary>If true, include unit rows for unleased / vacant units. Default true.</summary>
    public bool IncludeVacant { get; init; } = true;
}
```

#### Result

```csharp
public sealed record RentRollResult(
    DateOnly AsOf,
    IReadOnlyList<RentRollPropertyBlock> Properties,
    RentRollPortfolioSummary Portfolio);

public sealed record RentRollPropertyBlock(
    PropertyId PropertyId,
    string PropertyName,
    IReadOnlyList<RentRollUnitRow> Units,
    RentRollPropertySummary Summary);

public sealed record RentRollUnitRow(
    string UnitLabel,
    LeaseId? CurrentLeaseId,
    PartyId? TenantId,
    string? TenantName,
    DateOnly? LeaseStart,
    DateOnly? LeaseEnd,
    bool ExpiringSoon,
    decimal MonthlyRent,
    decimal ProjectedNextMonthRent,
    DateOnly? LastPaymentDate,
    decimal PrepaidBalance,
    decimal OpenBalance,
    ArAgingBucket DelinquencyBucket,
    OccupancyStatus Status,
    VacancyReason? VacancyReason);

public enum OccupancyStatus { Occupied, Vacant, NoticeGiven, OffMarket }
public enum ArAgingBucket { Current, Days0to30, Days31to60, Days61to90, Days90Plus, NoBalance }
public enum VacancyReason { Turnover, EndOfTerm, Eviction, NeverLeased, OffMarket }

public sealed record RentRollPropertySummary(
    int TotalUnits,
    int OccupiedUnits,
    decimal OccupancyRate,    // OccupiedUnits / TotalUnits  (0..1)
    decimal MonthlyRentTotal,
    decimal MonthlyRentTotalIfFullyLeased,
    decimal OpenBalanceTotal);

public sealed record RentRollPortfolioSummary(
    int PropertiesCovered,
    int TotalUnits,
    int OccupiedUnits,
    decimal OccupancyRate,
    decimal MonthlyRentTotal,
    decimal OpenBalanceTotal);
```

#### Cartridge implementation sketch

```csharp
public sealed class RentRollCartridge
    : IReportCartridge<RentRollParameters, RentRollResult>
{
    public ReportKind Kind => ReportKind.RentRoll;

    private readonly ILeaseService _leases;
    private readonly IPropertyReadModel _properties;    // if available; else use ILeaseService.GetPropertiesAsync(...)
    private readonly IArAgingService _aging;
    private readonly IPartyReadModel _parties;          // optional; null → use the Party shim from blocks-leases
    private readonly IClock _clock;

    public async Task<RentRollResult> ExecuteAsync(
        ReportExecutionContext context,
        RentRollParameters parameters,
        CancellationToken ct = default)
    {
        var asOf = parameters.AsOfDate ?? _clock.GetCurrentInstant().InUtc().Date.ToDateOnly();
        if (parameters.ExpiringWindowDays < 0)
            throw new ReportParameterValidationException("ExpiringWindowDays must be ≥ 0.");

        // 1. Resolve properties in tenant
        var properties = await ResolvePropertiesAsync(context.TenantId, parameters.PropertyIds, ct);

        // 2. For each property: enumerate units, find current lease (if any), build row
        var blocks = new List<RentRollPropertyBlock>();
        var portfolioMonthlyRent = 0m;
        var portfolioOpenBalance = 0m;
        var portfolioUnits = 0;
        var portfolioOccupied = 0;

        // Pre-fetch AR aging bucketed by lease (one call per property)
        // OR a single chart-wide call grouped by lease — implementation choice
        var leaseAging = await BuildLeaseAgingMapAsync(parameters.ChartId, asOf, context, ct);

        foreach (var property in properties.OrderBy(p => p.Name, StringComparer.Ordinal)
                                            .ThenBy(p => p.Id.ToString(), StringComparer.Ordinal))
        {
            var units = await _properties.GetUnitsAsync(property.Id, ct);  // or via ILeaseService
            var rows = new List<RentRollUnitRow>();
            foreach (var unit in units.OrderBy(u => u.Label, StringComparer.Ordinal))
            {
                var currentLease = await _leases.GetCurrentLeaseForUnitAsync(unit.Id, asOf, ct);
                if (currentLease is null && !parameters.IncludeVacant) continue;
                rows.Add(BuildRow(unit, currentLease, asOf, parameters.ExpiringWindowDays, leaseAging));
            }
            var summary = SummarizeProperty(rows);
            blocks.Add(new RentRollPropertyBlock(property.Id, property.Name, rows, summary));

            portfolioMonthlyRent  += summary.MonthlyRentTotal;
            portfolioOpenBalance  += summary.OpenBalanceTotal;
            portfolioUnits        += summary.TotalUnits;
            portfolioOccupied     += summary.OccupiedUnits;
        }

        var portfolio = new RentRollPortfolioSummary(
            PropertiesCovered:  blocks.Count,
            TotalUnits:         portfolioUnits,
            OccupiedUnits:      portfolioOccupied,
            OccupancyRate:      portfolioUnits == 0 ? 0m : (decimal)portfolioOccupied / portfolioUnits,
            MonthlyRentTotal:   portfolioMonthlyRent,
            OpenBalanceTotal:   portfolioOpenBalance);

        return new RentRollResult(asOf, blocks, portfolio);
    }
}
```

#### Tests (PR 6)

`tests/RentRollCartridgeTests.cs`:

- **Edge case — empty:** `RentRoll_NoProperties_ReturnsEmptyBlocksAndZeroPortfolio`.
- **Edge case — empty:** `RentRoll_OnePropertyNoUnits_ReturnsOneBlockWithZeroSummary`.
- **Edge case — single record:** `RentRoll_OneOccupiedUnit_ShowsTenantAndRent`.
- **Edge case — single record:** `RentRoll_OneVacantUnit_IncludeVacantTrue_ShownAsVacant`.
- **Edge case — single record:** `RentRoll_OneVacantUnit_IncludeVacantFalse_Omitted`.
- **Property filter:** `RentRoll_PropertyIdsFilter_OmitsOtherProperties`.
- **Tenant-isolation:** `RentRoll_PropertyIdFromDifferentTenant_ThrowsReportParameterValidationException`.
- **Tenant-isolation:** `RentRoll_ChartFromDifferentTenant_ThrowsReportParameterValidationException`.
- **As-of date:** `RentRoll_AsOfDateInPast_ShowsLeasesActiveOnThatDate`.
- **As-of date:** `RentRoll_AsOfDateBeforeAnyLease_ShowsAllVacant`.
- **Expiring window:** `RentRoll_LeaseEndingWithinWindow_ExpiringSoonTrue`.
- **Expiring window:** `RentRoll_LeaseEndingOutsideWindow_ExpiringSoonFalse`.
- **Expiring window:** `RentRoll_ExpiringWindowZero_OnlyLeasesEndingTodayAreExpiringSoon`.
- **Expiring window:** `RentRoll_ExpiringWindowNegative_ThrowsValidationException`.
- **Aging delegation:** `RentRoll_TenantWithOpenInvoice_DelinquencyBucketReflectsAging`.
- **Aging delegation:** `RentRoll_TenantWithNoOpenInvoice_DelinquencyBucketIsNoBalance`.
- **Vacancy reason:** `RentRoll_UnitNeverLeased_VacancyReasonIsNeverLeased`.
- **Vacancy reason:** `RentRoll_UnitWithEndedLease_VacancyReasonIsEndOfTerm`.
- **Vacancy reason:** `RentRoll_UnitWithEvictionTerminatedLease_VacancyReasonIsEviction`.
- **Property summary:** `RentRoll_PropertySummary_OccupancyRateIsOccupiedOverTotal`.
- **Property summary:** `RentRoll_PropertySummary_OccupancyRateAllVacant_IsZero`.
- **Property summary:** `RentRoll_PropertySummary_NoUnits_OccupancyRateIsZero` (no divide-by-zero).
- **Portfolio summary:** `RentRoll_PortfolioTotals_EqualSumAcrossProperties`.
- **Ordering:** `RentRoll_Properties_OrderedByNameThenId`.
- **Ordering:** `RentRoll_UnitsWithinProperty_OrderedByLabel`.
- **Determinism (shared harness):** `RentRollDeterminismTests : ReportCartridgeDeterminismTests<...>`.

`tests/AddRentRollCartridgeTests.cs`:

- `AddRentRollCartridge_RegistersCartridge`.

Total new tests this PR: ~28.

#### Verification

- `dotnet build` succeeds.
- All PR 1–6 tests pass.
- Spot-check: seed 2 properties × 3 units each (5 occupied + 1 vacant) → 2 blocks, 6 unit rows, portfolio occupancy rate `5/6 ≈ 0.833`.

#### Do NOT in this PR

- Do NOT touch `@sunfish/contracts` or the Bridge `GET /api/v1/reports/rent-roll` endpoint. The v1 stays on main; the v1 → v2 rewire is a separate follow-on hand-off authored after PR 7 of THIS hand-off lands.
- Do NOT add escalator / step-up rent projection. Phase 1 v2 projects current rent unchanged; future v3 adds escalators.
- Do NOT add lease-renewal status. That is a separate Lease Expiration cartridge.

---

### PR 7 — `AddBlocksReports()` DI extension + docs page + cluster acceptance

**Estimated effort:** ~1.5h
**Scope:** umbrella DI registration; `apps/docs/blocks/reports/README.md`; Rent Roll v1 → v2 migration note; cluster acceptance harness; package README
**Commit subject:** `feat(blocks-reports): umbrella AddBlocksReports() + docs + cluster acceptance per Stage 06 PASS gate`
**Depends on:** PRs 1–6 merged
**Branch:** `cob/blocks-reports-di-and-docs`

#### `DependencyInjection/ServiceCollectionExtensions.cs` extension

```csharp
namespace Sunfish.Blocks.Reports.DependencyInjection;

public static class ReportsCartridgeServiceCollectionExtensions
{
    /// <summary>
    /// Umbrella registration: substrate + all built-in Phase 1 MVP cartridges.
    /// Equivalent to: AddBlocksReportsSubstrate() + AddTrialBalanceCartridge()
    ///   + AddArAgingSummaryCartridge() + AddApAgingSummaryCartridge()
    ///   + AddProfitAndLossByPropertyCartridge() + AddRentRollCartridge().
    /// </summary>
    public static IServiceCollection AddBlocksReports(
        this IServiceCollection services,
        Action<ReportRunnerOptions>? configureRunner = null)
    {
        services.AddBlocksReportsSubstrate(configureRunner);
        services.AddTrialBalanceCartridge();
        services.AddArAgingSummaryCartridge();
        services.AddApAgingSummaryCartridge();
        services.AddProfitAndLossByPropertyCartridge();
        services.AddRentRollCartridge();
        return services;
    }
}
```

If a per-PR gate is unmet (e.g., AP cluster not yet built when this hand-off finalizes), the corresponding `Add*Cartridge()` call is wrapped in a feature-gate or simply not registered; document the gap in the cluster README.

#### `apps/docs/blocks/reports/README.md`

```markdown
# blocks-reports

Read-side reporting cartridge cluster for the Sunfish Anchor native domain
(ADR 0088 §1; Stage 02 design `icm/02_architecture/blocks-reports-schema-design.md`).

## Overview

`blocks-reports` is a pure read-side cluster. Cartridges are stateless functions
of (parameters, tenant scope, snapshot marker) → structured result DTOs. The
cluster owns no entities, emits no domain events, and performs no mutations.

## Phase 1 MVP cartridges shipped

| Kind | Cartridge | Purpose |
|---|---|---|
| `TrialBalance` | `TrialBalanceCartridge` | Per-period debit/credit balance per account |
| `ArAgingSummary` | `ArAgingSummaryCartridge` | Per-customer + per-property AR aging rollups |
| `ApAgingSummary` | `ApAgingSummaryCartridge` | Per-vendor + per-property AP aging rollups |
| `ProfitAndLossByProperty` | `ProfitAndLossByPropertyCartridge` | Per-period income minus expense, per-property dimensional aggregation |
| `RentRoll` | `RentRollCartridge` | Per-property snapshot of units, leases, rent collection, vacancy (canonical implementation; supersedes the `@sunfish/contracts` v1 thin slice from W#60 Phase 5 PR #847) |

## Future cartridges (follow-on hand-offs)

Balance Sheet · Cash Flow · Statement (customer/tenant) · Schedule E ·
1099-NEC · 1099-MISC · Work Order Summary · Maintenance Backlog ·
Lease Expiration · Vacancy · InvoicePDF · ReceiptPDF · QuotePDF · BillPDF
· Executive Dashboards (the `Dashboard`/`KPI` entities from Stage 02 §3) ·
Schedule + Subscription + Run-history persistence.

Each follow-on is a single-cartridge PR that adds an enum member, a cartridge
class, and a registration extension. No substrate changes.

## Architecture

### `IReportCartridge<TParams, TResult>`

The single cartridge contract. Cartridges are stateless functions; they MUST NOT
inject any write-surface or event publisher. This is the read-side discipline at
compile time.

### `IReportRunner`

The single dispatch entry point. Runner is responsible for:
- Resolving `ReportKind` → cartridge via the registry.
- Binding the caller's `TenantId` and `PrincipalId` into the execution context.
- Capturing a snapshot marker (for as-of-snapshot semantics — upstream cluster honor is follow-on work).
- Wrapping execution timing + warning/provisionality propagation.

### Determinism

Every cartridge guarantees: same `(params, snapshotMarker, tenantId)` → same `TResult`.
Enforced via `ReportCartridgeDeterminismTests<>` per-cartridge.

### Tenant isolation

Every cartridge:
1. Treats `context.TenantId` as the sole tenant scope.
2. Rejects entity IDs in parameters that belong to a different tenant (throws `ReportParameterValidationException`).
3. Filters or excludes silently with a warning for collection parameters (e.g., `CustomerIds`) — choice is per cartridge, documented in code.

## Registration

```csharp
services.AddBlocksReports();
// OR substrate + selected cartridges:
services
    .AddBlocksReportsSubstrate()
    .AddTrialBalanceCartridge()
    .AddRentRollCartridge();
```

## Rent Roll v1 → v2 migration

The `@sunfish/contracts` v1 Rent Roll DTO (W#60 Phase 5 PR #847) and the Bridge
`GET /api/v1/reports/rent-roll` endpoint (PR #848) **coexist with v2 for one
minor version**.

| Version | v1 location | v2 location | Status |
|---|---|---|---|
| Current | `@sunfish/contracts` + Bridge endpoint | `blocks-reports.RentRollCartridge` | Both live |
| +1 minor | v1 endpoint re-implemented as adapter to v2 | canonical | v1 marked `[Obsolete]` |
| +2 minors | v1 deleted (`410 Gone`) | canonical | Single source of truth |

The rewire (v1 endpoint adapter to v2) is a separate sunfish-PM hand-off
authored after this cluster's PR 7 merges.

## Stage 02 source

See `icm/02_architecture/blocks-reports-schema-design.md`.
```

#### Cluster acceptance harness

`tests/ClusterAcceptanceTests.cs` (in PR 7):

- `AddBlocksReports_RegistersAllSixMvpCartridges` (asserts `RegisteredKinds` contains all 5 cartridge kinds — note: 5 cartridges, despite "six" naming in early drafts; if a 6th cartridge is added before PR 7 merges, update this).
- `AddBlocksReports_IReportRunnerResolves` (smoke).
- `AddBlocksReports_TrialBalanceRunsEndToEnd` (light integration: synthetic ledger seeded; runner returns balanced result).
- `AddBlocksReports_RentRollRunsEndToEnd` (light integration: synthetic property + lease; runner returns non-empty block).
- `Cartridges_AllRegisteredKinds_HaveDeterminismTestCoverage` (reflection-based: asserts every `ReportKind` enum value EXCEPT the reserved-for-follow-on values has a corresponding `*DeterminismTests` test class loaded into the test assembly — a tripwire for future PRs that forget the determinism harness).

Total new tests this PR: ~5 (light; most coverage is per-PR).

#### Verification

- `dotnet build` succeeds across the entire solution.
- All PR 1–7 tests pass (~120+ total).
- `apps/docs/blocks/reports/README.md` rendered without broken links.
- Integration smoke per cluster acceptance §below.

#### Do NOT in this PR

- Do NOT update `@sunfish/contracts`. The v1 → v2 rewire is its own follow-on.
- Do NOT touch the Bridge endpoint. Same.
- Do NOT add Schedule E or PDF cartridges. Same.

---

## Cross-cluster integration table

Reports cluster reads FROM the following clusters (writes to NONE). Per Stage 02 §9.

| Source cluster | Interface(s) consumed | Fields/methods used | Cartridge(s) |
|---|---|---|---|
| `blocks-financial-ledger` | `IChartOfAccountsRepository`, `IGeneralLedgerReadModel` | `GetByIdAsync(chartId)`, `GetAccountsAsync(chartId, includeDeleted)`, `GetAccountBalancesAsOfAsync(chartId, asOf, marker)`, `GetJournalLinesByAccountTypeAndPeriodAsync(chartId, types, period, dimensionField, marker)` | PR 2 (Trial Balance), PR 5 (P&L) |
| `blocks-financial-periods` | `IFiscalPeriodRepository` | `GetByIdAsync(periodId)` → `FiscalPeriod { Id, ChartId, Label, StartDate, EndDate, Status, TenantId }` | PR 2 (Trial Balance period bind), PR 5 (P&L period bind + prior-period lookup) |
| `blocks-financial-ar` | `IArAgingService`, `IInvoiceRepository` (optional, for drill-down) | `ComputeAgingAsync(chartId, asOf, groupBy)` → `ArAgingReport` | PR 3 (AR Aging Summary), PR 6 (Rent Roll delinquency bucket) |
| `blocks-financial-ap` | `IApAgingService`, `IBillRepository` | `ComputeAgingAsync(chartId, asOf, groupBy)` → `ApAgingReport`; `QueryByStatusAsync(chart, [Disputed])` for warning emission | PR 4 (AP Aging Summary) |
| `blocks-leases` | `ILeaseService` (or `ILeaseRepository`), `IPropertyReadModel` (if available; else lease-side property surface) | `GetCurrentLeaseForUnitAsync(unitId, asOf)`, `ListLeasesAsync(propertyId, asOf)`, per-unit enumeration | PR 6 (Rent Roll) |
| `blocks-people-foundation` (optional) | `IPartyReadModel` | `GetPartyAsync(partyId)` → tenant/customer/vendor name resolution | PRs 3, 4, 6 (label hydration) |

**Filters:** every cluster call passes the snapshot marker through `context.SnapshotMarker` (upstream honor is follow-on work; cartridges already pass the value).

**Tenant scope:** every cluster call is implicitly scoped by either (a) the chart id (which carries `TenantId`) or (b) the entity ID being looked up (which carries `TenantId`). The cartridge validates the `TenantId` of every returned entity matches `context.TenantId` and treats mismatches as a security boundary violation (throws or excludes with warning per cartridge).

---

## Pre-merge council requirements (consolidated)

| PR | security-engineering | .NET architect | Why |
|---|---|---|---|
| **PR 1 (substrate)** | **REQUIRED — 30-min spot-check** | **REQUIRED — 30-min spot-check** | Tenant-id binding convention + cartridge contract surface are the two highest-leverage decisions in the cluster; get both right once |
| PR 2 (Trial Balance) | optional | optional | Pattern-lift from PR 1; tenant-scoping pattern established |
| PR 3 (AR Aging) | optional | optional | Same |
| PR 4 (AP Aging) | optional | optional | Same |
| PR 5 (P&L) | optional | optional | Same |
| PR 6 (Rent Roll) | optional | optional | Same |
| PR 7 (DI + docs) | optional | optional | Mechanical |

If security-engineering or architect spot-check on PR 1 requests substantive shape changes, **halt and file `cob-question-*`** before pushing the revisions — the substrate shape change might cascade into PRs 2–6 (which are otherwise authored in parallel after PR 1).

---

## Idempotency-key catalog

**N/A.** Reports cluster is read-side: no events emitted, no mutations, no idempotency keys.

Each upstream cluster owns its own event catalog (per `path-ii-cross-cluster-event-bus.md`). Reports cluster does NOT subscribe to upstream events either — cartridges read state on-demand at execution time. Cache invalidation / live dashboards (Stage 02 §7) are a deferred follow-on.

If a future cartridge needs to persist a `ReportRun` audit record OR emit a `Reports.ReportRan` event, that addition is gated on a SEPARATE hand-off (likely the run-persistence + scheduling hand-off) and would include event catalog entries with the standard `idempotencyKey` field. **It is not in scope here.**

---

## Dependencies + sequence

### Upstream (must be on main)

| Cluster | Hand-off | Status at this hand-off's authoring (2026-05-17) |
|---|---|---|
| `foundation-events` | `foundation-events-stage06-handoff.md` | built ✓ |
| `blocks-financial-ledger` | `blocks-financial-ledger-chart-and-journal-stage06-handoff.md` (sibling — referenced but not on disk in this branch) | shipped per memory; PR 2/PR 5 gate |
| `blocks-financial-periods` | `blocks-financial-periods-stage06-handoff.md` | shipped per memory; PR 2/PR 5 gate |
| `blocks-financial-ar` | `blocks-financial-ar-stage06-handoff.md` | shipped per memory; PR 3 gate |
| `blocks-financial-ap` | `blocks-financial-ap-stage06-handoff.md` | sibling, ready-to-build; PR 4 gate (defer PR 4 if AP not on main when other cartridges land) |
| `blocks-leases` | (Path I; existing) | on main ✓; PR 6 gate satisfied |
| `blocks-people-foundation` | `blocks-people-foundation-stage06-handoff.md` | sibling, ready-to-build; OPTIONAL for PRs 3, 4, 6 (label hydration) — if not on main, use cluster-local Party shim per AR/AP pattern |

### Downstream (consumers / follow-ons)

| Consumer / follow-on | When it lands | What it does with this cluster |
|---|---|---|
| `apps/anchor-react` Rent Roll page rewire | Follow-on hand-off after PR 7 merges | Replaces v1 `@sunfish/contracts` Rent Roll fetch with a Bridge endpoint that delegates to `IReportRunner.RunAsync<RentRollParameters, RentRollResult>` |
| `blocks-reports-pdf` (Schedule E, invoice, receipt, quote, bill PDFs) | Phase 3 follow-on | Adds `@react-pdf/renderer` integration + template files; cartridges from this hand-off remain as the data-fetch layer; new cartridges register via the same substrate |
| `blocks-reports-dashboards` (Dashboard + Widget + KPI entities) | Phase 1.5 follow-on | Adds the `Dashboard` / `KPI` entities + widget-data sources; consumes existing cartridge results as widget data |
| `blocks-reports-scheduling` (ReportSchedule + ReportRun + ReportArtifact) | Phase 1.5 follow-on | Adds run-persistence + RRULE-based scheduling; emits the first `Reports.*` events (with idempotency keys) |
| `blocks-reports-tax` (TaxFormLineMap + Schedule E + 1099) | Phase 1.5 follow-on | Adds the tax-form mapping table + Schedule E generator; retires `blocks-tax-reporting/` per Stage 02 §11 Q8 |
| `tooling-anchor-import` ERPNext loader | Existing (consumes cluster after this lands) | After import, runs Trial Balance + Rent Roll cartridges to validate import correctness |

### Parallelization

- This hand-off can run **in parallel** with `blocks-docs` (no shared surface).
- PRs 2, 3, 5, 6 can be authored **in parallel** after PR 1 merges (PR 4 gates on AP).
- The Rent Roll v1 → v2 rewire follow-on does NOT block this hand-off's PASS gate.

---

## License posture

Clean-room MIT per ADR 0088 §3. Every formula and entity shape in this cluster derives from:

1. **Standard accounting / property-management practice** (uncopyrightable facts and processes per *Baker v. Selden*) — Trial Balance debit-vs-credit projection by NormalSide; aging-bucket boundaries (0/30/60/90+); P&L = Income − Expense; rent-roll column conventions.
2. **Pre-shipped Sunfish surfaces** — Rent Roll v1 from `@sunfish/contracts` PR #847 (already MIT).
3. **No copyleft code** enters this cluster. The cartridge contract, registry, runner, and cartridge implementations are original code authored against the Stage 02 design.

### Inspiration sources (study-only; no code transferred)

Per Stage 02 §10. The following were studied for **column-list / algorithmic-shape / taxonomy** purposes only; no code was transferred:

| Source | License | What we drew from it (uncopyrightable) |
|---|---|---|
| Wave Accounting | Proprietary | Structural inspiration for AR aging summary + per-property P&L (no code access — observed UI behavior only) |
| QuickBooks | Proprietary | Trial Balance + AR aging conventional column layout (idem) |
| GnuCash | GPLv2 | Aging-bucket boundaries (0/30/60/90+ days) — uncopyrightable convention; cited in NOTICE.md as "inspiration only — clean-room study in isolated worktree per ADR 0088 §3.2; no code transferred" |
| ERPNext | GPLv3 | Rent Roll column set + per-property cost-center grouping pattern — uncopyrightable taxonomy; same NOTICE treatment |
| Beancount + ledger-cli | GPLv2 | "Trial Balance is a sum-by-account over a period" — uncopyrightable algorithm-shape; idem |

### Required attribution

**`packages/blocks-reports/NOTICE.md`** (created in PR 1):

```markdown
# NOTICE — Sunfish.Blocks.Reports

This package implements read-side reporting cartridges for the Sunfish
Anchor native domain (ADR 0088).

All code in this package is original work distributed under the MIT License.

## Clean-room inspirations

The following projects were studied for **uncopyrightable factual content
only** (column lists, bucket boundaries, algorithmic shapes, taxonomy)
per ADR 0088 §3 clean-room discipline. No code was transferred from any of
these projects; all sources were read in an isolated git worktree at
`/tmp/sunfish-cleanroom-reads/` per ADR 0088 §3.2.

- **GnuCash** (GPLv2): aging bucket boundaries (0/30/60/90+ days) and AR/AP
  aging report structure.
- **ERPNext / Frappe** (GPLv3): rent roll column set; per-property cost-center
  P&L grouping convention.
- **Beancount + ledger-cli** (GPLv2): "Trial Balance is the sum of postings
  per account, with side-projection by normal balance" — standard double-entry
  algorithm.
- **Wave Accounting** (proprietary; no source access): structural inspiration
  for AR aging summary + per-property P&L from publicly-observed UI.
- **QuickBooks** (proprietary; no source access): Trial Balance + AR aging
  conventional column layout from publicly-observed UI.

## Direct dependencies

None at present. Future cartridges (e.g., the PDF cartridges in
`blocks-reports-pdf`) will introduce dependencies on `@react-pdf/renderer`
(MIT) and WeasyPrint (BSD-3); attribution will be added at that time.
```

PRs 2–6 do NOT need to add additional NOTICE entries — the single cluster-level NOTICE covers all clean-room inspirations.

### Discipline check before merging any PR

1. No copyleft code was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from GPL/AGPL sources appear in the new code.
3. The clean-room schema in `blocks-reports-schema-design.md` §4 + §6 + §8 is the source of truth.

---

## Test plan

### Per-PR minima (summary)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (substrate) | ~25 | Registry; runner; context; provisionality propagation; determinism harness |
| PR 2 (Trial Balance) | ~24 | Edge cases; balanced/unbalanced; zero-balance + tombstone filters; provisional period; ordering; determinism |
| PR 3 (AR Aging Summary) | ~18 | Aggregation; filtering; top-delinquent; tenant-isolation; totals consistency; determinism |
| PR 4 (AP Aging Summary) | ~22 | Same + Disputed exclusion + warning emission |
| PR 5 (P&L by Property) | ~25 | Edge cases; period boundary; dimension aggregation; comparative period; NetIncome math; provisional; determinism |
| PR 6 (Rent Roll v2) | ~28 | Edge cases; tenant-isolation; expiring window; aging delegation; vacancy reason; occupancy math; ordering; determinism |
| PR 7 (DI + docs + cluster acceptance) | ~5 | DI smoke; reflection-based determinism-coverage tripwire |
| **Total** | **~145 new** | |

### Cluster-level acceptance (PASS gate at end of PR 7)

**A1.** `dotnet build` succeeds across `Sunfish.Blocks.Reports` + every downstream consumer (`packages/blocks-financial-ledger`, `packages/blocks-financial-periods`, `packages/blocks-financial-ar`, `packages/blocks-financial-ap`, `packages/blocks-leases`).

**A2.** `dotnet test packages/blocks-reports/tests/` passes all ~145 new tests.

**A3.** Trial Balance round-trip:
- Seed a chart via `IChartSeedingService.SeedChartAsync(...)` with 5 accounts (Cash Asset, AR Asset, Revenue Income, AP Liability, Expense — each with NormalSide).
- Post 3 balanced JEs across 3 days within the same fiscal period.
- Call `IReportRunner.RunAsync<TrialBalanceParameters, TrialBalanceResult>(...)` with `FiscalPeriodId` set.
- Assert: `Result.IsBalanced == true`; `Rows.Count == 5`; `TotalDebit == TotalCredit`; if the period is Open → `IsProvisional == true` + non-empty warnings; if Locked → `IsProvisional == false`.

**A4.** AR Aging Summary round-trip:
- Seed 3 customers × 3 invoices each at staggered due dates; 1 customer 90+ days overdue.
- Call the cartridge.
- Assert: `ByCustomer.Count == 3`; `Totals.TotalOpen` equals sum of customer rows; `TopDelinquent[0]` is the 90+-days customer with non-zero `Days90PlusBalance`.

**A5.** AP Aging Summary round-trip:
- Seed 3 vendors × 3 bills each; mark 1 vendor's bills as `Disputed`.
- Call the cartridge.
- Assert: `ByVendor.Count == 3`; Disputed-vendor's bills excluded from buckets (open buckets are 0); `DisputedBillWarnings` contains the vendor's name + the bill count.

**A6.** P&L by Property round-trip:
- Seed 2 properties × 1 month of postings (3 income + 4 expense lines per property; 1 expense with `null` PropertyId).
- Call the cartridge with `FiscalPeriodId` of a Locked period.
- Assert: `Properties.Count == 3` (2 properties + Unassigned); `Totals.NetIncome == sum(p.NetIncome for p in Properties)`; `IsProvisional == false`.

**A7.** Rent Roll v2 round-trip:
- Seed 2 properties × 3 units each (5 occupied, 1 vacant).
- Call the cartridge.
- Assert: `Properties.Count == 2`; `Portfolio.TotalUnits == 6`; `Portfolio.OccupiedUnits == 5`; `Portfolio.OccupancyRate ≈ 0.833`; the vacant unit's `Status == Vacant` with a non-null `VacancyReason`.

**A8.** DI registration smoke:
- Build a minimal `IServiceCollection` → `IServiceProvider` using `AddBlocksReports()` + the prerequisite cluster registrations.
- Assert: `IReportRunner` resolves; `ReportCartridgeRegistry.RegisteredKinds` contains all 5 cartridge kinds shipped in this hand-off.

**A9.** Determinism cluster invariant:
- Reflection scan asserts that every `ReportKind` member NOT in the reserved-for-follow-on list has at least one `*DeterminismTests` test class registered in the test assembly. Tripwire for future PRs that add cartridges without the harness.

**A10.** Tenant-isolation cluster invariant:
- For each cartridge, attempt to run with an entity ID from a different tenant; assert `ReportParameterValidationException` is thrown (or a warning is emitted, per cartridge spec — both behaviors are documented).

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `coordination/inbox/`:

### H1. Pre-build gate unmet (cluster-wide)

If `blocks-financial-ledger` or `foundation-events` is not on main when COB starts, **STOP** and file `cob-question-2026-05-XXTHH-MMZ-w60-p4-reports-cluster-gate-unmet.md` naming which predecessor is missing.

### H2. Per-PR gate unmet (per cartridge)

If a per-PR predecessor (e.g., `blocks-financial-ap` for PR 4) is not on main, **skip that PR** — author PRs 1, 2, 3, 5, 6, 7 in any order, and append PR 4 as a follow-up commit when AP lands. **Do NOT** file `cob-question-*` for skipped per-PR gates; instead, document the deferred PR in PR 7's docs and ledger note.

### H3. `IGeneralLedgerReadModel.GetJournalLinesByAccountTypeAndPeriodAsync` does not exist

If the ledger does not expose a method that satisfies P&L by Property's "sum journal lines by account type + period + dimension" need, **halt and file `cob-question-*`** naming the missing query. P&L is structurally impossible without it.

**Mitigation (no halt):** if a close-match method exists (e.g., `GetJournalLinesByPeriodAsync(chartId, period)`), use it and do the in-memory grouping inside the cartridge — slower for large periods but functionally correct. Document the slow path in code with a TODO.

### H4. `ICartridgeRegistrar` startup invocation pattern conflicts with existing host wiring

PR 1 introduces an `ICartridgeRegistrar` indirection (DI-registered fan-out into the cartridge registry). If the host's startup pipeline doesn't support invoking startup tasks (e.g., older `Microsoft.Extensions.Hosting` integration), file `cob-question-*` and fall back to eager registration: cartridges register themselves in their `Add*Cartridge()` extension method, not via a separate registrar phase.

### H5. `ILeaseService.GetCurrentLeaseForUnitAsync` shape mismatch (PR 6)

If the existing `ILeaseService` on `blocks-leases` does not expose a per-unit current-lease lookup, identify the closest equivalent (likely `ListLeasesAsync(propertyId, asOf, …)` filtered to the unit) and use it. File `cob-question-*` ONLY if no lease lookup works.

### H6. `IPartyReadModel` not on main — label hydration fallback

For PRs 3, 4, 6 — if `blocks-people-foundation` has not landed, use the cluster-local Party shim from `blocks-financial-ar` / `blocks-leases` (whichever defines `PartyId`). Add a TODO comment for the cross-cluster relocation. **Do NOT halt** — the cartridge ships with name-as-ID display (label = id.ToString()); the UI hydrates labels via a separate path until the people foundation lands.

### H7. Bridge / `@sunfish/contracts` rewire surfacing before PR 7

If anyone (including the COB session itself) starts a v1 Rent Roll endpoint rewire **before** PR 7 of this hand-off merges, **halt the rewire** and file `cob-question-*`. The rewire is a separately-authored follow-on; cross-streaming it into this hand-off risks merge-order chaos. The v1 endpoint must stay live and untouched through this hand-off's PASS gate.

### H8. PR 1 council-review pushback (substrate shape change)

If security-engineering or architect spot-check on PR 1 requests a substantive shape change to `IReportCartridge<,>` or `ReportExecutionContext`, **halt** before pushing revisions and file `cob-question-*` describing the requested change. The substrate shape ripples into PRs 2–6, all of which are authored against the shape established in PR 1. A change here may require holding all subsequent PR branches until the substrate stabilizes.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–7 merged to main** (PR 1 strictly first; PRs 2, 3, 5, 6 can land in any order after; PR 4 gated on `blocks-financial-ap` shipping; PR 7 requires all of PRs 1–6 — or PRs 1, 2, 3, 5, 6 if PR 4 is intentionally deferred). If PR 4 is deferred, the ledger flip is `built-partial` with a note naming the deferred cartridge.

2. **Cluster acceptance A1–A10 pass** (see §Test plan above).

3. **`apps/docs/blocks/reports/README.md` published** (ships in PR 7).

4. **Determinism harness coverage:** every shipped cartridge has a `*DeterminismTests` test class extending `ReportCartridgeDeterminismTests<>`. The cluster-acceptance reflection scan (A9) asserts this.

5. **Tenant-isolation invariant verified:** every cartridge has at least one `*FromDifferentTenant_*` test case in its tests file.

6. **No code in `@sunfish/contracts` or Bridge has been touched** by this hand-off's PRs. The v1 Rent Roll rewire is its own follow-on.

7. **`active-workstreams.md`** row for W#60 P4 / blocks-reports updated with `built` status + the merged PR numbers. (XO handles the ledger edit after CO review per the multi-session coordination protocol; sunfish-PM authors only the source-row change.)

8. **`coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w60-p4-reports-built.md`** beacon dropped with PR numbers + the test-count + whether PR 4 was deferred + a one-line pointer to the follow-on Rent Roll rewire hand-off (if XO has authored it; otherwise note "follow-on pending XO").

When the PASS gate is met, the next Phase 3 work in line picks up:

- **Rent Roll v1 → v2 rewire** (Bridge + React)
- **`blocks-reports-pdf`** (PDF cartridges)
- **`blocks-reports-tax`** (Schedule E + 1099 — retires `blocks-tax-reporting/`)
- **`blocks-reports-dashboards`** (Dashboard + KPI entities)
- **`blocks-reports-scheduling`** (ReportRun persistence + RRULE scheduling)

---

## Cited-symbol verification (XO-authored discipline)

Per `feedback_council_can_miss_spot_check_negative_existence` — symbols cited in this hand-off that COB must verify exist (or halt if missing):

| Symbol | Cluster | Verification command |
|---|---|---|
| `TenantId` (record struct) | `foundation-events` or `foundation` | `grep -rln "record struct TenantId" packages/foundation*` |
| `PrincipalId` (record struct) | `foundation-events` or `foundation` | `grep -rln "record struct PrincipalId" packages/foundation*` |
| `IChartOfAccountsRepository` | `blocks-financial-ledger` | `grep -rln "interface IChartOfAccountsRepository" packages/blocks-financial-ledger` |
| `IGeneralLedgerReadModel` | `blocks-financial-ledger` | `grep -rln "interface IGeneralLedgerReadModel" packages/blocks-financial-ledger` |
| `GLAccountId`, `ChartOfAccountsId`, `JournalEntryId` | `blocks-financial-ledger` | `ls packages/blocks-financial-ledger/Models/{GLAccountId,ChartOfAccountsId,JournalEntryId}.cs` |
| `IFiscalPeriodRepository`, `FiscalPeriod`, `FiscalPeriodId`, `FiscalPeriodStatus` | `blocks-financial-periods` | `ls packages/blocks-financial-periods/{Models,Services}/Fiscal*.cs` |
| `IArAgingService`, `ArAgingReport`, `ArAgingRow`, `AgingGroupBy` | `blocks-financial-ar` | `grep -rln "interface IArAgingService" packages/blocks-financial-ar` |
| `IApAgingService`, `ApAgingReport`, `ApAgingRow`, `ApAgingGroupBy` | `blocks-financial-ap` | `grep -rln "interface IApAgingService" packages/blocks-financial-ap` |
| `IInvoiceRepository`, `IBillRepository` | `blocks-financial-ar`, `blocks-financial-ap` | grep — optional, only if drill-down query needed |
| `ILeaseService`, `Lease`, `LeaseId` | `blocks-leases` | `ls packages/blocks-leases/Services/ILeaseService.cs` ✓ already verified on main |
| `PartyId` | `blocks-leases` (existing) or `blocks-people-foundation` (preferred) | `grep -rln "record struct PartyId" packages/blocks-leases packages/blocks-people-foundation` |
| `IPartyReadModel` | `blocks-people-foundation` (preferred) | `grep -rln "interface IPartyReadModel" packages/blocks-people-foundation` — if missing, use AR/AP local Party shim |
| `IClock` | `foundation` (typical) or NodaTime direct | `grep -rln "using NodaTime" packages/blocks-financial-ledger` to confirm the project uses NodaTime |

If any of the **bold** dependencies are missing, halt per §Halt conditions. The remaining are best-effort lookups; degrade gracefully per the cartridge's `Do NOT` rules.

---

## Cohort discipline

This hand-off is part of the **Phase 3 reports + docs cohort** (parallel `blocks-docs` hand-off). The two clusters do not share surfaces; either can ship first.

If `blocks-docs` ships first, the docs page for `blocks-reports` (PR 7 of this hand-off) can reference the docs cluster's templates. If `blocks-docs` ships later, PR 7's README is plain Markdown — no docs-cluster dependency.

---

## Beacon protocol

Per the multi-session coordination protocol (`/Users/christopherwood/Projects/SunfishSoftware/CLAUDE.md` §Multi-Session Coordination):

- **At session start** (especially after `/compact`): batch-run `git log --all`, `git status`, `gh pr list`, `but status`, and tail `.wolf/memory.md` before acting on anything in this hand-off marked "pending."
- **Question beacons:** `coordination/inbox/cob-question-2026-05-XXTHH-MMZ-w60-p4-reports-{topic}.md` — body: 3-line YAML frontmatter (`type: question`, `workstream-or-chapter: W60-P4-reports`, `last-pr: cob/blocks-reports-{...}`) + ≤2 lines context + ≤2 lines "what would unblock me."
- **Status beacons** (per-PR merge): `coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w60-p4-reports-pr{N}-merged.md` — for each PR merge. Acceptable to batch into one cluster-level beacon when 3+ PRs merge in the same session.
- **Final PASS-gate beacon:** `coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w60-p4-reports-built.md` — drops when PR 7 merges + ledger flip authored by XO.
- **Idle:** if PRs 2, 3, 5, 6 are all merged and PR 4 is waiting on AP, drop `coordination/inbox/cob-idle-2026-05-XXTHH-MMZ-w60-p4-reports-awaiting-ap.md` + `ScheduleWakeup 1800s` per the fallback work order.

---

## Cross-references

- Stage 02 design: [`icm/02_architecture/blocks-reports-schema-design.md`](../../02_architecture/blocks-reports-schema-design.md)
- ADR 0088 Path II: [`docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md)
- CRDT conventions: [`icm/02_architecture/path-ii-crdt-schema-conventions.md`](../../02_architecture/path-ii-crdt-schema-conventions.md)
- Event-bus vocabulary: [`icm/02_architecture/path-ii-cross-cluster-event-bus.md`](../../02_architecture/path-ii-cross-cluster-event-bus.md)
- Sibling hand-offs (predecessors): `blocks-financial-ledger-chart-and-journal-stage06-handoff.md`, `blocks-financial-periods-stage06-handoff.md`, `blocks-financial-tax-stage06-handoff.md`, `blocks-financial-ar-stage06-handoff.md`, `blocks-financial-ap-stage06-handoff.md`, `blocks-people-foundation-stage06-handoff.md`
- v1 Rent Roll source: `icm/_state/handoffs/w60-reporting-contracts-phase5-stage06-handoff.md` (PR 2 — Rent roll + P&L reporting)
- Coordination README: `/Users/christopherwood/Projects/SunfishSoftware/coordination/README.md`

---

**End of hand-off.**
