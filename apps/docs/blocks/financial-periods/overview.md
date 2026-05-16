# blocks-financial-periods

Period management for the Sunfish Anchor native financial domain.

## Overview

This package is the period-management layer of the `blocks-financial-*`
cluster per [ADR 0088 §1](../../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md).
It provides:

- **`FiscalYear`** — a fiscal-year container (calendar year or shifted).
- **`FiscalPeriod`** — Monthly / Quarterly / Annual / Custom period within
  a fiscal year. Reuses `FiscalPeriodId` from `blocks-financial-ledger` so
  `JournalEntry.PeriodId` can FK-reference periods without a cross-cluster
  type duplication.
- **`FiscalPeriodCollectionValidator`** — Stage 02 §3.16 invariants
  (contiguous, non-overlapping, full FY coverage; Locked-only-if-Closed).
- **`FiscalPeriodFactory`** — `BuildMonthlyPeriods` / `BuildQuarterlyPeriods` /
  `BuildAnnualPeriod` helpers.
- **`IPeriodCloseService`** — soft-close + reopen-soft (PR 2); hard-close
  + year-end retained-earnings rollover land in PR 3.
- **`SqlitePeriodResolver`** — date → period lookup; consumed by the
  ledger's `JournalPostingService` for period-gating (landed in PR 2).
- **`IErpnextFiscalYearImporter`** + **`IErpnextFiscalPeriodImporter`** —
  ERPNext migration entry-points (landed in PR 4). Idempotent on
  `ErpnextFiscalYearSource.Name` (FY) and per-FY (periods are
  synthesized via `FiscalPeriodFactory`).

## Period status state machine

| From → To | Posting allowed | Trigger | Reversibility |
|---|---|---|---|
| `Open → Open` | Yes | n/a | n/a |
| `Open → SoftClosed` | Blocked for non-admin | `softClosePeriod` | Admin reopen |
| `SoftClosed → Locked` | Blocked everyone | Year-end close | Admin unlock + audit |
| `Locked → SoftClosed` | After unlock | Admin unlock + audit memo | Audit-event recorded |

See [Stage 02 §5.4 + §8.5](../../../../icm/02_architecture/blocks-financial-schema-design.md).

## CRDT discipline

Period status transitions follow **Pattern A — Designated authority** per
`_shared/engineering/crdt-friendly-schema-conventions.md` §7: the
period-close action is performed by **one designated replica** (the
manager-app / FinancialAdmin replica). Other replicas observe the status
change propagate via Loro CRDT but never advance the state locally. This
avoids state-machine races at the cost of unavailability when the
designated replica is offline (mitigated by the UI surfacing "period
close pending propagation").

`FiscalPeriod` rows are **posted-then-immutable** once `Locked` per §6 of
the same conventions doc: once locked, the row never mutates; unlocks
generate audit-event records but don't modify the original row in place.

## Quickstart

```csharp
var fy = FiscalYear.CreateOpen(
    id:        FiscalYearId.NewId(),
    chartId:   chart.Id,
    label:     "2026",
    startDate: new DateOnly(2026, 1, 1),
    endDate:   new DateOnly(2026, 12, 31));

var periods = FiscalPeriodFactory.BuildMonthlyPeriods(fy);
// periods = 12 Open FiscalPeriod rows, contiguous, covering 2026.

var validation = FiscalPeriodCollectionValidator.Validate(fy, periods);
// validation.IsValid == true.
```

## Related packages

- `blocks-financial-ledger` — owns `ChartOfAccountsId`, `JournalEntryId`,
  `FiscalPeriodId`, and the `IPeriodResolver` consumer-facing contract
  (`SqlitePeriodResolver` implementation lands here in PR 2)
- `blocks-reports-tax` (Phase 1 follow-on) — `generateScheduleE` requires
  `fy.status == "Closed"`
- `blocks-financial-budget` (Phase 3) — `Budget.fiscalYearId` references
  `FiscalYear.Id`
