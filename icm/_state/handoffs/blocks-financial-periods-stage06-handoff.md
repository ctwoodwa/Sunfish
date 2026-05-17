# Hand-off — `blocks-financial-periods` FiscalYear + FiscalPeriod + Period-Close (Phase 1 foundational)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build`
**Workstream:** W#60 P4 — Path II native domain, second cluster unit (foundational; period management)
**Spec source:** [`icm/02_architecture/blocks-financial-schema-design.md`](../../02_architecture/blocks-financial-schema-design.md) §3.15, §3.16, §5.4, §6.5, §8.5, §9, §10
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) (Proposed; ratified by CO 2026-05-16)
**Sibling hand-off:** [`blocks-financial-ledger-chart-and-journal-stage06-handoff.md`](./blocks-financial-ledger-chart-and-journal-stage06-handoff.md) (ledger; PRs 1–6)
**Conventions:** [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md) §6 (posted-then-immutable), §7 (state machines under CRDT — Pattern A designated authority), §1 (ULID); [`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md) §3.1 (`Financial.*` catalog)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~6–8h sunfish-PM (new package scaffold + 2 entities + close service + retained-earnings rollover + importer hooks + ~25–30 tests + docs)
**PR count:** 4 PRs (PR 1 scaffold + entities; PR 2 soft-close service + period-gating; PR 3 hard-close + year-end rollover; PR 4 ERPNext importer hooks)
**Pre-merge council:** NOT required (substrate scope; mirrors the W#34/W#35/W#36 substrate-only pattern + sibling ledger hand-off). Standard COB self-audit applies.
**Audit before build:** `ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-financial-(periods|ledger)"` — expect `blocks-financial-ledger/` present (after sibling PR 1) and `blocks-financial-periods/` absent.

---

## Context

### Path II reframe

W#60 ratified Path II via ADR 0088 on 2026-05-16: **Anchor is the all-in-one
local-first runtime.** SQLite is the primary store; Loro CRDT layers on top
for peer-to-peer sync; the native domain is implemented in Sunfish
`blocks-*` clusters with no external engine.

The financial domain partitions into 7 Phase 1-core sub-clusters (per ADR
0088 §1 + the Stage 02 design §1 cluster split):

| Sub-cluster | Scope |
|---|---|
| `blocks-financial-ledger` | GLAccount + JournalEntry + posting service (sibling hand-off — PRs 1–6) |
| `blocks-financial-chart` | (Optional split-out) ChartOfAccounts management UI |
| **`blocks-financial-periods`** | **FiscalYear + FiscalPeriod + close machinery + retained-earnings rollover (this hand-off)** |
| `blocks-financial-ar` | Invoice + InvoiceLine + AR aging |
| `blocks-financial-ap` | Bill + BillLine |
| `blocks-financial-payments` | Payment + PaymentApplication + bank reconciliation |
| `blocks-financial-tax` | TaxCode + TaxRate + TaxJurisdiction |

This hand-off is the **second cluster unit** in the Phase 1 critical path.
It is **independent** of sibling `-ledger` PRs 3–4 in the sense that no
PR in this hand-off requires `JournalPostingService` to compile (the
opposite is true; the ledger's `IPeriodResolver` resolves through this
package once it ships). Suggested sequencing: cob ships ledger PRs 1–2
first (rename + GLAccount extensions establish the `blocks-financial-*`
neighborhood + `ChartOfAccountsId`), then this hand-off's PR 1 can land
**in parallel with** ledger PRs 3–4 (JournalEntry extensions + posting
service stub). When this hand-off's PR 2–4 land, the sibling ledger's
`InMemoryPeriodResolver` stub gets superseded via DI swap.

### Why periods is the second cluster unit up

1. **Topological dependency.** Every Phase 1 financial transaction posts
   *through* a `FiscalPeriod` (per Stage 02 §3.3 — `JournalEntry.periodId`
   is non-null, pinned at post time; §6.1 Phase 4 period-gating is the
   fourth precondition of the posting algorithm).
2. **Period-close is the year-end gate.** Without `closeFiscalYear` and
   the retained-earnings rollover (Stage 02 §6.5), the chart cannot start
   a new fiscal year cleanly — Income/Expense balances would never zero
   out and Retained Earnings would never accumulate. The Schedule E
   generator (Stage 02 §6.6) explicitly requires
   `fy.status == "Closed"` before producing a final return.
3. **CRDT-discipline gate.** FiscalPeriod's status transition
   (`Open → SoftClosed → Locked`) is the cluster's canonical example of
   **Pattern A — Designated authority** under
   `crdt-friendly-schema-conventions.md` §7. Specifying it correctly here
   sets the precedent for the whole cluster's state-machine handling.
4. **Migration importer dependency.** ERPNext's `Fiscal Year` doctype
   maps to `FiscalYear` per Stage 02 §10.1; the `importPeriods` Pass 1
   step (§10.3) synthesizes monthly `FiscalPeriod` entities from the
   imported FYs. This hand-off ships the importer hooks the orchestrator
   will consume.
5. **No carve-out from sibling ledger.** `FiscalPeriodId` is forward-
   declared in `blocks-financial-ledger` PR 3 as a strongly-typed ULID
   id (sibling hand-off §"PR 3 — JournalEntry + JournalEntryLine schema
   extensions" → "**`Models/FiscalPeriodId.cs`** — ULID strongly-typed
   id"). This hand-off's PR 1 **does NOT redefine `FiscalPeriodId`**; it
   re-uses the id type from `blocks-financial-ledger` via project
   reference. The entity (`FiscalPeriod`) plus `FiscalYear` + `FiscalYearId`
   live in **this** package.

### Naming + scope (binding)

- Package name: **`blocks-financial-periods`** (matches Stage 02 §1
  cluster table line `blocks-financial-periods | FiscalYear + FiscalPeriod
  + period-close machinery. | 1`).
- C# namespace: **`Sunfish.Blocks.FinancialPeriods`**.
- csproj name: **`Sunfish.Blocks.FinancialPeriods.csproj`**.
- DI extension: **`AddBlocksFinancialPeriods()`**.
- The `FiscalPeriodStatus` enum is **defined here** (`Open | SoftClosed |
  Locked`); the local placeholder in `blocks-financial-ledger` PR 4 (per
  the sibling hand-off's "Supporting stubs" section) gets **deleted** when
  this hand-off's PR 2 lands and the real `IPeriodResolver` ships. PR 2
  of this hand-off explicitly carries the deletion of the placeholder
  enum + redirects the ledger's `using` to point at this package.
- The `IPeriodResolver` interface **stays in `blocks-financial-ledger`**
  (it is the consumer-facing contract). The **implementation**
  (`SqlitePeriodResolver` — real DB-backed lookup) lives here and is
  registered in DI by `AddBlocksFinancialPeriods()`. The sibling ledger
  package's `InMemoryPeriodResolver` stays as the test-only fallback.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify sibling `-ledger` PRs 1–2 merged.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ \
       | grep -E "^blocks-financial-(ledger|periods)"
   ```
   Expected: `blocks-financial-ledger/` present; `blocks-financial-periods/`
   absent. If `-ledger` is still named `blocks-accounting/`, halt — sibling
   PR 1 (rename) must merge before this hand-off proceeds.

2. **Verify `ChartOfAccountsId` + `FiscalPeriodId` types exist in
   `blocks-financial-ledger`.**
   ```bash
   grep -l "ChartOfAccountsId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Models/
   grep -l "FiscalPeriodId"   /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Models/
   ```
   Expected: both present after sibling PRs 2 + 3 merge. If not, halt + file
   `cob-question-*-w60-p4-periods-blocked-on-ledger-ids.md`.

3. **Confirm ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Proposed`. The hand-off is `ready-to-build` even with
   `status: Proposed` because the CO directive in the inbox is operative
   (sibling ledger hand-off precedent).

4. **Confirm no parallel-session PRs touch `blocks-financial-*`.**
   ```bash
   gh pr list --state open --search "blocks-financial in:title,body"
   gh pr list --state open --search "blocks-accounting in:title,body"
   gh pr list --state open --search "FiscalPeriod  in:title,body"
   gh pr list --state open --search "FiscalYear    in:title,body"
   ```
   Expected: only the sibling ledger hand-off's PRs (if still in flight).
   Anything else → file `cob-question-*` before starting PR 1.

5. **Check Beancount/Ledger-cli source-isolation discipline.**
   This hand-off applies clean-room discipline against Beancount + GnuCash
   (GPLv2) for period-close patterns. Before any PR work:
   - **Close any editor session that has Beancount/GnuCash/ERPNext source
     open.**
   - The schema in Stage 02 §3.15–§3.16 + §6.5 is the **clean-room source
     of truth**. Do not consult upstream GPL sources during implementation.

6. **Confirm `apps/docs/blocks-financial-ledger/overview.md` is in place
   (sibling PR 5).** This hand-off's PR 1 introduces a sibling
   `apps/docs/blocks-financial-periods/overview.md` page following the
   same template. If the convention has drifted, file `cob-question-*`.

7. **Confirm `but status` (or `git status`) is clean** and current branch
   is `main` (or a fresh worktree from `main` per
   `feedback_worktree_base_main_not_gitbutler`).

---

## Per-PR deliverables

This hand-off splits into **4 PRs** by responsibility:

| PR | Scope | Effort | Depends on |
|---|---|---|---|
| PR 1 | Package scaffold + FiscalYear + FiscalPeriod entities + status enums + creation/validation | ~2h | sibling `-ledger` PRs 1+2+3 |
| PR 2 | `IPeriodCloseService` soft-close + `SqlitePeriodResolver` + period-gating wire-through; supersedes ledger's placeholder enum | ~1.5–2h | PR 1 |
| PR 3 | Hard-close + year-end retained-earnings rollover (creates closing JE) + reopen-year flow | ~2h | PR 2 + sibling `-ledger` PR 4 (`JournalPostingService` available) |
| PR 4 | ERPNext importer entry-points (`IErpnextFiscalYearImporter` + `IErpnextFiscalPeriodImporter`) + idempotent upsert | ~1.5h | PR 3 |

PRs are strictly sequential by dependency. PR 1 can land **in parallel
with** sibling ledger PRs 3–4 once ledger PRs 1–2 are in.

---

### PR 1 — Package scaffold + FiscalYear + FiscalPeriod entities

**Estimated effort:** ~2h
**Scope:** new package; `FiscalYear`, `FiscalYearId`, `FiscalYearStatus`;
`FiscalPeriod`, `FiscalPeriodKind`, `FiscalPeriodStatus`; factory +
validation helpers; package README + docs page; **no service surface**
**Commit subject:** `feat(blocks-financial-periods): add FiscalYear + FiscalPeriod entities per Stage 02 §3.15–§3.16`
**Branch:** `cob/blocks-financial-periods-entities`

#### Directory scaffold

```
packages/blocks-financial-periods/
  Sunfish.Blocks.FinancialPeriods.csproj
  README.md
  NOTICE.md                                      # OFBiz attribution (see License posture)
  Models/
    FiscalYearId.cs                              # ULID strongly-typed id
    FiscalYear.cs                                # record per §3.15
    FiscalYearStatus.cs                          # Open | Closed
    FiscalPeriod.cs                              # record per §3.16
    FiscalPeriodKind.cs                          # Monthly | Quarterly | Annual | Custom
    FiscalPeriodStatus.cs                        # Open | SoftClosed | Locked
  Services/
    (empty — services land in PR 2+3)
  DependencyInjection/
    ServiceCollectionExtensions.cs               # AddBlocksFinancialPeriods() — registrations land in PR 2
  Migration/
    (empty — importer hooks land in PR 4)
  tests/
    Sunfish.Blocks.FinancialPeriods.Tests.csproj
    FiscalYearTests.cs
    FiscalPeriodTests.cs
    FiscalPeriodValidationTests.cs
```

#### `FiscalYearId.cs`

ULID strongly-typed id; mirrors `ChartOfAccountsId` pattern from sibling
ledger PR 2.

```csharp
namespace Sunfish.Blocks.FinancialPeriods;

public readonly record struct FiscalYearId(string Value)
{
    public static FiscalYearId New() => new(Ulid.NewUlid().ToString());
}
```

(If the project uses a different ULID library or wrapper pattern, follow
the convention already in use in `blocks-financial-ledger/Models/`.)

#### `FiscalYearStatus.cs`

Per Stage 02 §3.15:

```csharp
namespace Sunfish.Blocks.FinancialPeriods;

public enum FiscalYearStatus
{
    Open,        // Periods within may be Open, SoftClosed, or (rarely) Locked
    Closed,      // closeFiscalYear() executed; closing JE posted; all periods Locked
}
```

#### `FiscalYear.cs`

Record per Stage 02 §3.15:

```csharp
namespace Sunfish.Blocks.FinancialPeriods;

using NodaTime;
using Sunfish.Blocks.FinancialLedger;          // ChartOfAccountsId
                                                // JournalEntryId (closingJournalEntryId)

public sealed record FiscalYear(
    FiscalYearId Id,
    ChartOfAccountsId ChartId,
    string Label,                              // "2026", "FY26", "FY26 (Apr2026-Mar2027)"
    DateOnly StartDate,
    DateOnly EndDate,                          // inclusive
    FiscalYearStatus Status,
    Instant? ClosedAtUtc,
    JournalEntryId? ClosingJournalEntryId,
    Instant CreatedAtUtc)
{
    public static FiscalYear CreateOpen(
        FiscalYearId id,
        ChartOfAccountsId chartId,
        string label,
        DateOnly startDate,
        DateOnly endDate,
        Instant? createdAtUtc = null) =>
        new(
            Id: id,
            ChartId: chartId,
            Label: label,
            StartDate: startDate,
            EndDate: endDate,
            Status: FiscalYearStatus.Open,
            ClosedAtUtc: null,
            ClosingJournalEntryId: null,
            CreatedAtUtc: createdAtUtc ?? SystemClock.Instance.GetCurrentInstant());
}
```

**Validation (helper method `FiscalYear.Validate`):**

1. `StartDate <= EndDate`.
2. Label non-empty.
3. If `Status == Closed`, `ClosedAtUtc` must be non-null and
   `ClosingJournalEntryId` should be non-null (soft-warn, not throw — the
   import path may not yet have synthesized the closing JE).
4. If `Status == Open`, both `ClosedAtUtc` and `ClosingJournalEntryId`
   must be null.

#### `FiscalPeriodKind.cs`

```csharp
public enum FiscalPeriodKind
{
    Monthly,     // 12 periods per year
    Quarterly,   //  4 periods per year
    Annual,      //  1 period per year
    Custom,      //  any other period scheme (e.g. 4-4-5 retail calendar)
}
```

#### `FiscalPeriodStatus.cs`

Per Stage 02 §3.16:

```csharp
public enum FiscalPeriodStatus
{
    Open,         // postings allowed
    SoftClosed,   // postings blocked for regular users; admin can reopen
    Locked,       // immutable; reopening requires explicit unlock-with-audit
}
```

#### `FiscalPeriod.cs`

Record per Stage 02 §3.16:

```csharp
namespace Sunfish.Blocks.FinancialPeriods;

using NodaTime;
using Sunfish.Blocks.FinancialLedger;          // ChartOfAccountsId, FiscalPeriodId, JournalEntryId

public sealed record FiscalPeriod(
    FiscalPeriodId Id,
    ChartOfAccountsId ChartId,
    FiscalYearId FiscalYearId,
    FiscalPeriodKind Kind,
    string Label,                              // "2026-01", "Q1-2026", "FY2026"
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalPeriodStatus Status,
    Instant? SoftClosedAtUtc,
    Instant? LockedAtUtc,
    JournalEntryId? ClosingJournalEntryId,
    Instant CreatedAtUtc)
{
    public static FiscalPeriod CreateOpen(
        FiscalPeriodId id,
        ChartOfAccountsId chartId,
        FiscalYearId fiscalYearId,
        FiscalPeriodKind kind,
        string label,
        DateOnly startDate,
        DateOnly endDate,
        Instant? createdAtUtc = null) =>
        new(
            Id: id,
            ChartId: chartId,
            FiscalYearId: fiscalYearId,
            Kind: kind,
            Label: label,
            StartDate: startDate,
            EndDate: endDate,
            Status: FiscalPeriodStatus.Open,
            SoftClosedAtUtc: null,
            LockedAtUtc: null,
            ClosingJournalEntryId: null,
            CreatedAtUtc: createdAtUtc ?? SystemClock.Instance.GetCurrentInstant());

    public bool Contains(DateOnly d) =>
        d >= StartDate && d <= EndDate;
}
```

**Validation (helper method `FiscalPeriod.Validate`):**

1. `StartDate <= EndDate`.
2. Label non-empty.
3. `FiscalYearId` non-default (validates against an external resolver in
   PR 2; PR 1's helper only checks shape).
4. If `Status == SoftClosed`, `SoftClosedAtUtc` non-null and `LockedAtUtc`
   null.
5. If `Status == Locked`, both `SoftClosedAtUtc` and `LockedAtUtc` non-null.
6. (Cross-period invariant — Stage 02 §3.16 rule 1.) Periods within the
   same `FiscalYearId` MUST be contiguous and non-overlapping. Enforced
   collection-level by the validator (see `FiscalPeriodCollectionValidator`
   below).
7. (Cross-period invariant — Stage 02 §3.16 rule 2.) The union of all
   periods in a year exactly covers `[FiscalYear.StartDate,
   FiscalYear.EndDate]`. Collection-level.

#### `FiscalPeriodCollectionValidator` (static helper)

Validates a `IReadOnlyList<FiscalPeriod>` for a given `FiscalYear`:

```csharp
public static class FiscalPeriodCollectionValidator
{
    public sealed record ValidationResult(
        bool IsValid,
        IReadOnlyList<string> Errors);

    public static ValidationResult Validate(
        FiscalYear fiscalYear,
        IReadOnlyList<FiscalPeriod> periods)
    {
        var errors = new List<string>();

        // Rule: contiguous, non-overlapping (sorted by StartDate)
        var sorted = periods
            .OrderBy(p => p.StartDate)
            .ToList();

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i].EndDate.AddDays(1) != sorted[i + 1].StartDate)
                errors.Add(
                    $"Period gap or overlap between {sorted[i].Label} " +
                    $"(ends {sorted[i].EndDate:O}) and {sorted[i + 1].Label} " +
                    $"(starts {sorted[i + 1].StartDate:O}).");
        }

        // Rule: union covers FY span
        if (sorted.Count > 0)
        {
            if (sorted.First().StartDate != fiscalYear.StartDate)
                errors.Add(
                    $"First period {sorted.First().Label} starts " +
                    $"{sorted.First().StartDate:O}; FY {fiscalYear.Label} " +
                    $"starts {fiscalYear.StartDate:O}.");
            if (sorted.Last().EndDate != fiscalYear.EndDate)
                errors.Add(
                    $"Last period {sorted.Last().Label} ends " +
                    $"{sorted.Last().EndDate:O}; FY {fiscalYear.Label} " +
                    $"ends {fiscalYear.EndDate:O}.");
        }

        // Rule: status discipline — Locked only if FY is Closed
        // (Stage 02 §3.16 rule 3)
        foreach (var p in sorted)
        {
            if (p.Status == FiscalPeriodStatus.Locked &&
                fiscalYear.Status != FiscalYearStatus.Closed)
                errors.Add(
                    $"Period {p.Label} is Locked but FY {fiscalYear.Label} " +
                    "is Open. Locking is only valid as part of year-close.");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}
```

#### `FiscalPeriodFactory` (static helper)

Synthesizes a monthly period set for a given FY. Used by the ERPNext
importer in PR 4 and by manual FY creation flows.

```csharp
public static class FiscalPeriodFactory
{
    public static IReadOnlyList<FiscalPeriod> BuildMonthlyPeriods(
        FiscalYear fy,
        Instant? createdAtUtc = null)
    {
        var periods = new List<FiscalPeriod>();
        var cursor = fy.StartDate;
        var monthIndex = 1;

        while (cursor <= fy.EndDate)
        {
            // Last day of the month containing `cursor`, or fy.EndDate,
            // whichever is earlier.
            var endOfMonth = new DateOnly(
                cursor.Year, cursor.Month,
                DateTime.DaysInMonth(cursor.Year, cursor.Month));
            var periodEnd = endOfMonth < fy.EndDate ? endOfMonth : fy.EndDate;

            periods.Add(FiscalPeriod.CreateOpen(
                id: FiscalPeriodId.New(),
                chartId: fy.ChartId,
                fiscalYearId: fy.Id,
                kind: FiscalPeriodKind.Monthly,
                label: $"{fy.Label}-M{monthIndex:D2}",
                startDate: cursor,
                endDate: periodEnd,
                createdAtUtc: createdAtUtc));

            cursor = periodEnd.AddDays(1);
            monthIndex++;
        }

        return periods;
    }

    public static IReadOnlyList<FiscalPeriod> BuildQuarterlyPeriods(
        FiscalYear fy,
        Instant? createdAtUtc = null) { /* analogous to monthly, 4 chunks */ ... }

    public static IReadOnlyList<FiscalPeriod> BuildAnnualPeriod(
        FiscalYear fy,
        Instant? createdAtUtc = null) =>
        new[]
        {
            FiscalPeriod.CreateOpen(
                id: FiscalPeriodId.New(),
                chartId: fy.ChartId,
                fiscalYearId: fy.Id,
                kind: FiscalPeriodKind.Annual,
                label: fy.Label,
                startDate: fy.StartDate,
                endDate: fy.EndDate,
                createdAtUtc: createdAtUtc),
        };
}
```

#### `DependencyInjection/ServiceCollectionExtensions.cs`

Placeholder for PR 1 (no service registrations yet); the extension exists
so consumers can wire `AddBlocksFinancialPeriods()` even in PR 1 phase
without compile breakage:

```csharp
namespace Sunfish.Blocks.FinancialPeriods.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlocksFinancialPeriods(
        this IServiceCollection services)
    {
        // Service registrations land in PR 2 + PR 3 + PR 4.
        // PR 1 ships entities only; no DI surface required by them.
        return services;
    }
}
```

#### Tests (PR 1)

`tests/FiscalYearTests.cs`:

- `CreateOpen_PopulatesStatusOpen_AndNullCloseFields`.
- `CreateOpen_PopulatesCreatedAtUtc_WhenNotProvided`.
- `Validate_RejectsStartAfterEnd`.
- `Validate_RejectsEmptyLabel`.
- `Validate_RejectsClosedWithNullClosedAtUtc`.
- `Validate_RejectsOpenWithNonNullClosedAtUtc`.
- `Validate_AcceptsWellFormedOpenYear`.

`tests/FiscalPeriodTests.cs`:

- `CreateOpen_PopulatesStatusOpen_AndNullCloseFields`.
- `Contains_ReturnsTrueOnBoundaries` (start + end inclusive).
- `Contains_ReturnsFalseOutsideRange`.
- `Validate_RejectsStartAfterEnd`.
- `Validate_RejectsSoftClosedWithNullTimestamp`.
- `Validate_RejectsLockedWithNullSoftClosedTimestamp`.

`tests/FiscalPeriodValidationTests.cs` (collection-level):

- `Validate_AcceptsContiguousMonthlyPeriods_FullCalendarYear`.
- `Validate_RejectsGapBetweenPeriods` (Jan + Mar with no Feb).
- `Validate_RejectsOverlappingPeriods` (two periods both covering Feb).
- `Validate_RejectsPeriodSetStartingAfterFyStart`.
- `Validate_RejectsPeriodSetEndingBeforeFyEnd`.
- `Validate_RejectsLockedPeriodWhenFyOpen` (Stage 02 §3.16 rule 3).

`tests/FiscalPeriodFactoryTests.cs`:

- `BuildMonthlyPeriods_ProducesTwelveForCalendarYear`.
- `BuildMonthlyPeriods_ProducesContiguousNonOverlappingSet`
  (round-trip through the validator).
- `BuildMonthlyPeriods_LabelsAreFyLabelPlusMonthOrdinal`.
- `BuildQuarterlyPeriods_ProducesFour`.
- `BuildAnnualPeriod_ProducesOne`.

Total new tests this PR: **~18**.

#### Docs

`apps/docs/blocks-financial-periods/overview.md` — cluster docs page
following the sibling ledger doc template:

```markdown
# blocks-financial-periods

Period management for the Sunfish Anchor native financial domain.

## Overview

This package is the period-management layer of the `blocks-financial-*`
cluster per ADR 0088 §1. It provides:

- `FiscalYear` — a fiscal-year container (calendar year or shifted).
- `FiscalPeriod` — Monthly / Quarterly / Annual / Custom period within
  a fiscal year.
- `IPeriodCloseService` — soft-close + hard-close + year-end retained-
  earnings rollover (PR 2 + PR 3).
- `SqlitePeriodResolver` — date → period lookup; consumed by the
  ledger's `JournalPostingService` for period-gating (PR 2).
- `IErpnextFiscalYearImporter` + `IErpnextFiscalPeriodImporter` — ERPNext
  migration entry-points (PR 4).

## Period status state machine

| From → To | Posting allowed | Trigger | Reversibility |
|---|---|---|---|
| `Open → Open` | Yes | n/a | n/a |
| `Open → SoftClosed` | Blocked for non-admin | `softClosePeriod` | Admin reopen |
| `SoftClosed → Locked` | Blocked everyone | Year-end close | Admin unlock + audit |
| `Locked → SoftClosed` | After unlock | Admin unlock + audit memo | Audit-event recorded |

See [Stage 02 §5.4 + §8.5](../../icm/02_architecture/blocks-financial-schema-design.md).

## CRDT discipline

Period status transitions follow **Pattern A — Designated authority**
per `_shared/engineering/crdt-friendly-schema-conventions.md` §7: the
period-close action is performed by **one designated replica** (the
manager-app / FinancialAdmin replica). Other replicas observe the
status change propagate via Loro CRDT but never advance the state
locally. This avoids state-machine races at the cost of unavailability
when the designated replica is offline (mitigated by the UI surfacing
"period close pending propagation").

`FiscalPeriod` rows are **posted-then-immutable** once `Locked` per
§6 of the same conventions doc: once locked, the row never mutates;
unlocks generate audit-event records but don't modify the original
row in place.

## Events

Per `_shared/engineering/cross-cluster-event-bus-design.md` §3.1, this
package emits:

- `Financial.PeriodOpened` (PR 1 — new event introduced by this hand-off)
- `Financial.PeriodSoftClosed` (PR 2 — catalog §3.1)
- `Financial.PeriodLocked` (PR 3 — catalog §3.1)
- `Financial.YearClosed` (PR 3 — catalog §3.1)
- `Financial.YearEndRolloverCompleted` (PR 3 — new event introduced
  by this hand-off; companion to `Financial.YearClosed`)

## Quickstart

```csharp
var fy = FiscalYear.CreateOpen(
    id: FiscalYearId.New(),
    chartId: chart.Id,
    label: "2026",
    startDate: new DateOnly(2026, 1, 1),
    endDate:   new DateOnly(2026, 12, 31));

var periods = FiscalPeriodFactory.BuildMonthlyPeriods(fy);
// periods = 12 Open FiscalPeriod rows, contiguous, covering 2026.
```

## Related

- `blocks-financial-ledger` — owns `ChartOfAccountsId`, `JournalEntryId`,
  `FiscalPeriodId`, and the `IPeriodResolver` consumer-facing contract
  (`SqlitePeriodResolver` implementation lives here)
- `blocks-reports-tax` (Phase 1 follow-on) — `generateScheduleE` requires
  `fy.status == "Closed"`
- `blocks-financial-budget` (Phase 3) — `Budget.fiscalYearId` references
  `FiscalYear.Id`
```

#### Verification (PR 1)

- `dotnet build` succeeds on the new package.
- `dotnet test packages/blocks-financial-periods/tests/` passes ~18 tests.
- `grep -r "FiscalPeriodStatus" packages/blocks-financial-ledger/`
  returns only the placeholder enum in the ledger's PR 4 (deletion
  scheduled for PR 2 of this hand-off).
- `apps/docs/blocks-financial-periods/overview.md` renders.
- No `Sunfish.Blocks.FinancialPeriods` references in any consumer yet
  (introduced in PR 2 via the DI swap on the resolver).

#### Do NOT in this PR

- Do NOT add any service surface. Period-close + resolver live in PR 2+3.
- Do NOT delete the ledger's placeholder `FiscalPeriodStatus` yet — the
  ledger's `JournalPostingService` references it; PR 2 of this hand-off
  performs the swap atomically with the SqlitePeriodResolver landing.
- Do NOT emit any `Financial.PeriodOpened` events from `FiscalPeriod.
  CreateOpen` — events are emitted from the service layer (PR 2+3), not
  from record constructors. Constructors are pure (Stage 02 §3.3 +
  CRDT-conventions §6 discipline).

#### PR description template

```
Add blocks-financial-periods scaffold + FiscalYear + FiscalPeriod entities
per Stage 02 §3.15–§3.16 and ADR 0088 §1.

This is the entity-only PR for the new package. Service surface lands in
PR 2 (soft-close + resolver) + PR 3 (hard-close + year-end rollover).
ERPNext importer hooks land in PR 4.

- New package: `packages/blocks-financial-periods/`
- New types: FiscalYearId, FiscalYear, FiscalYearStatus, FiscalPeriod,
  FiscalPeriodKind, FiscalPeriodStatus
- New helpers: FiscalPeriodCollectionValidator, FiscalPeriodFactory
- Docs: apps/docs/blocks-financial-periods/overview.md
- ~18 unit tests covering entity validation + period-factory output

Refs: ADR 0088 §1; Stage 02 §3.15, §3.16, §5.4, §8.5; sibling hand-off
`blocks-financial-ledger-chart-and-journal-stage06-handoff.md`.
```

---

### PR 2 — `IPeriodCloseService` soft-close + `SqlitePeriodResolver`

**Estimated effort:** ~1.5–2h
**Scope:** soft-close algorithm (Stage 02 §6.5(a)); period resolver
(`IPeriodResolver` implementation that the sibling ledger consumes);
`Financial.PeriodOpened` + `Financial.PeriodSoftClosed` event emission;
delete the sibling ledger's placeholder `FiscalPeriodStatus` enum +
re-wire to this package
**Commit subject:** `feat(blocks-financial-periods): soft-close service + SQLite period resolver per Stage 02 §6.5(a)`
**Depends on:** PR 1 + sibling ledger PRs 3–4 merged
**Branch:** `cob/blocks-financial-periods-soft-close-and-resolver`

#### New service contracts

**`Services/IPeriodCloseService.cs`**:

```csharp
namespace Sunfish.Blocks.FinancialPeriods;

public interface IPeriodCloseService
{
    /// <summary>
    /// Soft-closes a period. Non-admin users can no longer post to it;
    /// admins can. Reversals remain allowed (per Stage 02 §6.5(a) +
    /// §6.1 Phase 4 logic in JournalPostingService).
    /// </summary>
    Task<PeriodCloseResult> SoftCloseAsync(
        FiscalPeriodId periodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a soft-closed period. Admin-only (caller responsibility
    /// to gate). Emits Financial.PeriodOpened.
    /// </summary>
    Task<PeriodCloseResult> ReopenAsync(
        FiscalPeriodId periodId,
        string auditMemo,
        CancellationToken cancellationToken = default);

    // Hard-close + closeFiscalYear + reopenFiscalYear: PR 3.
}

public enum PeriodCloseError
{
    None,
    PeriodNotFound,
    PeriodAlreadySoftClosed,
    PeriodLocked,                     // can't soft-close a locked period
    FiscalYearAlreadyClosed,          // can't reopen a period in a Closed FY
    AuditMemoRequired,                // reopen path
}

public readonly record struct PeriodCloseResult(
    FiscalPeriod? Period,
    PeriodCloseError Error,
    string? Detail);
```

#### Implementation: `Services/PeriodCloseService.cs`

```csharp
namespace Sunfish.Blocks.FinancialPeriods;

using System.Data;                              // IDbConnection
using NodaTime;
using Sunfish.Blocks.FinancialLedger;          // for cross-package events helper if any

public sealed class PeriodCloseService : IPeriodCloseService
{
    private readonly IDbConnection _db;
    private readonly IFiscalPeriodRepository _periods;
    private readonly IFiscalYearRepository _years;
    private readonly IDomainEventPublisher _events;
    private readonly IClock _clock;

    public PeriodCloseService(
        IDbConnection db,
        IFiscalPeriodRepository periods,
        IFiscalYearRepository years,
        IDomainEventPublisher events,
        IClock clock)
    {
        _db = db; _periods = periods; _years = years;
        _events = events; _clock = clock;
    }

    public async Task<PeriodCloseResult> SoftCloseAsync(
        FiscalPeriodId periodId,
        CancellationToken ct = default)
    {
        var period = await _periods.GetAsync(periodId, ct);
        if (period is null)
            return new(null, PeriodCloseError.PeriodNotFound, periodId.Value);
        if (period.Status == FiscalPeriodStatus.SoftClosed)
            return new(period, PeriodCloseError.PeriodAlreadySoftClosed, null);
        if (period.Status == FiscalPeriodStatus.Locked)
            return new(period, PeriodCloseError.PeriodLocked, null);

        var now = _clock.GetCurrentInstant();
        var updated = period with
        {
            Status = FiscalPeriodStatus.SoftClosed,
            SoftClosedAtUtc = now,
        };

        await using var tx = _db.BeginTransaction();
        try
        {
            await _periods.UpdateAsync(updated, tx, ct);
            await _events.PublishAsync(
                new Financial.PeriodSoftClosed(
                    PeriodId: updated.Id,
                    ChartId: updated.ChartId,
                    ClosedByPrincipalId: /* from IUserContext if wired */ null),
                ct);
            tx.Commit();
            return new(updated, PeriodCloseError.None, null);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<PeriodCloseResult> ReopenAsync(
        FiscalPeriodId periodId,
        string auditMemo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auditMemo))
            return new(null, PeriodCloseError.AuditMemoRequired, null);

        var period = await _periods.GetAsync(periodId, ct);
        if (period is null)
            return new(null, PeriodCloseError.PeriodNotFound, periodId.Value);
        if (period.Status != FiscalPeriodStatus.SoftClosed)
            // Reopen-from-Locked is PR 3's unlock-with-audit path.
            return new(period, PeriodCloseError.PeriodLocked, null);

        var fy = await _years.GetAsync(period.FiscalYearId, ct);
        if (fy is { Status: FiscalYearStatus.Closed })
            return new(period, PeriodCloseError.FiscalYearAlreadyClosed, null);

        var now = _clock.GetCurrentInstant();
        var updated = period with
        {
            Status = FiscalPeriodStatus.Open,
            SoftClosedAtUtc = null,
        };

        await using var tx = _db.BeginTransaction();
        try
        {
            await _periods.UpdateAsync(updated, tx, ct);
            await _events.PublishAsync(
                new Financial.PeriodOpened(
                    PeriodId: updated.Id,
                    ChartId: updated.ChartId,
                    Reason: $"Reopened by admin: {auditMemo}"),
                ct);
            tx.Commit();
            return new(updated, PeriodCloseError.None, null);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
```

(Code is illustrative; COB to fit project conventions — `Microsoft.Data.
Sqlite` direct usage vs an existing repository pattern.)

#### `SqlitePeriodResolver`

Implements the `IPeriodResolver` interface declared in
`blocks-financial-ledger`:

```csharp
namespace Sunfish.Blocks.FinancialPeriods;

using Sunfish.Blocks.FinancialLedger;          // IPeriodResolver contract

public sealed class SqlitePeriodResolver : IPeriodResolver
{
    private readonly IFiscalPeriodRepository _periods;

    public SqlitePeriodResolver(IFiscalPeriodRepository periods)
    {
        _periods = periods;
    }

    public async Task<IPeriodResolver.PeriodSnapshot?> ResolveAsync(
        string chartId,
        DateOnly entryDate,
        CancellationToken ct = default)
    {
        var period = await _periods.FindByChartAndDateAsync(
            new ChartOfAccountsId(chartId), entryDate, ct);
        if (period is null) return null;

        return new IPeriodResolver.PeriodSnapshot(
            PeriodId: period.Id.Value,
            ChartId: period.ChartId.Value,
            Status: period.Status switch
            {
                FiscalPeriodStatus.Open       => IPeriodResolver.Status.Open,
                FiscalPeriodStatus.SoftClosed => IPeriodResolver.Status.SoftClosed,
                FiscalPeriodStatus.Locked     => IPeriodResolver.Status.Locked,
                _ => IPeriodResolver.Status.Open,
            });
    }
}
```

**`IPeriodResolver` shape adjustment (delete + redirect, ledger side):**

PR 2 of this hand-off contains a small one-shot edit to
`packages/blocks-financial-ledger/Services/IPeriodResolver.cs`:

1. **Delete** the local `FiscalPeriodStatus` enum (the placeholder added
   in sibling ledger PR 4 per the sibling hand-off "Supporting stubs"
   section).
2. **Replace** with a nested `IPeriodResolver.Status` enum that
   `JournalPostingService` switches on, so the ledger package remains
   ignorant of `Sunfish.Blocks.FinancialPeriods` at compile time:

```csharp
// packages/blocks-financial-ledger/Services/IPeriodResolver.cs
public interface IPeriodResolver
{
    Task<PeriodSnapshot?> ResolveAsync(
        string chartId, DateOnly entryDate, CancellationToken ct = default);

    public readonly record struct PeriodSnapshot(
        string PeriodId, string ChartId, Status Status);

    public enum Status { Open, SoftClosed, Locked }
}
```

This **breaks no consumer**: the ledger's `JournalPostingService` PR 4
already switches on the local enum's three values; the redirect is
identifier-only. Existing tests of `InMemoryPeriodResolver` are updated
to construct `IPeriodResolver.Status.Open` instead of the deleted
`FiscalPeriodStatus.Open` — a mechanical find-and-replace.

**Cleansing check before merge:** `grep -r "namespace Sunfish.Blocks.FinancialLedger" --include="*.cs"
| xargs grep -l "FiscalPeriodStatus"` returns zero hits after this PR.
The `FiscalPeriodStatus` enum (the public one) lives only in
`Sunfish.Blocks.FinancialPeriods`.

#### `Financial.PeriodOpened` event (new type)

Add to a `Financial.cs` event-payload module (this package; sibling
ledger may also house some `Financial.*` payload types — pick one
location per project convention, prefer **this package** for
period-related events):

```csharp
namespace Sunfish.Blocks.FinancialPeriods.Financial;

public sealed record PeriodOpened(
    FiscalPeriodId PeriodId,
    ChartOfAccountsId ChartId,
    string? Reason);                 // null = fresh open; non-null = reopen reason
// event-type name: "Financial.PeriodOpened"
// idempotency-key:  $"period-opened:{PeriodId.Value}:{recordedAtUtc:O}"
```

**Catalog reconciliation:** The hand-off's prompt called for
`Financial.FiscalPeriodOpened` / `…SoftClosed` / `…HardClosed` /
`Financial.YearEndRolloverCompleted`. The canonical event-bus catalog
(`_shared/engineering/cross-cluster-event-bus-design.md` §3.1) already
defines:

- `Financial.PeriodSoftClosed` ✓ (use this name; not `FiscalPeriodSoftClosed`)
- `Financial.PeriodLocked` ✓ (use this name; corresponds to "hard close")
- `Financial.YearClosed` ✓ (use this name; not `YearEndRolloverCompleted`)

**Rule (§2 of event-bus design — "no rename"):** we use the canonical
catalog names. The hand-off introduces **two new events** not in the
existing catalog:

- `Financial.PeriodOpened` — new in PR 2 (fresh open or reopen).
- `Financial.YearEndRolloverCompleted` — new in PR 3 (companion to
  `Financial.YearClosed`; emitted *after* the closing JE posts, distinct
  from `YearClosed` which is emitted when FY status flips).

These two new types must be added to the §3.1 catalog **as part of PR 3
in this hand-off** (a docs-only edit to the event-bus design doc; see
"Cross-references" below). PR 2 emits only `PeriodOpened` +
`PeriodSoftClosed`.

#### `IDomainEventPublisher` interface

Defined where the cross-cluster event bus's surface lives. If
`foundation-events` or `kernel-events` is the home (Q1 of event-bus
design §10), depend on it. **If the host package isn't ratified yet**,
ship a minimal local interface:

```csharp
public interface IDomainEventPublisher
{
    Task PublishAsync<TPayload>(TPayload payload, CancellationToken ct = default);
}
```

…with a placeholder `NoopDomainEventPublisher` registered by
`AddBlocksFinancialPeriods()` for now. **File `cob-question-*` before
merging PR 2** if the foundation-events / kernel-events package is in
flight elsewhere — the registration target needs to be authoritative.

#### `IFiscalPeriodRepository` + `IFiscalYearRepository`

```csharp
public interface IFiscalPeriodRepository
{
    Task<FiscalPeriod?>             GetAsync(FiscalPeriodId id, CancellationToken ct = default);
    Task<IReadOnlyList<FiscalPeriod>> GetByFiscalYearAsync(FiscalYearId fyId, CancellationToken ct = default);
    Task<FiscalPeriod?>             FindByChartAndDateAsync(ChartOfAccountsId chartId, DateOnly d, CancellationToken ct = default);
    Task InsertAsync(FiscalPeriod period, IDbTransaction? tx = null, CancellationToken ct = default);
    Task UpdateAsync(FiscalPeriod period, IDbTransaction? tx = null, CancellationToken ct = default);
    Task<FiscalPeriod?> GetByExternalRefAsync(string externalRef, CancellationToken ct = default);
}

public interface IFiscalYearRepository
{
    Task<FiscalYear?>             GetAsync(FiscalYearId id, CancellationToken ct = default);
    Task<IReadOnlyList<FiscalYear>> GetByChartAsync(ChartOfAccountsId chartId, CancellationToken ct = default);
    Task InsertAsync(FiscalYear fy, IDbTransaction? tx = null, CancellationToken ct = default);
    Task UpdateAsync(FiscalYear fy, IDbTransaction? tx = null, CancellationToken ct = default);
    Task<FiscalYear?> GetByExternalRefAsync(string externalRef, CancellationToken ct = default);
}
```

Implementations: `SqliteFiscalPeriodRepository` + `SqliteFiscalYearRepository`
using `Microsoft.Data.Sqlite` (or the project's existing SQLite
abstraction; mirror the pattern adopted in ledger PR 4 if any).

#### Tests (PR 2)

`tests/PeriodCloseServiceTests.SoftClose.cs`:

- `SoftClose_OpenPeriod_TransitionsToSoftClosed`.
- `SoftClose_PopulatesSoftClosedAtUtc`.
- `SoftClose_AlreadySoftClosed_ReturnsAlreadySoftClosedError`.
- `SoftClose_LockedPeriod_ReturnsPeriodLockedError`.
- `SoftClose_UnknownPeriod_ReturnsPeriodNotFound`.
- `SoftClose_EmitsPeriodSoftClosedEvent`.

`tests/PeriodCloseServiceTests.Reopen.cs`:

- `Reopen_SoftClosedPeriod_TransitionsToOpen`.
- `Reopen_EmptyAuditMemo_ReturnsAuditMemoRequired`.
- `Reopen_LockedPeriod_ReturnsPeriodLocked`.
- `Reopen_PeriodInClosedFy_ReturnsFiscalYearAlreadyClosed`.
- `Reopen_EmitsPeriodOpenedEvent_WithReopenReason`.

`tests/SqlitePeriodResolverTests.cs`:

- `Resolve_DateWithinOpenPeriod_ReturnsOpenSnapshot`.
- `Resolve_DateOutsideAllPeriods_ReturnsNull`.
- `Resolve_DateOnBoundary_ReturnsContainingPeriod` (e.g., entryDate
  equals the period's startDate).
- `Resolve_DateOnEndBoundary_ReturnsContainingPeriod`.
- `Resolve_TranslatesSoftClosedStatusCorrectly`.
- `Resolve_TranslatesLockedStatusCorrectly`.

Total new tests this PR: **~17**.

#### Verification (PR 2)

- All PR 1 tests still pass.
- New PR 2 tests pass.
- Sibling ledger's `JournalPostingService` tests still pass (the
  identifier swap doesn't change behavior).
- `grep -r "FiscalPeriodStatus" packages/blocks-financial-ledger/`
  returns zero hits (the placeholder is deleted).
- A round-trip integration test: post a `JournalEntry` against a chart
  with an Open period → succeeds; soft-close the period → next post for
  a non-admin returns `PostError.PeriodSoftClosed`; admin post still
  succeeds.

#### Do NOT in this PR

- Do NOT implement hard-close or `closeFiscalYear`. Those are PR 3.
- Do NOT register a real `IDomainEventPublisher` if the foundation-events
  home isn't ratified — ship `NoopDomainEventPublisher` + file
  `cob-question-*` to confirm.
- Do NOT add `Financial.YearClosed` / `Financial.YearEndRolloverCompleted`
  types yet — PR 3.

---

### PR 3 — Hard-close + year-end retained-earnings rollover

**Estimated effort:** ~2h
**Scope:** `lockPeriod` (Stage 02 §8.5 row 3); `closeFiscalYear` (Stage 02
§6.5(b)); year-end retained-earnings rollover algorithm — generates the
closing `JournalEntry`; `reopenFiscalYear` (admin + audit memo);
`Financial.PeriodLocked`, `Financial.YearClosed`, +
`Financial.YearEndRolloverCompleted` event emission
**Commit subject:** `feat(blocks-financial-periods): year-end close + retained-earnings rollover per Stage 02 §6.5(b)`
**Depends on:** PR 2 + sibling ledger PR 4 (`JournalPostingService`)
**Branch:** `cob/blocks-financial-periods-year-end-close`

#### Extended `IPeriodCloseService`

```csharp
public interface IPeriodCloseService
{
    Task<PeriodCloseResult> SoftCloseAsync(FiscalPeriodId, CancellationToken = default);
    Task<PeriodCloseResult> ReopenAsync(FiscalPeriodId, string auditMemo, CancellationToken = default);

    // PR 3 additions:
    Task<PeriodCloseResult> LockAsync(
        FiscalPeriodId periodId, CancellationToken cancellationToken = default);

    Task<PeriodCloseResult> UnlockAsync(
        FiscalPeriodId periodId, string auditMemo, CancellationToken cancellationToken = default);
}

public interface IFiscalYearCloseService
{
    /// <summary>
    /// Closes a fiscal year per Stage 02 §6.5(b):
    /// 1. Soft-close any Open periods within the year.
    /// 2. Build + post a closing JournalEntry that zeroes Income/Expense
    ///    accounts into Retained Earnings.
    /// 3. Lock all periods in the year.
    /// 4. Flip FY.status to Closed; populate ClosedAtUtc + ClosingJournalEntryId.
    /// 5. Emit Financial.YearClosed + Financial.YearEndRolloverCompleted.
    /// </summary>
    Task<FiscalYearCloseResult> CloseFiscalYearAsync(
        FiscalYearId fyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Admin-only reopen path: requires audit memo; posts a reversal of
    /// the closing JournalEntry; flips periods Locked→SoftClosed and
    /// FY.status to Open.
    /// </summary>
    Task<FiscalYearCloseResult> ReopenFiscalYearAsync(
        FiscalYearId fyId, string auditMemo, CancellationToken cancellationToken = default);
}

public enum FiscalYearCloseError
{
    None,
    FiscalYearNotFound,
    FiscalYearAlreadyClosed,
    FiscalYearAlreadyOpen,                // reopen path
    RetainedEarningsAccountNotConfigured,
    ClosingJournalEntryFailed,
    AuditMemoRequired,
    ReversalEntryFailed,
}

public readonly record struct FiscalYearCloseResult(
    FiscalYear? FiscalYear,
    JournalEntryId? ClosingEntryId,
    FiscalYearCloseError Error,
    string? Detail);
```

#### Implementation: `Services/FiscalYearCloseService.cs`

Algorithm transcription from Stage 02 §6.5(b):

```csharp
public async Task<FiscalYearCloseResult> CloseFiscalYearAsync(
    FiscalYearId fyId, CancellationToken ct = default)
{
    var fy = await _years.GetAsync(fyId, ct);
    if (fy is null)
        return new(null, null, FiscalYearCloseError.FiscalYearNotFound, fyId.Value);
    if (fy.Status == FiscalYearStatus.Closed)
        return new(fy, null, FiscalYearCloseError.FiscalYearAlreadyClosed, null);

    var chart = await _charts.GetAsync(fy.ChartId, ct);
    if (chart?.RetainedEarningsAccountId is null)
        return new(fy, null,
            FiscalYearCloseError.RetainedEarningsAccountNotConfigured,
            "Chart of accounts lacks a designated retained-earnings account.");

    // Step 1 — ensure all periods at least SoftClosed.
    var periods = await _periods.GetByFiscalYearAsync(fy.Id, ct);
    foreach (var p in periods.Where(p => p.Status == FiscalPeriodStatus.Open))
        await _periodClose.SoftCloseAsync(p.Id, ct);

    // Step 2 — build the closing JE.
    var asOfDate = fy.EndDate;
    var incomeAccounts  = await _accounts.GetByTypeAsync(
        fy.ChartId, GLAccountType.Revenue, ct);
    var expenseAccounts = await _accounts.GetByTypeAsync(
        fy.ChartId, GLAccountType.Expense, ct);

    var closingLines = new List<JournalEntryLine>();
    decimal incomeTotal = 0m, expenseTotal = 0m;

    foreach (var acct in incomeAccounts)
    {
        var bal = await _balances.ComputeAsOfAsync(acct.Id, asOfDate, ct);
        if (bal == 0m) continue;
        incomeTotal += bal;
        // Income accounts normally credit-balance; debit to zero them out.
        closingLines.Add(new JournalEntryLine(
            Id: JournalEntryLineId.New(),
            AccountId: acct.Id,
            Debit:  bal > 0m ? bal : 0m,
            Credit: bal < 0m ? -bal : 0m,    // negative income → credit it
            LineMemo: $"Year-end close to retained earnings — {fy.Label}"));
    }
    foreach (var acct in expenseAccounts)
    {
        var bal = await _balances.ComputeAsOfAsync(acct.Id, asOfDate, ct);
        if (bal == 0m) continue;
        expenseTotal += bal;
        // Expense accounts normally debit-balance; credit to zero them out.
        closingLines.Add(new JournalEntryLine(
            Id: JournalEntryLineId.New(),
            AccountId: acct.Id,
            Debit:  bal < 0m ? -bal : 0m,    // negative expense → debit
            Credit: bal > 0m ? bal : 0m,
            LineMemo: $"Year-end close to retained earnings — {fy.Label}"));
    }

    var netIncome = incomeTotal - expenseTotal;     // positive = profit
    if (netIncome > 0m)
    {
        closingLines.Add(new JournalEntryLine(
            Id: JournalEntryLineId.New(),
            AccountId: chart.RetainedEarningsAccountId.Value,
            Debit: 0m, Credit: netIncome,
            LineMemo: "Net income to retained earnings"));
    }
    else if (netIncome < 0m)
    {
        closingLines.Add(new JournalEntryLine(
            Id: JournalEntryLineId.New(),
            AccountId: chart.RetainedEarningsAccountId.Value,
            Debit: -netIncome, Credit: 0m,
            LineMemo: "Net loss to retained earnings"));
    }

    if (closingLines.Count == 0)
    {
        // Zero-activity year: nothing to roll. Skip the JE but still
        // lock periods + close FY for hygiene.
        return await FinalizeWithoutClosingEntryAsync(fy, periods, ct);
    }

    // Step 3 — build + post the closing JE (uses sibling ledger's
    // JournalPostingService).
    var closingEntry = new JournalEntry(
        id: JournalEntryId.New(),
        entryDate: fy.EndDate,
        memo: $"Year-end closing entry — {fy.Label}",
        lines: closingLines,
        createdAtUtc: _clock.GetCurrentInstant())
    {
        ChartId    = fy.ChartId,
        Status     = JournalEntryStatus.Draft,
        SourceKind = JournalEntrySource.Closing,
    };

    var postResult = await _posting.PostAsync(closingEntry, ct);
    if (postResult.Error != PostError.None)
        return new(fy, null,
            FiscalYearCloseError.ClosingJournalEntryFailed,
            $"{postResult.Error}: {postResult.Detail}");

    var postedClosing = postResult.Entry!;

    // Step 4 — lock all periods + flip FY status (atomic).
    var now = _clock.GetCurrentInstant();
    await using var tx = _db.BeginTransaction();
    try
    {
        foreach (var p in periods)
        {
            var locked = p with
            {
                Status = FiscalPeriodStatus.Locked,
                SoftClosedAtUtc = p.SoftClosedAtUtc ?? now,
                LockedAtUtc = now,
                ClosingJournalEntryId =
                    p.EndDate == fy.EndDate ? postedClosing.Id : p.ClosingJournalEntryId,
            };
            await _periods.UpdateAsync(locked, tx, ct);
            await _events.PublishAsync(
                new Financial.PeriodLocked(
                    PeriodId: locked.Id, ChartId: locked.ChartId),
                ct);
        }

        var closedFy = fy with
        {
            Status = FiscalYearStatus.Closed,
            ClosedAtUtc = now,
            ClosingJournalEntryId = postedClosing.Id,
        };
        await _years.UpdateAsync(closedFy, tx, ct);

        // Step 5 — emit YearClosed + YearEndRolloverCompleted.
        await _events.PublishAsync(
            new Financial.YearClosed(
                FyId: closedFy.Id, ChartId: closedFy.ChartId,
                ClosingEntryId: postedClosing.Id),
            ct);
        await _events.PublishAsync(
            new Financial.YearEndRolloverCompleted(
                FyId: closedFy.Id, ChartId: closedFy.ChartId,
                ClosingEntryId: postedClosing.Id,
                NetIncome: netIncome,
                IncomeAccountsClosed: incomeAccounts.Count,
                ExpenseAccountsClosed: expenseAccounts.Count),
            ct);

        tx.Commit();
        return new(closedFy, postedClosing.Id, FiscalYearCloseError.None, null);
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}
```

The `LockAsync` + `UnlockAsync` methods on `IPeriodCloseService` follow
the same pattern; `LockAsync` is permitted only as a step of year-close
(or for already-SoftClosed-final-of-the-year periods) per Stage 02 §8.5
row 3. `UnlockAsync` requires audit-memo + emits `Financial.PeriodOpened`
with `Reason = $"Unlocked by admin: {auditMemo}"`.

**`ReopenFiscalYearAsync`** posts a reversal of the closing JE via
`JournalPostingService.PostAsync(reversal)`, flips periods Locked →
SoftClosed, and FY.status Closed → Open. Audit-memo required.

#### New event types (catalog additions)

`Financial/PeriodLocked.cs`, `Financial/YearClosed.cs`,
`Financial/YearEndRolloverCompleted.cs` (in this package):

```csharp
namespace Sunfish.Blocks.FinancialPeriods.Financial;

public sealed record PeriodLocked(FiscalPeriodId PeriodId, ChartOfAccountsId ChartId);
// event-type "Financial.PeriodLocked"
// idempotency: $"period-locked:{PeriodId.Value}"

public sealed record YearClosed(
    FiscalYearId FyId, ChartOfAccountsId ChartId, JournalEntryId ClosingEntryId);
// event-type "Financial.YearClosed"
// idempotency: $"year-closed:{FyId.Value}"

public sealed record YearEndRolloverCompleted(
    FiscalYearId FyId, ChartOfAccountsId ChartId, JournalEntryId ClosingEntryId,
    decimal NetIncome, int IncomeAccountsClosed, int ExpenseAccountsClosed);
// event-type "Financial.YearEndRolloverCompleted"
// idempotency: $"year-end-rollover:{FyId.Value}"
// NEW EVENT — add to event-bus catalog §3.1 in PR 3 docs edit.
```

#### Event-bus catalog edit (docs-only)

PR 3 adds two rows to `_shared/engineering/cross-cluster-event-bus-design.md`
§3.1 `Financial.*` events table:

| Event | Consumers | Payload | Idempotency-key |
|---|---|---|---|
| `Financial.PeriodOpened` | reports, work | `{ periodId, chartId, reason? }` | `period-opened:{periodId}` |
| `Financial.YearEndRolloverCompleted` | reports | `{ fyId, chartId, closingEntryId, netIncome, incomeAccountsClosed, expenseAccountsClosed }` | `year-end-rollover:{fyId}` |

The existing `Financial.PeriodSoftClosed`, `Financial.PeriodLocked`,
`Financial.YearClosed` rows stay unchanged.

#### `IBalanceComputer` (or equivalent)

The rollover algorithm needs balances as-of `fy.EndDate`. If
`blocks-financial-ledger` PR 4+ ships an `IAccountBalanceService`, use
that. Otherwise, ship a minimal helper in this package:

```csharp
public interface IBalanceComputer
{
    Task<decimal> ComputeAsOfAsync(GLAccountId accountId, DateOnly asOf, CancellationToken ct = default);
}
```

…with a SQLite-direct implementation summing
`SUM(debit) - SUM(credit)` from `journal_lines` joined to `journal_entries`
where `entry_date <= asOf` and `status = 'Posted'`. **Coordination point
with sibling ledger:** if the ledger's PR 4 or a follow-on hand-off already
introduces this surface, do NOT duplicate; reuse. File `cob-question-*`
if uncertain.

#### Tests (PR 3)

`tests/PeriodCloseServiceTests.Lock.cs`:

- `Lock_SoftClosedPeriod_TransitionsToLocked`.
- `Lock_OpenPeriod_Rejects_PeriodMustBeSoftClosedFirst`.
- `Lock_PopulatesLockedAtUtc`.
- `Lock_EmitsPeriodLockedEvent`.

`tests/PeriodCloseServiceTests.Unlock.cs`:

- `Unlock_LockedPeriod_TransitionsToSoftClosed_WithAuditMemo`.
- `Unlock_EmptyAuditMemo_ReturnsAuditMemoRequired`.
- `Unlock_EmitsPeriodOpenedEvent_WithUnlockReason`.

`tests/FiscalYearCloseServiceTests.cs`:

- `CloseFY_AlreadyClosed_ReturnsAlreadyClosed`.
- `CloseFY_RetainedEarningsAccountUnset_ReturnsConfigurationError`.
- `CloseFY_AutoSoftClosesAnyRemainingOpenPeriods`.
- `CloseFY_ZeroActivityYear_FlipsFyAndLocksPeriods_NoClosingEntry`.
- `CloseFY_NetProfit_PostsClosingEntry_CreditingRetainedEarnings`.
- `CloseFY_NetLoss_PostsClosingEntry_DebitingRetainedEarnings`.
- `CloseFY_PostsBalancedClosingEntry` (Σ debits == Σ credits — regression).
- `CloseFY_AfterClose_PeriodsAreAllLocked_AndFyEndPeriodPointsAtClosingEntry`.
- `CloseFY_EmitsYearClosedAndYearEndRolloverCompletedEvents`.
- `CloseFY_FailedClosingEntryPost_LeavesFyUnchanged` (rollback assertion).

`tests/FiscalYearCloseServiceTests.Reopen.cs`:

- `ReopenFY_ClosedFY_PostsReversal_FlipsToOpen`.
- `ReopenFY_OpenFY_ReturnsFiscalYearAlreadyOpen`.
- `ReopenFY_EmptyAuditMemo_ReturnsAuditMemoRequired`.
- `ReopenFY_FlipsPeriodsLockedToSoftClosed`.

Total new tests this PR: **~17**.

#### Verification (PR 3)

- All PR 1+2 tests still pass.
- New PR 3 tests pass.
- Integration: seed a chart (sibling ledger PR 5's `RentalRealEstate`
  template) → designate retained-earnings account → post a handful of
  income + expense JEs in 2026 → `CloseFiscalYearAsync(fy2026)` →
  resulting closing JE has balanced lines summing to net income; FY
  status is Closed; all 12 periods are Locked; trial-balance post-close
  shows zero Income + zero Expense balances.
- Event-bus catalog doc edit lands in the same PR (additive rows).

#### Do NOT in this PR

- Do NOT ship the ERPNext importer hooks. Those are PR 4.
- Do NOT implement Schedule E generation (`generateScheduleE` Stage 02
  §6.6); that lives in `blocks-reports-tax`. The `fy.status == Closed`
  gate that Schedule E checks is now enforceable by virtue of PR 3.
- Do NOT add multi-currency conversion to the closing JE. Stage 02 §3.2
  rule 6 pins v1 to single-currency.

---

### PR 4 — ERPNext importer hooks

**Estimated effort:** ~1.5h
**Scope:** `IErpnextFiscalYearImporter` + `IErpnextFiscalPeriodImporter`
contracts + implementations; idempotent upsert on `externalRef`; period
synthesis (monthly default) when ERPNext export carries FYs but not
periods
**Commit subject:** `feat(blocks-financial-periods): add ERPNext importer entry-points for FiscalYear + FiscalPeriod`
**Depends on:** PR 3 merged
**Branch:** `cob/blocks-financial-periods-importer-hooks`

#### Field shape ER mapping (Stage 02 §10.1)

| ERPNext DocType | Target |
|---|---|
| `Fiscal Year.name` | `FiscalYear.externalRef` (preserved for trace) |
| `Fiscal Year.year_start_date` | `FiscalYear.startDate` |
| `Fiscal Year.year_end_date` | `FiscalYear.endDate` |
| `Fiscal Year.disabled` | (ignored — Sunfish has no disabled-FY concept; closed FY ≠ disabled) |
| `Fiscal Year.is_short_year` | (informational; affects period synthesis — see below) |

Note: **ERPNext does NOT export FiscalPeriod records as a separate
DocType.** ERPNext synthesizes monthly buckets at query time. The Sunfish
importer's responsibility is to **synthesize the period set** at import
time via `FiscalPeriodFactory.BuildMonthlyPeriods(fy)` and persist them.
This matches Stage 02 §10.3's `importPeriods(targetChartId)` line ("//
synthesize from FYs + monthly default").

#### `Migration/IErpnextFiscalYearImporter.cs`

```csharp
namespace Sunfish.Blocks.FinancialPeriods.Migration;

public interface IErpnextFiscalYearImporter
{
    Task<ImportOutcome<FiscalYear>> UpsertFromErpnextAsync(
        ErpnextFiscalYearSource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default);
}

public sealed record ErpnextFiscalYearSource(
    string Name,                  // ERPNext "name" — stable id
    string Modified,              // ERPNext "modified" — version key
    DateOnly YearStartDate,
    DateOnly YearEndDate,
    string? CompanyShortName,     // for label derivation if needed
    bool IsShortYear);

// ImportOutcome<T> + ImportAction are defined in blocks-financial-ledger
// (sibling hand-off PR 6). Reuse via project reference.
```

#### `Migration/IErpnextFiscalPeriodImporter.cs`

```csharp
namespace Sunfish.Blocks.FinancialPeriods.Migration;

public interface IErpnextFiscalPeriodImporter
{
    /// <summary>
    /// Synthesizes FiscalPeriod rows for an imported FiscalYear, since
    /// ERPNext doesn't export periods as a discrete doctype. Idempotent
    /// per-FY: re-running on a FY whose periods already exist returns
    /// Skipped for each existing period.
    /// </summary>
    Task<IReadOnlyList<ImportOutcome<FiscalPeriod>>> SynthesizePeriodsForFiscalYearAsync(
        FiscalYearId fyId,
        FiscalPeriodKind kind = FiscalPeriodKind.Monthly,
        CancellationToken cancellationToken = default);
}
```

#### Implementations

**`Migration/ErpnextFiscalYearImporter.cs`** — per-record flow:

1. Look up existing `FiscalYear` by `_years.GetByExternalRefAsync(source.Name)`.
2. If exists and version unchanged → return `Skipped`.
3. If exists and version moved forward → update fields (label,
   start/end dates if they shifted), return `Updated`. **Do NOT change
   status — if it's Closed in Sunfish, an ERPNext re-export shouldn't
   reopen it.**
4. If new → label-derive from `Company` short-name + year (e.g., "FY26"
   when start date is 2026-01-01 + company is "Acero Properties"); call
   `FiscalYear.CreateOpen(...)`; set `externalRef = source.Name`;
   persist; return `Inserted`.

**`Migration/ErpnextFiscalPeriodImporter.cs`** — per-FY flow:

1. Look up FY via `_years.GetAsync(fyId)`.
2. Look up existing periods via `_periods.GetByFiscalYearAsync(fyId)`.
3. If existing period count > 0 → return `Skipped` for every synthesized
   shape (idempotency).
4. If empty → build the synthesized set via
   `FiscalPeriodFactory.BuildMonthlyPeriods(fy)` (or `Quarterly` /
   `Annual` per `kind` arg); validate via
   `FiscalPeriodCollectionValidator`; persist each (atomic transaction);
   return `Inserted` for each.

**Idempotency contract:**

- `externalRef` carries the ERPNext-side `name` (Stage 02 §10.4 pattern).
- A second pass with the same source returns `Skipped`.
- A third pass with version-bumped source returns `Updated` (FY-side
  only; periods don't carry an ERPNext-side version because they're
  synthesized).

#### DI registration

Extend `AddBlocksFinancialPeriods()`:

```csharp
public static IServiceCollection AddBlocksFinancialPeriods(
    this IServiceCollection services)
{
    // PR 1 baseline (no services).

    // PR 2 additions:
    services.AddSingleton<IFiscalYearRepository, SqliteFiscalYearRepository>();
    services.AddSingleton<IFiscalPeriodRepository, SqliteFiscalPeriodRepository>();
    services.AddSingleton<IPeriodResolver, SqlitePeriodResolver>();
    services.AddSingleton<IPeriodCloseService, PeriodCloseService>();
    services.AddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>();
    // (Real IDomainEventPublisher replaces NoopDomainEventPublisher
    //  once the foundation-events / kernel-events home is ratified
    //  per Q1 of cross-cluster-event-bus-design §10.)

    // PR 3 addition:
    services.AddSingleton<IFiscalYearCloseService, FiscalYearCloseService>();

    // PR 4 additions:
    services.AddSingleton<IErpnextFiscalYearImporter, ErpnextFiscalYearImporter>();
    services.AddSingleton<IErpnextFiscalPeriodImporter, ErpnextFiscalPeriodImporter>();

    return services;
}
```

#### Tests (PR 4)

`tests/ErpnextFiscalYearImporterTests.cs`:

- `Upsert_NewSource_InsertsFiscalYear`.
- `Upsert_SameVersion_ReturnsSkipped`.
- `Upsert_HigherVersion_ReturnsUpdated`.
- `Upsert_LowerVersion_ReturnsSkipped`.
- `Upsert_ClosedFy_HigherVersion_DoesNotReopen` (regression — explicit).
- `Upsert_PreservesExternalRef`.
- `Upsert_DerivesLabel_FromCompanyAndStartDate`.

`tests/ErpnextFiscalPeriodImporterTests.cs`:

- `SynthesizePeriods_EmptyFy_InsertsTwelveMonthlyPeriods`.
- `SynthesizePeriods_PeriodsAlreadyExist_ReturnsSkippedForAll`.
- `SynthesizePeriods_QuarterlyKind_InsertsFour`.
- `SynthesizePeriods_AnnualKind_InsertsOne`.
- `SynthesizePeriods_ProducedSetPassesCollectionValidator`.
- `SynthesizePeriods_ShortYear_LastPeriodTruncatedToFyEnd` (regression —
  what happens when the FY ends mid-month).

Total new tests this PR: **~13**.

#### Verification (PR 4)

- All PR 1+2+3 tests still pass.
- New PR 4 tests pass.
- Integration: simulate ERPNext's `Fiscal Year` export shape (4 LLCs from
  the project's existing Mac-ERPNext data — at minimum FY2024 + FY2025
  + FY2026 per LLC × 4 LLCs = 12 FYs) → importer ingests all → period
  synthesis produces 12 monthly periods per FY × 12 FYs = 144 periods,
  all passing the collection validator.
- Re-running the importer on the same export produces zero `Inserted`
  outcomes; all `Skipped`.

#### Do NOT in this PR

- Do NOT close any FY via the importer. Importer always lands FYs as
  Open (sibling ledger's importer lands JEs as Posted; if the ERPNext
  source has period-close metadata, surface as a separate "import then
  close" step, not in the FY import).
- Do NOT post the closing JE during import. Year-close requires the
  `FiscalYearCloseService` path with a designated retained-earnings
  account; the import has no business doing that.
- Do NOT introduce a new top-level package; this package's
  `Migration/` folder is the home (sibling ledger uses the same
  convention).

---

## CRDT-friendly schema conventions applied

This hand-off applies the cluster's CRDT-friendly conventions per
`_shared/engineering/crdt-friendly-schema-conventions.md`. The relevant
patterns for this hand-off:

### 1. ULID identifiers (§1)

`FiscalYearId` + (reused) `FiscalPeriodId` are ULIDs per §1 of the
conventions doc. The `Id<T>` brand pattern is upheld via C#
strongly-typed `readonly record struct` wrappers.

### 2. Stable string codes for enums (§5)

`FiscalYearStatus`, `FiscalPeriodStatus`, `FiscalPeriodKind` are typed as
C# enums but serialize as their string member names (per the project's
existing JSON serializer convention; sibling ledger PR 3 establishes the
pattern). The wire format uses stable names — `"Open"`, `"SoftClosed"`,
`"Locked"`, etc. Renames forbidden; deprecation requires additive new
member + transition window per §5.

### 3. State-machine handling — Pattern A (designated authority) (§7)

Per §7 of the conventions doc and Stage 02 §3.16, the **period close
operation is performed by one designated replica** (the manager-app /
FinancialAdmin replica). The Stage 02 design and the conventions doc
align on this:

- The conventions doc's §7 table includes:
  `blocks-financial-* | JournalEntry (Draft → Posted) | Posted-then-
  immutable (§6); no merge needed because Posted is append-only`.
- The same `Pattern A` rule is applied to `FiscalPeriod.status`: only
  the designated authority replica performs the transition; other
  replicas observe via Loro CRDT propagation but never advance the state
  themselves.

**Implementation discipline (PR 2 + PR 3):**

- `PeriodCloseService` + `FiscalYearCloseService` assume they run on the
  authority replica. The DI registration target for these services is
  the manager-app / FinancialAdmin replica only; observer replicas do
  NOT register them. This is **enforced by service-resolution semantics**
  — calling `IPeriodCloseService` on an observer replica throws
  `ServiceNotRegistered`.
- The UI on an observer replica shows period status as **read-only**
  and surfaces "period close authority is on `<manager-replica-name>`;
  pending propagation" when a remote close has been initiated but not
  yet observed via CRDT replication.

This pattern matches the sibling ledger's `JournalEntry` posted-then-
immutable shape: once an authority replica writes a state transition,
all other replicas converge on it via CRDT propagation, not via local
state advancement.

### 4. Posted-then-immutable for Locked periods (§6)

Per §6 of the conventions doc, `FiscalPeriod` rows are **posted-then-
immutable once `Locked`**: no field mutation on Locked periods. Unlock
operations:

1. Post an **audit-event** record (an append-only log entry per §4 of
   the conventions doc) carrying the audit memo + previous-locked-at
   timestamp + reopener-principal-id.
2. Insert a **new** `FiscalPeriod` row with the same `Id` and the
   transitioned status. Under Loro CRDT semantics this is an append
   ("the latest write wins" applied to a single ULID key, but the
   append-only audit log preserves the full history).

The C# implementation simplifies for SQLite: a Locked → SoftClosed
transition does perform a row update **on the authority replica only**,
guarded by an append-only audit-event row in the `domain_events` table
that records the unlock action. The Loro layer treats the audit-event
table as authoritative for the history; the `FiscalPeriod` row is the
materialized "current view" — CRDT-friendly because the materialized
view is rebuildable from the audit log.

### 5. Append-only event log (§4 + event-bus design §1)

Per §4 of the conventions doc and §1 of the cross-cluster event-bus
design, all events emitted (`Financial.PeriodOpened`,
`Financial.PeriodSoftClosed`, `Financial.PeriodLocked`,
`Financial.YearClosed`, `Financial.YearEndRolloverCompleted`) are
appended to `domain_events`. Existing rows never `UPDATE`. Idempotency-
key column prevents duplicate emission on replay.

### 6. Tombstone discipline (§2) — N/A in v1

`FiscalYear` + `FiscalPeriod` don't support delete in v1 (no
`deletedAt` field in either Stage 02 §3.15 or §3.16). Tombstones aren't
applicable. If a FY needs to be retired, the path is `closeFiscalYear`
(PR 3), not deletion.

### 7. Cross-replica idempotency (§4 of event-bus design)

Per §4 of the event-bus design, idempotency keys are derived from event
semantics:

- `period-opened:{periodId}` (PR 2 — keyed by period; a reopen of the
  same period within the same calendar second has the same key + would
  be deduped; tolerable because reopens are rare and the recorded-at
  delta is enough to disambiguate ordering when needed).
- `period-soft-closed:{periodId}` (canonical catalog §3.1).
- `period-locked:{periodId}` (canonical catalog §3.1).
- `year-closed:{fyId}` (canonical catalog §3.1).
- `year-end-rollover:{fyId}` (new in PR 3).

These match the canonical catalog (where present) + introduce two
additive types per §2's "no rename" rule.

---

## License posture

### Borrowed-with-attribution (permissive)

**Apache OFBiz** (`accounting/CustomTimePeriod` entity, Apache 2.0,
v18.12.x as of 2026-05-16) — the period-status state machine
(`Open → Closed → Locked`-style) and the FY-then-period containment
shape derive from OFBiz's `CustomTimePeriod` pattern per
`blocks-financial-schema-design.md` §11.1. The Sunfish design renames
the states (`Open`/`SoftClosed`/`Locked` vs OFBiz's `IS_CLOSED`-boolean
+ derived states) and uses ULID ids; the structural pattern is the
reproduction unit.

**Attribution requirements:**

1. `packages/blocks-financial-periods/Sunfish.Blocks.FinancialPeriods.csproj`
   carries a `<PropertyGroup>` `<NOTICEFile>NOTICE.md</NOTICEFile>` reference.
2. `packages/blocks-financial-periods/NOTICE.md` (new file in PR 1):

```markdown
# NOTICE — Sunfish.Blocks.FinancialPeriods

This package's period-status state machine and FY → period containment
shape derive from Apache OFBiz's `accounting/CustomTimePeriod` entity
(<https://ofbiz.apache.org/>, Apache 2.0 license).

OFBiz version studied: v18.12.x (as of 2026-05-16).

The Sunfish implementation is original code, distributed under the
MIT License. The OFBiz entity-shape pattern is reproduced with
attribution per Apache 2.0 §4(c) of the OFBiz License.
```

3. Source-header comments on `FiscalYear.cs`, `FiscalPeriod.cs`, and
   `Services/FiscalYearCloseService.cs` reference OFBiz in a one-line
   comment.

### Clean-room only (copyleft)

Per `blocks-financial-schema-design.md` §11.2 + ADR 0088 §3 (clean-room
discipline), the following sources were studied for understanding only
and contribute NO code to this hand-off:

- **Beancount + ledger-cli** (GPLv2) — Beancount's period-semantics
  model (the `pad` and `balance` directives plus the implicit period
  boundary on the `option "operating_currency"` declaration) is the
  textbook fundamentals reference for clean-room period definition.
  The year-end retained-earnings rollover pattern in Stage 02 §6.5(b)
  is a textbook double-entry pattern (older than the project; not
  derivative of Beancount's specific implementation).
- **GnuCash** (GPLv2) — GnuCash's `Close Books` wizard was reviewed
  for UX cues (e.g., "automatically book any non-zero balance to
  retained earnings"); contributes no code, only the UX
  characterization captured in Stage 02 §6.5(b).
- **ERPNext + Frappe** (GPLv3) — ERPNext's `Period Closing Voucher`
  DocType was reviewed for data-shape parity (`closing_account_head`,
  `transaction_date`, etc.); contributes the field-mapping table in
  Stage 02 §10.1 only; no code.

**Discipline check before merging any PR in this hand-off:**

1. No copyleft code was opened in any editor session that produced this
   hand-off's PRs.
2. No identifier names from any GPL/AGPL source appear in the new code.
   Spot-check by grep before merge:
   ```bash
   grep -i "period_closing_voucher\|closing_voucher\|close_books\|close-books\|periodclosingaccount" \
       packages/blocks-financial-periods/
   ```
   Expected: zero hits.
3. The clean-room schema in Stage 02 §3.15–§3.16 + §6.5 is the source
   of truth for type shapes; deviations from Stage 02 require XO
   ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088
§2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (entities) | ~18 | record construction; per-entity validation; collection validation; factory output |
| PR 2 (soft-close + resolver) | ~17 | soft-close + reopen happy + error paths; date → period resolution; status enum mapping |
| PR 3 (hard-close + year-end rollover) | ~17 | lock + unlock; close-FY with zero / profit / loss; closing JE balance; reopen-FY; event emission |
| PR 4 (importer hooks) | ~13 | FY upsert idempotency; period synthesis; collection-validator round-trip; ERPNext-export shape |
| **Total** | **~65** | (target range ~25–30 per prompt; actual coverage runs higher because the close algorithm has many branches that warrant explicit tests; minimum acceptable is the ~25 most-load-bearing — listed below as the cluster-level acceptance gate) |

The prompt-suggested ~25–30 floor is the **cluster-level minimum**;
individual tests above that floor are recommended for safety but may be
trimmed if compile-time pressure surfaces. The acceptance-gate tests
(A1–A8 below) are the irreducible set.

### Cluster-level acceptance (PASS gate at end of PR 4)

**A1.** `dotnet build` succeeds on `Sunfish.Blocks.FinancialPeriods` and
every downstream consumer (sibling ledger, kitchen-sink demo if wired,
apps/docs site).

**A2.** `dotnet test packages/blocks-financial-periods/tests/` passes
all PR-1 → PR-4 tests (~65 tests).

**A3.** A monthly period set synthesized from a calendar-year FY (e.g.,
2026-01-01 → 2026-12-31) contains:
- 1 `FiscalYear` record (Open).
- 12 `FiscalPeriod` records, contiguous, non-overlapping, all Open.
- The validator round-trips clean.

**A4.** A `softClosePeriod` action on an Open period:
- Transitions the period to `SoftClosed` + populates `SoftClosedAtUtc`.
- Emits `Financial.PeriodSoftClosed` to `domain_events` with the
  canonical idempotency key.
- A subsequent admin post via `JournalPostingService` succeeds.
- A subsequent non-admin post via `JournalPostingService` returns
  `PostError.PeriodSoftClosed`.

**A5.** A `closeFiscalYear` action on an Open FY with net income > 0:
- Auto-soft-closes any remaining Open periods (idempotent — no error if
  all were already SoftClosed).
- Posts a balanced closing `JournalEntry` (Σ debits == Σ credits).
- Credits Retained Earnings with the net-income amount.
- Locks all 12 periods.
- Flips FY status to Closed; populates `ClosedAtUtc` +
  `ClosingJournalEntryId`.
- Emits `Financial.PeriodLocked` × 12 + `Financial.YearClosed` × 1 +
  `Financial.YearEndRolloverCompleted` × 1.
- Trial balance post-close shows zero Income + zero Expense balances.

**A6.** `closeFiscalYear` failure rollback: if `JournalPostingService.
PostAsync` returns a non-`None` error for the closing entry, the FY
status stays Open + no periods are locked + no events are emitted (full
rollback via the wrapping transaction).

**A7.** `ReopenFiscalYearAsync` on a Closed FY with an audit memo:
- Posts a reversal of the closing JE.
- Flips all periods Locked → SoftClosed.
- Flips FY status Closed → Open; clears `ClosedAtUtc` + nullifies
  `ClosingJournalEntryId`.

**A8.** ERPNext-import round-trip: a synthetic ERPNext-shape export with
2 `Fiscal Year` records imports cleanly via PR 4's importer hooks:
- 2 `FiscalYear` rows inserted with `externalRef` populated.
- 24 `FiscalPeriod` rows synthesized (12 per FY).
- Re-running the import returns `Skipped` for both FYs + every period.

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*`
beacon to `/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`:

### 1. Sibling ledger PR not yet merged (any PR)

If COB starts PR 1 before sibling `-ledger` PR 3 lands (which
introduces `FiscalPeriodId`) or before sibling `-ledger` PR 4 lands
(which introduces `IPeriodResolver`), the build will break.

**Mitigation:** pre-build checklist item 2 catches this. If the sibling
PRs haven't merged within ~30 min of starting this hand-off, file
`cob-question-2026-05-XXTHH-MMZ-w60-p4-periods-blocked-on-ledger.md`.
Recommended fallback: pause + work fallback rung 1 (Dependabot cleanup)
until sibling lands.

### 2. `IDomainEventPublisher` home not ratified (PR 2)

If `foundation-events` / `kernel-events` / equivalent package doesn't
exist or its surface isn't ratified by the time PR 2 ships, COB must
decide:

(a) ship a local `IDomainEventPublisher` + `NoopDomainEventPublisher`
    in this package + file `cob-question-*` to flag the temporary home;
    OR
(b) wait for the foundation home to land.

**XO recommendation:** ship (a) with a TODO comment + a beacon. The
Noop publisher means no events actually fire in v1; the test suite
should verify the publisher was *called* (mock-based assertion), not
that events landed in any persistence layer. When the real publisher
home is ratified, a follow-on PR re-registers the interface to the
real implementation; the calling code doesn't change.

Beacon file: `cob-question-2026-05-XXTHH-MMZ-w60-p4-periods-event-publisher-home.md`.

### 3. `IBalanceComputer` already exists in `blocks-financial-ledger` (PR 3)

PR 3 needs balance-as-of-date computation for the closing-JE algorithm.
If a sibling hand-off (or `blocks-financial-ledger` follow-on PR) has
already shipped `IAccountBalanceService` or similar, USE it — don't
duplicate.

```bash
grep -r "IBalanceComputer\|IAccountBalanceService\|ComputeAsOf" \
    /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-*/
```

If multiple candidates exist, file `cob-question-2026-05-XXTHH-MMZ-w60-p4-periods-balance-computer-choice.md`.

### 4. Retained-earnings account not set on imported charts (PR 3 acceptance test)

The acceptance test A5 requires the chart to have a designated
`RetainedEarningsAccountId`. If the sibling ledger's seed templates
don't set this (review the `RentalRealEstate` template wiring in sibling
ledger PR 5 — account `3900 Retained Earnings` is in the template, but
the `ChartOfAccounts.RetainedEarningsAccountId` pointer needs to be
populated by `IChartSeedingService` at seed time), file
`cob-question-2026-05-XXTHH-MMZ-w60-p4-periods-retained-earnings-wiring.md`.

**XO recommendation:** the sibling ledger's `InMemoryChartSeedingService`
(PR 5) should set `RetainedEarningsAccountId = <seeded 3900 account>`
on the new `ChartOfAccounts` record before returning. If it doesn't
(and the seeded chart leaves the pointer null), this hand-off's PR 3
**still ships**, but the acceptance test A5 has a setup-step prelude
that populates the pointer explicitly. Either path is acceptable.

### 5. Event-bus catalog doc owned by another session (PR 3)

PR 3 edits `_shared/engineering/cross-cluster-event-bus-design.md` §3.1
to add two new event rows. If that file is being concurrently edited
by another XO subagent or session, file
`cob-question-2026-05-XXTHH-MMZ-w60-p4-periods-event-bus-catalog-merge.md`
to coordinate the edit.

**XO recommendation:** rebase the PR after any other §3.1 catalog edit
lands first; the additions are purely additive and rebase cleanly.

### 6. Loro CRDT integration questions (any PR)

If COB hits a question about how `FiscalPeriod` state transitions
interact with Loro CRDT (e.g., "two replicas race to soft-close the
same period — what does the merge resolver do?"), this is the
`Pattern A — Designated authority` discipline being tested at code-
write time.

**Mitigation:** this hand-off enforces single-authority at the **DI
layer** — `IPeriodCloseService` is registered only on the manager-app /
FinancialAdmin replica. Observer replicas don't register the service +
will throw at injection-resolution if a UI on the observer attempts
to call it. The Loro merge resolver therefore never sees a race: there
is at most one writer per `FiscalPeriod.status` field.

If this design constraint surfaces a concrete blocker (e.g., the CO's
deployment doesn't have a clear "manager replica" designation in v1),
file `cob-question-2026-05-XXTHH-MMZ-w60-p4-periods-authority-replica-designation.md`.

### 7. `apps/docs` infrastructure absent

If `apps/docs/blocks-financial-periods/` doesn't fit the project's
docs-site convention (e.g., the site uses a different directory pattern),
file `cob-question-*`. XO recommendation: follow the sibling
`apps/docs/blocks-financial-ledger/overview.md` pattern verbatim.

### 8. Schedule E generation depends on this PR landing (PR 3 follow-on)

A `blocks-reports-tax` hand-off (separate; not in scope here) will
implement `generateScheduleE` per Stage 02 §6.6. The Schedule E algorithm
requires `fy.status == "Closed"` as a hard pre-condition. If a council
reviewer or another session pushes for "let me run Schedule E on an
Open FY", respond by referencing Stage 02 §6.6 line 1608
(`require fy.status == "Closed"`). If the prerequisite is being
soft-warned through, that's a Stage 02 design change requiring XO
ratification — file `cob-question-*`.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–4 merged to main** (sequentially per dependency).
2. **Entities exist:** `FiscalYear` + `FiscalPeriod` are constructible
   via the factory; the collection validator round-trips a synthesized
   monthly set cleanly.
3. **Soft-close works end-to-end:** `softClosePeriod` blocks non-admin
   posting via the sibling ledger's `JournalPostingService` integration
   (acceptance test A4).
4. **Year-end close works end-to-end:** `closeFiscalYear` posts a
   balanced closing entry; periods lock; FY flips; events emit
   (acceptance test A5).
5. **Rollback is correct:** a failed closing-entry post leaves the FY
   unchanged + no events emit (acceptance test A6).
6. **Reopen flow works:** `reopenFiscalYear` reverses cleanly with audit
   memo (acceptance test A7).
7. **ERPNext importer ingests cleanly:** synthetic 2-FY export becomes
   2 FY + 24 period rows; re-import is idempotent (acceptance test A8).
8. **Event-bus catalog updated:** `_shared/engineering/cross-cluster-
   event-bus-design.md` §3.1 carries the two new rows
   (`Financial.PeriodOpened`, `Financial.YearEndRolloverCompleted`).
9. **Tests pass:** ~65 tests across the package.
10. **`apps/docs/blocks-financial-periods/overview.md` published.**
11. **Sibling ledger ledger's placeholder `FiscalPeriodStatus` enum is
    deleted** + `IPeriodResolver.Status` (the nested enum) is the
    canonical wire type between the two packages.
12. **`active-workstreams.md`** row for W#60 P4 / Path II cluster
    implementation updated with `built` status + the 4 PR numbers.

When the PASS gate is met, the next hand-offs in the Phase 1 critical
path can proceed:

- `blocks-financial-tax-stage06-handoff.md` (TaxCode / TaxRate /
  TaxJurisdiction; tax-line-mapping consumes account-id only — no
  period dependency, but Schedule E generation in
  `blocks-reports-tax-stage06-handoff.md` requires `FiscalYear.status
  == Closed`, which this hand-off makes possible).
- `blocks-financial-ar-stage06-handoff.md` (Invoice + InvoiceLine).
- `blocks-financial-ap-stage06-handoff.md` (Bill + BillLine).
- `blocks-financial-payments-stage06-handoff.md` (Payment +
  PaymentApplication).
- `blocks-reports-tax-stage06-handoff.md` (TaxFormLineMap + Schedule E
  generator; consumes this hand-off's `closeFiscalYear` as its hard
  pre-condition).
- `tooling-anchor-import-stage06-handoff.md` (the migration importer
  orchestrator; consumes this hand-off's `IErpnextFiscalYearImporter` +
  `IErpnextFiscalPeriodImporter` + the sibling ledger's
  `IErpnextAccountImporter` + `IErpnextJournalEntryImporter`).

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-16):**

- `icm/02_architecture/blocks-financial-schema-design.md` §3.15
  (FiscalYear), §3.16 (FiscalPeriod), §5.4 (period-close relationships),
  §6.5 (period-close + reversal algorithms), §8.5 (state-transition
  table), §9 (SQLite-side indexes), §10 (migration importer notes),
  §11.1 (OFBiz attribution) ✓
- `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md` §1
  (7-cluster decomposition), §2 (license posture), §3 (clean-room
  discipline) ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` §1 (ULIDs),
  §5 (stable string codes), §6 (posted-then-immutable), §7 (state
  machines under CRDT — Pattern A) ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` §1 (envelope),
  §2 (naming), §3.1 (Financial.* catalog rows `PeriodSoftClosed`,
  `PeriodLocked`, `YearClosed`), §4 (idempotency) ✓
- Sibling hand-off:
  `icm/_state/handoffs/blocks-financial-ledger-chart-and-journal-stage06-handoff.md`
  (PRs 1–6; `ChartOfAccountsId`, `FiscalPeriodId`, `IPeriodResolver`,
  `JournalPostingService`, `RentalRealEstate` template, `ImportOutcome<T>`,
  `ImportAction`) ✓

**Introduced by this hand-off** (ship across PRs 1–4):

- New package: `packages/blocks-financial-periods/`
- New types (PR 1): `FiscalYearId`, `FiscalYear`, `FiscalYearStatus`,
  `FiscalPeriod`, `FiscalPeriodKind`, `FiscalPeriodStatus`,
  `FiscalPeriodCollectionValidator`, `FiscalPeriodFactory`
- New service contracts + implementations (PR 2):
  `IPeriodCloseService` + `PeriodCloseService`,
  `IFiscalYearRepository` + `SqliteFiscalYearRepository`,
  `IFiscalPeriodRepository` + `SqliteFiscalPeriodRepository`,
  `SqlitePeriodResolver` (implements `IPeriodResolver` from ledger),
  `IDomainEventPublisher` + `NoopDomainEventPublisher` (local placeholder
  until foundation-events home ratified)
- New service contracts + implementations (PR 3):
  `IFiscalYearCloseService` + `FiscalYearCloseService`,
  extended `IPeriodCloseService` (Lock + Unlock methods),
  `PeriodCloseError` enum extensions,
  `FiscalYearCloseError` + `FiscalYearCloseResult`,
  `IBalanceComputer` + `SqliteBalanceComputer` (local; subject to
  coordination with sibling ledger per Halt 3)
- New event payload types (PR 2+3):
  `Financial.PeriodOpened` (new event — added to event-bus catalog),
  `Financial.PeriodSoftClosed` (canonical catalog),
  `Financial.PeriodLocked` (canonical catalog),
  `Financial.YearClosed` (canonical catalog),
  `Financial.YearEndRolloverCompleted` (new event — added to catalog)
- New migration types (PR 4):
  `IErpnextFiscalYearImporter` + `ErpnextFiscalYearImporter`,
  `IErpnextFiscalPeriodImporter` + `ErpnextFiscalPeriodImporter`,
  `ErpnextFiscalYearSource`
- Sibling-ledger edit (PR 2): delete placeholder `FiscalPeriodStatus`;
  replace with nested `IPeriodResolver.Status`
- Docs: `apps/docs/blocks-financial-periods/overview.md`
- Catalog edit: `_shared/engineering/cross-cluster-event-bus-design.md`
  §3.1 + 2 rows (`Financial.PeriodOpened`,
  `Financial.YearEndRolloverCompleted`)
- Attribution: `packages/blocks-financial-periods/NOTICE.md`

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies
each cited symbol by reading the actual file before declaring AP-21
clean. Do not rely on grep-only verification. In particular, **read**
the sibling ledger's `IPeriodResolver.cs` before PR 2 to confirm the
contract shape; **read** the sibling ledger's `JournalPostingService.cs`
before PR 3 to confirm the `PostResult` shape; **read** Stage 02 §3.15
+ §3.16 + §6.5 verbatim before any entity-shape decisions.

---

## Cohort discipline

This hand-off is the **second Stage 06 hand-off under ADR 0088 Path II**
and the **second Phase 1 cluster implementation unit**. The COB self-
audit pattern applied to the sibling ledger hand-off (and to the
upstream W#34 / W#35 / W#36 / W#39 / W#40 substrate hand-offs) applies
here verbatim:

- **No two-overload constructor pattern is required** for `PeriodClose
  Service` (no audit-state to thread). If `IErpnextFiscalYearImporter`
  ends up coupled to an audit-enabled DI surface, mirror the sibling
  ledger's two-overload pattern (audit-disabled / audit-enabled both-
  or-neither).
- **`AddBlocksFinancialPeriods()` naming** for the DI extension.
- **`apps/docs/{cluster}/overview.md` page convention.**
- **README.md at the package root** referencing Stage 02 design + ADR
  0088 + sibling ledger hand-off.
- **`ConcurrentDictionary` dedup** for any cache (none introduced in
  this hand-off; flagged for future).
- **Read-the-actual-file self-audit** for every cited symbol (per
  ADR 0028-A10).
- **State-machine implementation pattern** as Pattern A — Designated
  authority (per §7 of CRDT conventions); single-writer enforced at the
  DI layer.

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w60-p4-periods-{slug}.md` in
  `/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`.
- Halt the workstream + add a note in `active-workstreams.md` row for W#60.
- `ScheduleWakeup 1800s`.

If COB completes PR 4 + the PASS gate is met:

- Update `active-workstreams.md` (via the source W*.md file, not the
  ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Drop `cob-status-2026-05-XXTHH-MMZ-w60-p4-periods-built.md` to inbox.
- Continue with the next hand-off in the Phase 1 critical path (likely
  `blocks-financial-tax` or `blocks-financial-ar` — whichever XO has
  dropped next).

---

## Cross-references

- **Spec source:** `icm/02_architecture/blocks-financial-schema-design.md`
  §3.15, §3.16, §5.4, §6.5, §8.5, §9, §10, §11.1.
- **ADR 0088:** `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`
  (Path II decision; 7-cluster decomposition; license posture; clean-room
  discipline).
- **Sibling hand-off:**
  `icm/_state/handoffs/blocks-financial-ledger-chart-and-journal-stage06-handoff.md`
  (Phase 1 critical-path PR 1 of 7; this hand-off is PR 2 of 7).
- **Conventions:**
  - `_shared/engineering/crdt-friendly-schema-conventions.md` §1
    (ULIDs), §5 (stable string codes), §6 (posted-then-immutable), §7
    (state machines — Pattern A designated authority).
  - `_shared/engineering/cross-cluster-event-bus-design.md` §1
    (envelope), §2 (naming convention — no rename), §3.1 (Financial.*
    catalog), §4 (idempotency-key derivation).
- **Cohort precedent hand-offs (substrate-only shape):**
  - `foundation-mission-space-stage06-handoff.md` (W#40 — 5-PR shape,
    DI extension pattern)
  - `foundation-versioning-stage06-handoff.md` (W#34 — substrate naming)
  - `foundation-migration-stage06-handoff.md` (W#35 — substrate sequencing)
- **Migration importer sibling spec:**
  `_shared/engineering/erpnext-to-anchor-migration-importer-spec.md`
  (drafted 2026-05-16 by XO; lands as a sibling doc to the ledger
  hand-off; this hand-off implements the FY + Period import hooks the
  orchestrator consumes).
- **Forward-references (consumers of this hand-off):**
  - `blocks-reports-tax-stage06-handoff.md` (TBD) — `generateScheduleE`
    requires `fy.status == "Closed"`; this hand-off makes that
    pre-condition reachable.
  - `blocks-financial-budget-stage06-handoff.md` (Phase 3, TBD) —
    `Budget.fiscalYearId` references this hand-off's `FiscalYear.Id`;
    `BudgetPeriod.fiscalPeriodId` references this hand-off's
    `FiscalPeriod.Id`.

---

**End of hand-off.**
