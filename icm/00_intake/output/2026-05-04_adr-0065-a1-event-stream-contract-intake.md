---
type: intake
workstream-or-chapter: adr-0065-a1-event-stream-contract
last-pr: "529"
---

# Intake — ADR 0065 Amendment A1: Standing Order Event-Stream Contract

**Filed:** 2026-05-04
**Filed by:** XO (per ADR 0066 council NM-2 — canonical council finding)
**Pipeline variant:** sunfish-feature-change (additive amendment to existing foundation-wayfinder package)
**Blocking:** ADR 0066 Phase 1 build (halt-condition H8)

---

## Problem

ADR 0065 (Wayfinder System + Standing Order Contract, `docs/adrs/0065-wayfinder-system-and-standing-order-contract.md`) defines Standing Orders and their issuance flow but does **not** define a cross-package event-stream surface for consumers to observe when a Standing Order is applied.

Specifically:
- `packages/foundation-wayfinder/IStandingOrderRepository.cs` ships only three imperative methods: `AppendAsync` / `GetAsync` / `EnumerateAsync`.
- Neither `StandingOrderAppliedEvent` nor any `IObservable<T>` (or equivalent observer) appears anywhere in `packages/foundation-wayfinder/` as of `origin/main` @ `bf31e04`.
- ADR 0065's body also names no reactive surface.

This gap was confirmed by the canonical Opus 4.7 council review of ADR 0066 (NM-2 finding, 2026-05-04; `icm/07_review/output/adr-audits/0066-council-review-2026-05-04.md`).

Consumers that need the reactive surface today:
- **ADR 0066 §1.3 trigger #2** — Helm widgets must recompute on Standing Order applied events (`recent-standing-orders`, `quick-toggles` post-issuance refresh). Without the event stream, these widgets fall back to periodic-refresh + envelope-change only (60-second stale state worst case).
- Future Bridge subscribers who need to react to Standing Order propagation across hosted tenants.

---

## Scope

Define in `Sunfish.Foundation.Wayfinder` (`packages/foundation-wayfinder/`):

1. `StandingOrderAppliedEvent` — a new record type capturing the applied `StandingOrder` + timestamp + originating `ActorId`. Returned by the event stream.
2. `IStandingOrderEventStream` interface (or expose `IObservable<StandingOrderAppliedEvent>` directly on `IStandingOrderRepository` as an additional member) — the cross-package observable surface consumers subscribe to.
3. One new `AuditEventType` constant `StandingOrderApplied` if not already present (verify against `packages/kernel-audit/AuditEventType.cs` — if the generic `StandingOrderIssued` from ADR 0065 covers applied events too, no new constant is needed).

**Out of scope:** Changes to ADR 0065's existing `IStandingOrderIssuer`, `IStandingOrderRepository` (imperative surface), or `StandingOrder` shape. This amendment is additive only.

---

## Effort estimate

- **XO author work:** ~2–3h (small amendment; one new type + one interface extension; §A0 self-audit + structural-citation verification of `AuditEventType` constants).
- **Council review:** standard 4-perspective Opus 4.7 council; pre-merge canonical per ADR 0069.
- **COB build:** ~1–2h (additive to `packages/foundation-wayfinder/`; no existing type changes; parity test for the event interface).

**Total estimated effort:** ~5–7h XO + build.

---

## Prerequisite chain

- This intake is a **hard prerequisite** for: ADR 0066 §1.3 trigger #2 (Helm widget live-state propagation on Standing Order applied).
- ADR 0065 itself must be `Accepted` before this amendment can build (it amends ADR 0065's package).

---

## Routing recommendation

- **Pipeline variant:** `sunfish-feature-change` (additive amendment)
- **ICM fast-track:** intake → architecture (reference ADR 0065 as prior discovery) → implementation-plan → build
- **Skip:** full discovery phase (prior art is ADR 0065 itself); package-design phase (additive to existing package)

---

## Filed per

ADR 0066 council review NM-2 (2026-05-04), `icm/07_review/output/adr-audits/0066-council-review-2026-05-04.md`. See also ADR 0066 §"Implementation checklist Phase 1" halt-condition H8.
