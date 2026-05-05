---
sort_order: 51
number: 49
slug: ood-watch-rotation
title: "**OOD Watch Rotation** (ADR 0078; W#35 Ship Architecture follow-on; `sunfish-feature-change` pipeline)"
status: "building"
status_cell: "`building` (P1 substrate merged PR #610 2026-05-05; P2 `DefaultOodWatchService`+`OodWatchExpiryService` merged PR #614 2026-05-05 — **4 known gaps (R1–R4); fix BEFORE P3 via addendum**; P3 docs+ledger pending)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`docs/adrs/0078-ood-watch-rotation.md` (PR #571) + `icm/_state/handoffs/ood-watch-rotation-stage06-handoff.md` + `icm/_state/handoffs/ood-watch-rotation-stage06-p2-amendment-addendum.md` (P2 gaps R1–R4)"
---

## Notes

**P1 shipped (PR #610):** `OodWatch` + `OodWatchId` + `OodRole` + `OodWatchState` +
`OodWatchConflictException` + `IOodWatchRepository` + `IOodWatchService` + 3 `AuditEventType`
constants + `StandingOrder.IssuedDuringWatchId`. 8 tests.

**P2 shipped (PR #614):** `DefaultOodWatchService` + `OodWatchExpiryService` + DI registration
in `WayfinderServiceExtensions` + 8 tests. BUT: XO post-merge council review found 4 gaps
that survived COB's own council pass. **Fix these BEFORE P3** per the P2-amendment addendum at
`icm/_state/handoffs/ood-watch-rotation-stage06-p2-amendment-addendum.md`:
- R1: TOCTOU pre-check still in `StartWatchAsync` (3 lines; remove — DB constraint owns invariant)
- R2: No `ILogger` on audit swallow (add `ILogger<DefaultOodWatchService>` + `LogError`)
- R3: No `OodHandoverKind` enum (add Voluntary|CommandRelieved + use in HandoverWatchAsync + payload)
- R4: `GetExpiredCandidatesAsync` on public `IOodWatchRepository` (extract to `internal
  IOodWatchSweepRepository`; `OodWatchExpiryService` takes sweep interface; cross-tenant sweep
  path should NOT be on the general-purpose repo interface)

**P3 pending:** docs + changelog + ledger flip (`built`). Includes R1–R4 fixes from addendum.
P3 estimates ~1.5–2.5h + 1–2 PRs (P2-amendment + P3 proper).
