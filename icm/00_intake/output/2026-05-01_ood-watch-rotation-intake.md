# Intake — OOD Pattern + Watch Rotation Primitive

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#35 Ship Architecture discovery §6.7 + §7.2)
**Request:** New ADR specifying the Officer of the Deck (OOD) pattern as a Sunfish primitive — currently-on-watch admin designation distinct from role assignment; watch handover Standing Order; per-Standing-Order OOD-tagging; Engineering Officer of the Watch (EOOW) analog.
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

Sunfish has no concept of *who is currently on watch* — the cross-cutting role assignment that's distinct from Department-Head designation. Real submarines distinguish: any qualified officer can be OOD (or EOOW for engineering) for their shift, regardless of their primary department-head role. For Sunfish, this maps onto **the currently-on-call admin** — when a tenant has multiple admins (Phase 2 spouse-recovery; multi-actor delegation; vendor co-coordinators), only one is OOD at any moment, and Standing Orders issued during their watch should be correlated to them.

Without this primitive, every Standing Order in W#34 ~ADR 0065 + every audit event in ADR 0049 lacks a "who was actually responsible at this moment" correlation distinct from "who has authority by role." The W#35 Ship Architecture discovery (§6.7) confirmed this is a genuine gap and CO promoted it to its own follow-on ADR.

## Predecessor

**No clean predecessor.** Adjacent: ADR 0046 + 0046-a1 (identity primitives); ADR 0032 (per-team subkeys); ADR 0049 (audit substrate); W#34 ~ADR 0065 (Wayfinder + Standing Order); W#34 ~ADR 0068 (security policy — composes with OOD authority).

## Scope

- **OOD designation primitive** — currently-on-watch admin; rotates independently of Department-Head role; bound to Quarterdeck location for the duration of the shift
- **EOOW analog** — currently-on-watch engineer for Engine Room
- **Watch-handover Standing Order shape** — `OOD.WatchTransferred(from: ActorId, to: ActorId, at: Instant, reason: string?)`
- **Per-Standing-Order OOD-tagging** — every Standing Order issued during a watch carries `IssuedDuringWatch: OodId`
- **Audit emission** — every Standing Order's audit event correlates to the OOD; every watch transition emits a high-priority audit event
- **Authority composition** — OOD can approve Standing Orders awaiting multi-actor approval *during their watch*; cross-references W#34 ~ADR 0068 for who can be OOD (minimum factors required)
- **Multi-tenant OOD** — Phase 2 commercial scope: same user can be OOD for tenant A and not for tenant B simultaneously
- **WCAG 2.2 AA conformance** — OOD-watch banner + handover announcement load-bearing for live-region a11y per W#35 §5.1

## Industry prior-art

- Real-Navy OOD / EOOW tradition (cited in CO directive)
- PagerDuty / Opsgenie on-call rotation
- Watchposting in NORAD / FAA ATC

## Dependencies and Constraints

- **Hard prerequisite**: W#34 ~ADR 0065 (Wayfinder + Standing Order) must land first — OOD-tagging composes with the Standing Order data model
- **Soft prerequisite**: W#34 ~ADR 0068 (security policy) — informs *who can be OOD*; can run in parallel
- **Effort estimate**: medium (~10–14h); cross-cutting → council-review-heavy
- **Council review posture**: pre-merge canonical + WCAG/a11y subagent (handover announcements + watch banner are live-region-heavy) + security-engineering subagent (OOD authority intersects security)

## Affected Areas

- foundation (ADR 0049 audit) — OOD-tagging extension to audit events
- Wayfinder (W#34) — Standing Order shape extension
- Quarterdeck (W#35 §5.1) — OOD-watch banner UI
- Engine Room (W#35 §5.3) — EOOW analog UI

## Downstream Consumers

- All Wayfinder Standing Orders (every one is OOD-tagged once this lands)
- W#22 Leasing Pipeline — Phase 6 compliance half (OOD audit correlation for FCRA disputes)
- Phase 2 commercial multi-actor delegation (which spouse / co-owner is on watch?)

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery after W#34 ~ADR 0065 lands.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md` §6.7 + §7.2 + §8.1
- Active workstream: W#35 in `icm/_state/active-workstreams.md`
- W#34 sibling: `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_34_wayfinder_naming.md`
