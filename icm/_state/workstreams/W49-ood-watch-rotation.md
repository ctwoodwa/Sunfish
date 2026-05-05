---
sort_order: 51
number: 49
slug: ood-watch-rotation
title: "**OOD Watch Rotation** (ADR 0078; W#35 Ship Architecture follow-on; `sunfish-feature-change` pipeline)"
status: "built"
status_cell: "`built` (Phases 1-3 complete + P2 amendment 2026-05-06)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`docs/adrs/0078-ood-watch-rotation.md` (PR #571 merged) + `icm/_state/handoffs/ood-watch-rotation-stage06-handoff.md` + `icm/_state/handoffs/ood-watch-rotation-stage06-p2-amendment-addendum.md` + `apps/docs/foundation/wayfinder/ood-watch.md`"
---

## Notes

**Phases 1-3 built + P2 amendment 2026-05-06.** PRs:

- **P1 #610** — substrate types + audit constants + StandingOrder extension; council 2 Major
  findings applied (`IMustHaveTenant` + `[JsonStringEnumConverter]`).
- **P2 #614** — `DefaultOodWatchService` + `OodWatchExpiryService`; council 6 Major findings
  applied (mandatory audit DI throw + narrowed catch + atomic
  `IOodWatchRepository.HandoverWatchAsync` + DI registration + internal `SweepOnceAsync` +
  single `occurredAt`).
- **P2-amendment #619** merged 2026-05-06 — XO post-merge council R1–R4 applied:
  - R1: removed TOCTOU pre-check from `StartWatchAsync` (DB unique index owns the invariant)
  - R2: non-nullable `ILogger<T>` on both services with logging-on-swallow
  - R3: new `OodHandoverKind` enum {`Voluntary`, `CommandRelieved`} discriminator on
    `HandoverWatchAsync` with severity-switching audit payload
  - R4: extracted `internal IOodWatchSweepRepository`; `OodWatchExpiryService` is now
    `internal sealed`; cross-tenant single-caller invariant now type-enforced
- **P3** (this PR) — docs + changelog + ledger flip.

**64/64** wayfinder tests pass.

**H4 + IClock resolution:** signature enforcement deferred to API/gateway layer per XO
directive 2026-05-05; service trusts authenticated `requestedBy`; uses
`TimeProvider.GetUtcNow()`.

**Cohort batting average:** 28-of-31 substrate amendments needed council fixes (P1 → 2 Major;
P2 → 6 Major; P2-amend → 0 — clean READY-TO-MERGE pre-merge council).

Unblocks W#50 (Engine Room Observability) Phase 3b EOOW-check wiring + W#51 (Quarterdeck)
Phase 3a WatchBanner.

**Phase 4 follow-up** (TODO comments): in-memory `IOodWatchRepository` impl (Phase 3 deferred);
`StandingOrder` watch-transfer issuance via `IStandingOrderIssuer` (W#42 P2-gated).
