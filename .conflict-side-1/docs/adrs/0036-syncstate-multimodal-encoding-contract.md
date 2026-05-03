# ADR 0036 — SyncState Multimodal Encoding Contract

**Status:** Accepted (2026-04-24); palette amended after CVD audit (2026-04-24, iteration 4)
**Date:** 2026-04-24
**Deciders:** Chris Wood (BDFL)
**Related ADRs:** [0034](./0034-a11y-harness-per-adapter.md) (a11y harness per adapter), [0017](./0017-web-components-lit-technical-basis.md) (Web Components / Lit)
**Related spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../superpowers/specs/2026-04-24-global-first-ux-design.md) §5
**Related plans:** [Plan 4B](../superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-3-6-ui-sensory-cascade-plan.md) (cascade across `ui-core`)

---

## Context

Sunfish's sync engine surfaces five distinct states that translate directly into UI: `healthy`,
`stale`, `offline`, `conflict`, `quarantine`. Each state must be:

- **Color-distinguishable under CVD** (deuteranopia, protanopia, tritanopia) — colorblind users
  still need to disambiguate states at a glance.
- **Shape-distinguishable** — even with no color (monochrome printer, Windows High Contrast Mode,
  legacy terminal screen-reader UI).
- **Text-labelled** — short labels for compact contexts (28-character density tier),
  long labels for primary contexts (10-character density tier).
- **ARIA-role-distinguishable** — assistive tech announces "status" (polite) for healthy/stale/
  offline states and "alert" (assertive) for conflict/quarantine, matching the severity gap.

Without a stable contract for this encoding, every component that surfaces sync-state would
re-invent the palette, the icons, and the label conventions — drifting in subtle but
user-visible ways across blocks, dashboards, and toolbar indicators.

The Week-1 pilot (`packages/ui-core/src/components/syncstate/sunfish-syncstate-indicator.ts`)
landed an initial encoding. The CVD ΔE2000 audit in spec §5 P0.3 verified the palette;
Plan 4B Task §5.6 flagged that the contract should live in a durable ADR — not just inline
comments in the pilot component — so the Phase 2 cascade has a stable reference.

---

## Decision

The five SyncStates use this five-channel encoding, **all five channels MUST agree per state**:

| State | Color (light) | Color (dark) | Icon (Material name) | Short label | Long label | ARIA role | aria-live |
|---|---|---|---|---|---|---|---|
| `healthy` | `#117733` | `#44bb55` | `check_circle` | Synced | Synced with all peers | `status` | `polite` |
| `stale` | `#0077bb` | `#3399dd` | `schedule` | Stale | Last synced earlier | `status` | `polite` |
| `offline` | `#888888` | `#bbbbbb` | `cloud_off` | Offline | Offline — saved locally | `status` | `polite` |
| `conflict` | `#ee7733` | `#ff9955` | `call_split` | Conflict | Review required — two versions diverged | `alert` | `assertive` |
| `quarantine` | `#cc3311` | `#ee5533` | `do_not_disturb_on` | Held | Can't sync — open diagnostics | `alert` | `assertive` |

**Palette source:** Paul Tol "vibrant" qualitative scheme adapted for Sunfish — research-vetted
for CVD distinguishability (Tol, "Colour Schemes", 2021). Original ADR 0036 palette failed
the §5 CVD ΔE2000 audit on first run; this is the iteration-4 result. See
[`waves/global-ux/week-2-cvd-palette-audit.md`](../../waves/global-ux/week-2-cvd-palette-audit.md)
for the iteration log + remaining pair exceptions awaiting designer review.

### Channel rules

**Color** — chosen for ΔE2000 ≥ 11 ("distinguishable") between every adjacent-pair under each
of: normal vision, deuteranopia (L-cone deficiency), protanopia (M-cone deficiency),
tritanopia (S-cone deficiency). The palette was iterated until min-pair-ΔE2000 cleared the
threshold under all four vision models. Dark-mode variants preserve the same hue ordering at
~80% saturation to maintain CVD distinguishability against dark surfaces.

**Icon** — Material Symbols family for default rendering. Each icon must be **non-square**
in silhouette so shape distinguishability survives loss of color. The chosen five satisfy
this: a circled checkmark, a clock, a struck cloud, a forking arrow, a circled minus.

**Short label** — ≤ 8 characters; truncates safely in the 10-character compact-density tier
without losing recognizability.

