# Intake — Sunfish Shared Design System

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#35 Ship Architecture discovery §8.2)
**Request:** New ADR specifying the Sunfish Shared Design System — role taxonomy + permission tuple + deck-progressive-disclosure pattern + first-aid baseline + design tokens + component primitives + WCAG 2.2 AA + EN 301 549 conformance baseline. **Load-bearing for every downstream UI ADR.**
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

The W#35 Ship Architecture discovery specifies a two-layer model (locations × responsibilities × deck-depth), permission tuple `(role, location, deck, action)`, first-aid baseline (universal contextual help), and platform a11y API contract (UIA / NSAccessibility / UIAccessibility / AccessibilityNodeInfo / ARIA). These are cross-cutting primitives that *every* downstream UI ADR consumes. Without a single canonical Shared Design System ADR specifying the contract, each per-location follow-on ADR re-derives tokens, role taxonomy, accessibility baseline, and permission denial UX from scratch.

This is the **load-bearing follow-on ADR** of the W#35 set — sequence it first per §9.2.

## Predecessor

**No clean predecessor.** Adjacent: ADR 0041 (dual-namespace components — informs degradation primitive); ADR 0036 (sync states with ARIA roles + aria-live policies — informs live-region primitive); W#34 ~ADR 0065 (Wayfinder + Standing Order — composes); W#34 §5.1 + Appendix A (cross-platform a11y survey — informs).

## Scope

### Role taxonomy
Canonical role registration: Captain / XO / ENG / NAV / TAC / Division Officers (MPA / DCA / Comms / Sonar / Electrical / QA — with rotation pattern) / IDC / Scribe / SUPPO / OOD / EOOW.

### Permission tuple resolution
- `IPermissionResolver` DI surface
- `PermissionDecision { Granted | Denied(reason, remediation) }` shape (denial accessibility contract)
- Resolution algorithm: `(role, location, deck, action)` → grant/deny
- Composition with W#34 ~ADR 0068 security policy

### Deck-progressive-disclosure pattern
- Top deck (executive summary) / Main deck (operational) / Engineering deck (internals) / Below-the-waterline (destructive ops)
- Per-role default landing deck
- Permission gating per deck

### First-aid baseline (universal contextual help layer)
- Every UI surface inherits the WCAG 2.2 AA + EN 301 549 conformance baseline per W#35 §7.4
- Auditable at Stage 07 review

### Design tokens
- Color tokens with contrast guarantees (text/background ≥4.5:1; non-text UI ≥3:1; dark + light themes)
- Typography tokens
- Spacing tokens
- Motion/animation tokens (`prefers-reduced-motion`, `prefers-reduced-transparency`, `prefers-contrast`, `forced-colors`)

### Component library primitives
- `<LiveAnnouncer>` (live-region primitive)
- Form-control primitives (labels, errors, descriptions, `aria-invalid` + `aria-describedby`)
- Focus-trap primitive for modals
- Diff-preview primitive (composes Stripe-pattern from W#34 §B.3)
- Search-as-you-type primitive (combobox per ARIA APG)

### WCAG 2.2 AA + EN 301 549 conformance baseline
**Mandatory accessibility scope** (10 critical topics — *non-negotiable* per W#35 §8.2 Stage 1.5 hardening output):
1. WCAG 2.2 AA criteria (1.3.1, 1.4.1, 1.4.3, 1.4.11, 2.1.1, 2.4.7, 2.4.11, 2.5.7, 2.5.8, 3.2.6, 3.3.1, 3.3.7, 3.3.8, 4.1.2, 4.1.3)
2. Focus management contract
3. Color/theme contrast guarantees
4. Motion/animation tokens (OS prefs honored at token level)
5. Form-control contract
6. Live-region primitive
7. Internationalization + RTL (SC 3.1.1, 3.1.2)
8. Reduced-data / high-contrast modes
9. Authoring-time lint contract (axe / equivalent in CI)
10. EN 301 549 chapters 9–11 mapping (Bridge EU procurement readiness)

### Platform a11y API contract
Per W#35 §7.5: UIA / NSAccessibility / UIAccessibility / AccessibilityNodeInfo / ARIA mapping. Sunfish primitives surface accessible name/role/state through the **native** API on each runtime.

## Industry prior-art

- Apple HIG + Material Design 3 + Microsoft Fluent UI 2 (cross-platform design systems)
- React Aria / Radix UI (a11y-first primitives)
- WCAG 2.2 + EN 301 549 + Apple Accessibility + Microsoft Inclusive Design

## Dependencies and Constraints

- **No hard prerequisites** — this is the foundation the others build on
- **Effort estimate**: large (~20–26h authoring + extended council review)
- **Council review posture**: pre-merge canonical + WCAG/a11y subagent + design-engineering subagent + security-engineering subagent (role taxonomy intersects security) + i18n/RTL subagent if available

## Affected Areas

- foundation: role taxonomy + permission resolver
- ui-core: design tokens + component primitives + accessibility baseline
- ui-adapters-blazor / ui-adapters-react: per-adapter native a11y API binding
- accelerators/anchor + accelerators/bridge: per-accelerator design-system rendering

## Downstream Consumers

**Every downstream UI ADR**, including:
- W#34 ~ADR 0065 / 0066 / 0067 / 0068 / ADR 0009 amendment (Wayfinder follow-ons)
- W#35 ~ADR Quarterdeck / Engine Room / Tactical / Sick Bay / Ship's Office / OOD-Watch (Ship Architecture follow-ons)
- W#22 / W#23 / W#28 / W#29 / W#31 future UI surfaces

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery. **Highest priority of the W#35 follow-ons** per discovery §9.2 — sequence first.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md` §8.2
- Active workstream: W#35 in `icm/_state/active-workstreams.md`
- W#34 a11y precedent: `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §5.1 + Appendix A
