---
id: 36
title: SyncState Multimodal Encoding Contract
status: Accepted
date: 2026-04-24
tier: foundation
concern:
  - distribution
  - ui
composes:
  - 17
  - 34
extends: []
supersedes: []
superseded_by: null
amendments: []
---
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

---

## Amendments (post-acceptance, 2026-05-01)

### A1 (REQUIRED) — Public `Sunfish.Foundation.UI.SyncState` enum exposure

**Driver:** ADR 0063-A1.2 halt-condition + A1.17 sibling-amendment dependencies. ADR 0063 (Mission Space Requirements; landed post-A1 via PR #411) introduced `SyncStateSpec.AcceptableStates: IReadOnlySet<SyncState>?` as part of the install-time spec schema. The `SyncState` type ADR 0063 cites does not exist as a public foundation-tier enum on `origin/main` — ADR 0036 (this ADR) declares the encoding contract (the canonical identifiers `healthy / stale / offline / conflict / quarantine`) but stops short of exposing a public C# enum that downstream substrate ADRs can consume in type signatures.

A1 closes the gap. Ships a public foundation-tier `Sunfish.Foundation.UI.SyncState` enum + canonical-identifier round-trip helpers. Backward-compat preserved (additive surface; existing string-form consumers continue to work).

**Pipeline variant:** `sunfish-api-change` (introduces new public type; non-breaking).

**Companion intake:** [`icm/00_intake/output/2026-04-30_sync-state-public-enum-intake.md`](../../icm/00_intake/output/2026-04-30_sync-state-public-enum-intake.md) (PR #414).

#### A1.1 — `Sunfish.Foundation.UI.SyncState` enum

```csharp
namespace Sunfish.Foundation.UI;

public enum SyncState
{
    Healthy,    // canonical identifier "healthy"
    Stale,      // canonical identifier "stale"
    Offline,    // canonical identifier "offline"
    Conflict,   // canonical identifier "conflict"
    Quarantine  // canonical identifier "quarantine"
}
```

Enum value names are the PascalCase form of the canonical lowercase identifiers documented in §"Decision" + §"Channel rules" of this ADR. The 5-value set matches exactly — no additions, no removals.

#### A1.2 — Canonical-identifier round-trip

The PascalCase enum and the lowercase-string canonical identifier MUST round-trip cleanly via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` per the project's existing convention (per ADR 0028-A7.8 + ADR 0028-A8.4 precedent).

The canonical encoding rule: serialize as the **lowercase** identifier (`"healthy"`, `"stale"`, etc.) — matching ADR 0036's existing encoding-contract identifiers — NOT the PascalCase enum-name form.

Implementation note: a `JsonStringEnumConverter` configured with `JsonNamingPolicy.CamelCase` produces `"healthy"` for `SyncState.Healthy` (single-word identifiers are flat-cased identically under camelCase). The round-trip property is what matters; the implementation MUST verify round-trip for all 5 enum values.

#### A1.3 — Backward compatibility

Existing consumers consuming sync state via:
- ADR 0036's encoding contract (string-form canonical identifiers in JSON / CSS class names / ARIA roles) — **unchanged**.
- ADR 0036's per-tier UI surface conventions (color, glyph, status-noun text, ARIA `role` + `aria-live`) — **unchanged**.

The enum is **additive** — it provides a typed surface for downstream C# consumers who need it (specifically: ADR 0063's `SyncStateSpec.AcceptableStates`). No string-form consumer is broken.

#### A1.4 — Acceptance criteria

For a `Sunfish.Foundation.UI.SyncState` implementation to be considered A1-conformant, it MUST:

- [ ] Define the 5-value enum exactly (`Healthy`, `Stale`, `Offline`, `Conflict`, `Quarantine`)
- [ ] Round-trip via `CanonicalJson.Serialize` to lowercase canonical identifier strings (5 tests; one per value)
- [ ] Round-trip in dictionary-key contexts via `JsonStringEnumConverter` `ReadAsPropertyName` / `WriteAsPropertyName` (matches W#34 P1 PluginId/AdapterId pattern; needed for downstream substrate consumer use cases)
- [ ] Live in `packages/foundation-localfirst/` (or a new sub-namespace if foundation-localfirst is not the right home — Stage 06 picks)
- [ ] Surface via apps/docs as part of the existing ADR 0036 walkthrough page (if any) OR as a new `apps/docs/foundation/syncstate/overview.md` page
- [ ] No namespace collision with existing `Sunfish.Foundation.UI.*` types (verify before Phase 1 commit)

#### A1.5 — Cited-symbol verification (per cohort discipline)

**Existing on `origin/main`** (verified 2026-05-01):

- ADR 0036 (this ADR) — Accepted; the 5-state encoding contract is canonical
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — encoding contract
- `JsonStringEnumConverter` (System.Text.Json) — Microsoft framework type
- ADR 0028-A7.8 + A8.4 — camelCase canonical encoding precedents (cited; verified Accepted)

**Introduced by A1** (not on `origin/main`; ship in implementation hand-off):

- `Sunfish.Foundation.UI.SyncState` (5-value enum)
- A canonical-identifier-string round-trip test suite

#### A1.6 — Implementation hand-off

Stage 06 hand-off lands as a small workstream (~2–4h; realistically 1 PR). Recommend writing the hand-off file at `icm/_state/handoffs/foundation-ui-syncstate-stage06-handoff.md` when COB capacity opens (currently shipping W#30 Mesh VPN; W#35 Foundation.Migration is queued; W#23 iOS Field-Capture is queued — this amendment's hand-off slots after those).

#### A1.7 — Cohort discipline

Per `feedback_decision_discipline.md` cohort batting average (15-of-15 substrate ADR amendments needing council fixes; structural-citation failure rate 10-of-15 (~67%) XO-authored; §A0 self-audit catch rate 0-of-4 on ADR 0063):

- This is a small mechanical type-exposure amendment. **Pre-merge council MAY be waived** per Decision Discipline Rule 3 (mechanical fixes auto-accept) **if and only if** XO's draft passes a 3-direction spot-check at draft time AND no halt-conditions surface during authoring.
- The amendment cites:
  - 5 enum values matching the canonical identifiers documented elsewhere in this ADR — verified by reading this ADR's §"Decision" + §"Channel rules" (positive-existence + structural-citation)
  - `JsonStringEnumConverter` from System.Text.Json — verified existing as a Microsoft framework type
  - `CanonicalJson.Serialize` — verified existing per multiple prior amendments (ADR 0028-A4 retraction explicitly pinned this surface)
- Post-merge **standing rung-6 spot-check** within 24h per the cohort discipline; if any A1 claim turns out to be incorrect, file an A2 retraction matching the prior cohort retraction pattern.

The decision to waive pre-merge council on this amendment specifically (vs the substrate ADRs in W#33 §7.2) is intentional: this is a **type-exposure amendment** (PascalCase enum exposing existing canonical identifiers), not a substrate ADR (new type + new contract + downstream-consumer-surface). Future amendments to ADR 0036 that introduce new state values OR new encoding rules SHOULD pass through pre-merge council per cohort discipline.