**Long label** — full sentence per state; rendered in the 28-character standard-density tier
and read aloud by assistive tech. End-with-action-noun for conflict/quarantine ("Review
required"; "open diagnostics") gives users immediate guidance.

**ARIA role + aria-live** — the politeness split mirrors severity. Healthy/stale/offline are
informational; conflict/quarantine require user action. Screen readers announce conflict /
quarantine state changes immediately (`aria-live="assertive"`); other states wait for an
idle moment (`aria-live="polite"`).

### Directional mirroring under RTL

Only the `conflict` icon (`call_split`) mirrors under RTL — its forking-arrow geometry has a
left-vs-right semantic that flips. The other four are direction-agnostic. The full
icon-mirror manifest:

```json
{
  "healthy": "non-directional",
  "stale": "non-directional",
  "offline": "non-directional",
  "conflict": "mirrors",
  "quarantine": "non-directional"
}
```

This map must be reflected in each consuming component's `parameters.a11y.sunfish.directionalIcons`
contract block (the per-component a11y harness from ADR 0034 enforces it).

### Custom-state extension policy

Domain-specific states (e.g., `pending-review` in `blocks-businesscases`) MAY extend the
SyncState set IFF they preserve the channel agreement (color + icon + label + role) and
do not re-use one of the five canonical names with a different meaning. Custom states are
named in the source `blocks-*` package's `SharedResource.resx` and published in the package's
README — they are NOT added to the canonical contract; this ADR remains the single source
of truth for the canonical five.

---

## Consequences

### Positive

- One contract, enforced across `ui-core`, `ui-adapters-*`, every `blocks-*`, and every app
  surface — no drift between visualizations of the same state.
- The Phase 2 cascade (Plan 6) ships components that all match this contract; new components
  authored after Phase 1 inherit the contract without negotiation.
- A11y harness (ADR 0034) consumes the contract directly via `parameters.a11y.sunfish` —
  components either match or fail CI.

### Negative / costs

- Adding a sixth canonical state requires an ADR amendment + ΔE2000 audit re-run + every
  consuming component's labels translated for all 12 locales. By design, this is expensive —
  the contract is the brake on uncontrolled state proliferation.
- Custom domain states (e.g., `pending-review`) sit outside the canonical contract; consumers
  see them rendered consistently within a single block but can't compare them across blocks
  the way canonical states can.
- The chosen palette is opinionated. Tenants who want brand-color-aligned syncstate indicators
  must override via the existing CSS custom properties (`--sf-syncstate-*-bg`) — but the
  override MUST clear the same ΔE2000 audit. Tenant-theming guidance lives in the
  `_shared/design/theming.md` doc (added separately).

### Trust impact

None — this is a UI / UX contract, not a protocol contract. No wire-format implications.

---

## Alternatives considered

**Single-channel (color only) encoding.** Rejected: fails for CVD users, fails in monochrome
printing, fails for screen reader users (color is not exposed to AT). Modern accessibility
standards (WCAG SC 1.4.1 "Use of Color") explicitly prohibit color-only signaling.

**Three-state collapse (synced / pending / failed).** Rejected: collides with the actual
distribution of real-world sync states surfaced by Sunfish's CRDT engine. `stale` and
`offline` are different conditions with different recovery paths; collapsing them hides
useful guidance.

**Material 3 expressive palette (HCT color space).** Considered. The HCT palette has stronger
theoretical CVD properties but lands on hues that read as "Material brand" — Sunfish's brand
identity is not Material's. Stuck with empirically-validated CSS named-color-derived hex
values; the ΔE2000 audit is the gate, not the color-system source.

**Wider state namespace (`syncing-active`, `syncing-paused`, `quarantine-storage`,
`quarantine-policy`, ...)** Rejected at this round: too many states to test the CVD palette
against. Custom states extend per-block as documented above; the canonical five satisfy 80%
of cases.

---

## Verification

- Plan 4B Task §5.1 runs the binary CVD ΔE2000 audit against this palette; failure path
  Task §5.1a triggers palette rework + ADR amendment.
- Plan 4 Workstream B's a11y harness consumes this contract via every consuming component's
  `parameters.a11y.sunfish.directionalIcons` block; CI gate (Plan 5) fails the build on
  contract drift.
- Plan 6 Phase 2 cascade audits every existing usage of sync-state UI against this contract
  and remediates non-compliant call sites.

---

## Rollout

- **Phase 1 Week 1:** pilot component already implements this encoding (per Week-1 spec
  closure of P0.1, P0.2, P0.3).
- **Phase 1 Weeks 3–6 (Plan 4B):** cascade across `ui-core` — every component that surfaces
  syncstate adopts this contract. CVD audit gate fires on any palette deviation.
- **Phase 1 Week 6 (Plan 5):** CI gate enforces the contract on every PR.
- **Phase 2 (Plan 6):** application surfaces consume the contract from `ui-core` directly;
  no per-application syncstate authoring permitted.
