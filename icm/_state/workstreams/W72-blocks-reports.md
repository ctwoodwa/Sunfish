---
sort_order: 80
number: 72
slug: blocks-reports
title: "W#72 — blocks-reports: Read-side report cartridge cluster (Trial Balance, AR Aging, AP Aging, P&L by Property, Rent Roll v2)"
status: "ready-to-build"
status_cell: "`ready-to-build` — all gate deps on main (`blocks-financial-ledger` + `blocks-financial-ar` + `blocks-financial-ap` + `blocks-financial-periods` + `blocks-leases`); hand-off at `icm/_state/handoffs/blocks-reports-stage06-handoff.md`; 7 PRs; ~12-16h; security-engineering + .NET-architect spot-check MANDATORY on PR 1 (`IReportCartridge<,>` substrate contract)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/02_architecture/blocks-reports-schema-design.md` §6 (rendering pipeline + cartridge pattern) + §9 (cross-cluster contracts) + Appendix (Phase 1 MVP scope cut) + `icm/_state/handoffs/blocks-reports-stage06-handoff.md`; ADR 0088 §1 (Phase 1 cluster grouping)"
---

## Notes

**Phase 1 critical-path.** `blocks-reports` is the **read-side reporting pipeline** — a pure consumer that owns no double-entry entities. Ships the `IReportCartridge<TParams, TResult>` substrate + 5 Phase 1 MVP cartridges.

```
blocks-financial-ledger   (ChartOfAccounts + JournalEntry)     ✓ shipped
blocks-financial-periods  (FiscalYear + FiscalPeriod)          ✓ shipped
blocks-financial-ar       (Invoice + IArAgingService)          ✓ shipped
blocks-financial-ap       (Bill + IApAgingService)             ✓ shipped
blocks-leases             (Lease + ILeaseRepository)           ✓ on main
  └──▶ blocks-reports     ← THIS WORKSTREAM (cartridge substrate + 5 cartridges; 7 PRs)
        └──▶ blocks-reports-tax   (Schedule E + TaxFormLineMap — deferred follow-on)
        └──▶ blocks-reports-pdf   (invoice/statement PDF cartridges — deferred follow-on)
```

**What it ships.** 7 PRs (~12–16h):

- PR 1: Cartridge substrate — `IReportCartridge<TParams, TResult>` + `IReportRunner` + `ReportCartridgeRegistry` + `ReportExecutionContext` + `ReportKind` enum + `ISnapshotMarkerSource` stub; **mandatory security-engineering + .NET-architect spot-check before merge**
- PR 2: `TrialBalanceCartridge` — account-level debit/credit/balance aggregation per `FiscalPeriod`; `IsProvisional` flag for Open/SoftClosed periods
- PR 3: `ArAgingSummaryCartridge` — 0/30/60/90+ AR buckets per customer; per-property rollup; calls `IArAgingService` from `blocks-financial-ar`
- PR 4: `ApAgingSummaryCartridge` — same shape, AP side; calls `IApAgingService` from `blocks-financial-ap`
- PR 5: `ProfitAndLossByPropertyCartridge` — per-property P&L dimensional aggregation (income / expense / net per account group)
- PR 6: `RentRollCartridge` — Rent Roll v2 (supersedes W#60 P5 thin slice #847); richer DTO: prepaid balance + delinquency aging + lease-expiry window flag + vacancy reason + portfolio rollup
- PR 7: `AddBlocksReports()` DI extension + `apps/docs/blocks/reports/README.md` + Rent Roll v1→v2 migration note + ledger flip

**Deferred to follow-on hand-offs** (not in this workstream):
- Schedule E + `TaxFormLineMap` → `blocks-reports-tax`
- Invoice/receipt/quote/bill PDF cartridges → `blocks-reports-pdf`
- `ReportRun` / `ReportArtifact` / `ReportSchedule` persistence layer → future pipeline-infrastructure hand-off
- Dashboard / KPI / `WidgetDataSource` entities → future dashboard hand-off

**Attribution.** No external code vendored. GnuCash aging bucket boundaries (0/30/60/90+) studied clean-room — cited as inspiration, not borrowed code. ERPNext rent-roll column set studied clean-room — same. No NOTICE entry required (no borrowed code; design notes only).

**Note on Rent Roll v1.** W#60 P5 shipped a thin Rent Roll v1 DTO in `@sunfish/contracts` (PR #847) + Bridge endpoint (PR #848). v2 here is the canonical .NET implementation. Migration path (v1 → v2 adapter → v1 deprecation → 410 Gone) is documented in PR 7's migration note; v1 is NOT touched by this workstream.

**Council.** PR 1 MANDATORY: security-engineering spot-check (tenant-id binding convention on `ReportExecutionContext`) + .NET-architect spot-check (`IReportCartridge<,>` contract shape). PRs 2–6 inherit the pattern; spot-check only if a reviewer flags a halt. PR 7: no council.
