# Intake — Mission Space Requirements (install-UX layer)

**Date:** 2026-04-30
**Requestor:** XO research session (synthesis output of W#33 Mission Space Matrix discovery)
**Request:** New ADR ~0062 specifying the install-time minimum-spec UX surface — what a deployment probes at install, how minimum-spec is communicated to users (Steam-style "your device can do X / cannot do Y"), and how runtime upgrades unlock previously-unavailable features.
**Pipeline variant:** `sunfish-feature-change` (introduces new public-facing UX contract; no breaking change to existing surface)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

Sunfish has no canonical install-time minimum-spec specification. Each feature implementer derives hardware/environment gates ad hoc from paper §4 ("16GB RAM, 8-core CPU, 500GB NVMe baseline; 1GB at idle"), ADR 0044 (Windows-only Phase 1), and ADR 0048 (Phase 2 multi-platform target list). This means: (a) no per-feature minimum-spec table; (b) no install-time UX for surfacing capability to users; (c) no runtime probe protocol for re-evaluating after hardware change; (d) no graceful-degradation taxonomy. The Mission Space Matrix (W#33) identifies this as **Partial coverage** in §5.2 (Hardware/environment) with recommendation: "New ADR ~0062 — Mission Space Requirements."

## Predecessor

**No clean predecessor.** Adjacent: paper §4 (hardware baseline), ADR 0044 (Phase 1 platform scope), ADR 0048 (Phase 2 platform scope), paper §13.2 (UX staleness thresholds for AP/CP visibility). None of these specify install-time minimum-spec UX or runtime probe behavior; they are the ground truth that ~ADR 0062 will compose.

## Scope

- **Minimum-spec gradient table** keyed by feature (≥ 8 hardware/environment sub-axes: CPU class, RAM, disk, network, power posture, sensors, trust hardware, display, OS capability, accessibility primitives)
- **Install-time UX specification** — modeled on Steam's pre-install requirements page; pre-install vs post-install behavior; what's dismissable vs blocking
- **Runtime probe protocol** — when to probe (install / startup / continuous); what state to cache; what triggers re-evaluation (hot-plug, version upgrade, network-topology change, jurisdiction crossing on mobile)
- **Graceful-degradation taxonomy** — formalize hide / disable-with-explanation / disable-with-upsell / read-only / hard-fail per feature-gate failure mode (cross-references the Mission Space Negotiation Protocol intake — they pair)

## Dependencies and Constraints

- **Soft dependency** on ~ADR 0063 (Mission Space Negotiation Protocol) — the install-time probe is the surface; the negotiation protocol is the runtime layer. Authorable in either order; sequencing recommendation per discovery §7.2 places ~0062 *after* ~0063 since ~0062 needs to know what the runtime negotiates.
- **No hard blockers.**
- **Effort estimate:** medium-large (~12–18h authoring + council review).
- **Council review posture:** pre-merge canonical per `feedback_decision_discipline.md` cohort lesson (7-of-7 substrate amendments needed council fixes).

## Affected Areas

- foundation: capability-probe surface
- ui-core: install-time UX contract
- ui-adapters-blazor / ui-adapters-react: implementation per adapter
- apps/docs: user-facing minimum-spec documentation
- apps/kitchen-sink: probe demonstration
- accelerators/anchor + accelerators/bridge: install-time probe drives Mission Envelope per zone

## Downstream Consumers

- **W#22 Leasing Pipeline** — consumes commercial-tier surface
- **W#23 iOS Field-Capture App** — consumes hardware + form-factor probe results
- **W#28 Public Listings** — consumes form-factor probe for tier-aware rendering
- **Phase 2 commercial MVP (W#5)** — consumes install-time UX for onboarding flow

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery. Recommended sequencing per Mission Space Matrix §7.2: author ~ADR 0063 (Negotiation Protocol) first; ~ADR 0062 (Requirements) consumes its output.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.2 + §6.1 + §7
- Active workstream: W#33 in `icm/_state/active-workstreams.md`
- Mission Space plan: `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md`
