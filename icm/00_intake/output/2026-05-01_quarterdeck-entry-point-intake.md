# Intake — Quarterdeck Entry-Point Surface

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#35 Ship Architecture discovery §5.1 + §8.3)
**Request:** New ADR specifying the Quarterdeck — Sunfish's top-level entry point + executive-summary surface + OOD-watch UI + permission-gated descent into other locations.
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

The W#35 Ship Architecture discovery identifies the Quarterdeck as a Gap (§5.1) — no current artifact specifies the entry-point surface where users "report aboard" Sunfish. Without it, every UI surface lacks a coherent landing point; cross-department search, OOD-watch UI, and executive-summary across departments all live in TBD-land.

## Predecessor

**No clean predecessor.** Adjacent: ADR 0036 (5 sync states with ARIA roles + aria-live policies — partial UI substrate); paper §13.2 (AP/CP visibility staleness thresholds); ADR 0062 Mission Space Negotiation Protocol (provides `MissionEnvelopeProvider` for live-state widget); ADR 0049 audit trail (provides recent-events feed); W#34 ~ADR 0065 (Wayfinder + Standing Order — Quarterdeck shows recent Standing Orders); W#35 OOD ADR (Quarterdeck is OOD's primary location).

## Scope

- **Top-deck data model** — what cards / widgets render; sourcing rules per widget; refresh cadence
- **OOD-watch UI** — banner display ("you have the deck"); watch-handover flow; pending-actions inbox; programmatic landmark (`role="banner"`)
- **Permission-gated descent links** — how the Quarterdeck shows/hides departments per role; access denials surface through `PermissionDecision` contract (not blank panes)
- **Alert ticker** — receives feed from Tactical Lookout; motion auto-pauses on `prefers-reduced-motion`; high-priority alerts via `aria-live="assertive"`; ticker pause-control keyboard-reachable
- **KPI cards** — per-department status; text + icon (never color alone per WCAG 1.4.1); accessible name including metric value
- **Deep-link search** — combobox pattern per ARIA APG; arrow-key navigation; live result count announced
- **Recent Standing Orders widget** — last 5 Standing Orders affecting this user's permission tuple
- **Mission Envelope summary** — composes ADR 0062 (Negotiation Protocol)
- **Cross-tenant Quarterdeck** — Phase 2 commercial scope: tenant switcher above the sidebar
- **WCAG 2.2 AA conformance** per Shared Design System ADR baseline

## Industry prior-art

- macOS / Windows 11 / iOS settings entry surfaces (W#34 §A appendix)
- Heroku Dashboard per-app navigation (W#35 §A.3)
- Aspire Dashboard top-level Resources view (W#35 §A.1)
- Stripe Dashboard top-level overview (W#34 §B.3)

## Dependencies and Constraints

- **Hard prerequisite**: W#35 ~ADR Shared Design System (sequence first)
- **Hard prerequisite**: W#35 ~ADR OOD + Watch rotation (Quarterdeck is OOD's location)
- **Effort estimate**: medium-large (~12–18h)
- **Council review posture**: pre-merge canonical + WCAG/a11y subagent (entry-point + OOD-banner + ticker — high-risk surfaces)

## Affected Areas

- ui-core: Quarterdeck surface contract
- ui-adapters-blazor / ui-adapters-react: per-adapter rendering
- accelerators/anchor: Anchor Quarterdeck
- accelerators/bridge: Bridge admin Quarterdeck

## Downstream Consumers

- W#29 Owner Web Cockpit — reframe as Quarterdeck per-tenant projection (deferred to future amendment per W#35 §9.3)
- All other W#35 follow-on ADRs (Engine Room / Tactical / Sick Bay / Ship's Office) — Quarterdeck links to them

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery after Shared Design System + OOD ADRs land.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md` §5.1 + §8.3
- Active workstream: W#35 in `icm/_state/active-workstreams.md`
