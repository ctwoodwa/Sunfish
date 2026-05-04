# Intake — Sunfish Wayfinder System + Standing Order Contract (bundled)

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#34 Wayfinder configuration UX discovery)
**Request:** New ADR ~0065 (numbering speculative; next-available at authoring time) bundling the Wayfinder system architecture and the Standing Order event-type contract. The two are interdependent and must land together — defining one requires the other's context.
**Pipeline variant:** `sunfish-feature-change` (introduces new cross-cutting system + event-type contract)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

Sunfish has eight scattered configuration layers but no unified system for capturing, validating, distributing, auditing, and conflict-resolving configuration changes. The Wayfinder discovery (W#34) identifies this as the **most load-bearing** of the follow-on intakes — every other Wayfinder ADR (Helm, Atlas, integration-config, security-policy) consumes the Standing Order contract this ADR defines.

## Predecessor

**No clean predecessor.** Adjacent: ADR 0009 (FeatureManagement — provides the "fifth concept" alongside flags/features/entitlements/editions); ADR 0049 (audit — Standing Orders compose as audit events); ADR 0028 (CRDT — Standing Orders are append-only operations).

## Why bundled

Defining the Standing Order shape requires the Wayfinder system context (who issues, who validates, who consumes). Defining the Wayfinder system requires the Standing Order shape (what events flow through it). Splitting creates a chicken-and-egg sequencing problem.

## Scope

- **Standing Order data model** — `StandingOrder { Id, Scope, Path, OldValue, NewValue, IssuedBy, IssuedAt, Rationale, ApprovalChain, AuditEventId }`
- **Standing Order CRDT semantics** — append-only per-tenant log; conflict resolution between concurrent issuances; composition with ADR 0028's CRDT engine
- **Standing Order validation pipeline** — `IStandingOrderValidator` chain (schema validation → policy validation → authority check → conflict detection)
- **Standing Order audit emission** — every Standing Order is an audit event by construction (composes ADR 0049); 4–6 new `AuditEventType` constants (`StandingOrderIssued`, `StandingOrderAmended`, `StandingOrderRescinded`, `StandingOrderRejected`, `StandingOrderConflictResolved`)
- **Atlas materialized-view contract** — Atlas projects from the ordered Standing Order log; per-page rendering surface; search-as-you-type contract; dual-surface (form ↔ JSON) toggle
- **Wayfinder system DI surface** — `IStandingOrderRepository`, `IStandingOrderIssuer`, `IAtlasProjector`
- **WCAG 2.2 AA conformance specification** — keyboard navigation, screen-reader compatibility, error-prevention (3.3.7 + 3.3.8 + 3.3.9), accessible-alternative for JSON-edit view (Monaco/CodeMirror SR gaps), search-a11y, diff-preview a11y, EN 301 549 procurement compliance for Bridge EU tenants

## Industry prior-art

- VSCode dual-surface settings (`settings.json` + Settings UI sharing one backing store)
- JetBrains schema-driven preferences with deep search
- Stripe Dashboard diff-preview before commit
- Event-sourcing patterns (CQRS, Kafka log compaction)
- WAI-ARIA 1.2 + WCAG 2.2 AA + EN 301 549

## Dependencies and Constraints

- **Hard prerequisite for**: ~ADR 0066 (Helm + identity Atlas), ~ADR 0067 (Atlas integration-config), ~ADR 0068 (security policy)
- **Composes on**: ADR 0028 (CRDT), ADR 0049 (audit), ADR 0009 (FeatureManagement — fifth-concept extension via ADR 0009 amendment)
- **Effort estimate:** large (~16–24h authoring + council review; pre-merge canonical per cohort lesson)
- **Council review posture:** standard adversarial + **WCAG / a11y subagent** (cognitive-function tests forbidden in MFA UX per 3.3.8; non-trivial dual-surface a11y; EN 301 549 procurement-blocker risk)

## Affected Areas

- foundation (new package or extension): Wayfinder + Standing Order types
- ui-core: Atlas surface contract; dual-surface toggle
- ui-adapters-blazor / ui-adapters-react: per-adapter Atlas rendering
- accelerators/anchor + accelerators/bridge: per-zone Wayfinder surface

## Downstream Consumers

- All other Wayfinder follow-ons (~0066, ~0067, ~0068)
- ADR 0009 amendment (5th concept extension)
- Every Sunfish package that has settings (effectively all of them)

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery. Highest priority of the W#34 follow-ons per discovery §6.1.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §5.1 + §6.1 + §7
- Active workstream: W#34 in `icm/_state/active-workstreams.md`
- Wayfinder plan: `~/.claude/plans/sunfish-wayfinder-configuration-research.md`
- Naming memory: `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_34_wayfinder_naming.md`
