# Intake — Helm Composition + Identity Atlas Surface

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#34 Wayfinder configuration UX discovery)
**Request:** New ADR ~0066 (numbering speculative) specifying the Helm live-state pane composition and the identity Atlas surface (account profile, key rotation, recovery contacts, historical-keys browse, active-team switcher).
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

The Helm and identity Atlas are two distinct UI surfaces with shared substrate. The Wayfinder discovery (W#34) §5.8 identifies Layer 8 (account/identity) as **Partial coverage** — the cryptographic and audit infrastructure is fully built (ADRs 0046, 0046-a1, 0032, 0049 + W#32 Foundation.Recovery substrate) but no UX specification exists for surfacing identity to the user. The Helm needs widget composition; the identity Atlas needs profile-edit, key-rotation, and recovery-contact UX.

## Predecessor

- **Helm runtime substrate**: ADR 0062 (Mission Space Negotiation Protocol — provides `MissionEnvelopeProvider` + `ICapabilityGate<T>`)
- **Sync state widget**: ADR 0036 (5 sync states with ARIA roles + aria-live policies)
- **Active-team switcher**: ADR 0032 (per-team subkey derivation; multi-team Anchor)
- **Identity / key handling**: ADR 0046 (encrypted field + role-key wrapping); ADR 0046-a1 (historical-keys projection); W#32 substrate built (`EncryptedField` + `IFieldDecryptor`)
- **Audit emission**: ADR 0049 (foundation for identity-change audit events)

## Why net-new

Helm composition is its own surface (live-state pane vs deep-config Atlas); identity Atlas needs explicit UX specification (recovery-contact enrollment, key-rotation flow, historical-keys browse) that no current ADR addresses.

## Scope

### Helm composition
- Identity-glance widget (active team, current role, key fingerprint, recovery-status)
- Sync-state widget (composes ADR 0036's 5-state UI with ARIA + aria-live)
- Mission Envelope summary widget (consumes ADR 0062 — "what your device can do right now")
- Active-team switcher widget (composes ADR 0032)
- Recent Standing Orders widget (last 5 changes affecting this device's Mission Envelope; composes ~ADR 0065)
- Pending Standing Orders widget (issued but not yet propagated; CRDT in flight)
- Quick-toggle pane (go offline, do not disturb, pause sync)
- Quota / CRDT growth gauge (per paper §9)

### Identity Atlas surface
- Profile-edit (name, avatar, contact info) → Standing Order issuance
- Key-rotation flow (compromise response; rotation-window UX; pending-compromise-warning)
- Recovery-contact management (enrollment, removal, verification; spouse-recovery flow per ADR 0046)
- Historical-keys browse (which old keys still verify historical signatures per ADR 0046-a1)
- Per-team identity overview (cross-team correlation policy per ADR 0032)
- Multi-actor approval where appropriate (composes Phase 2 commercial scope's permissions matrix)

## Dependencies and Constraints

- **Hard prerequisite**: ~ADR 0065 (Wayfinder system + Standing Order contract) must land first — Helm and Atlas both consume the Standing Order data model
- **Composes on**: ADRs 0032, 0036, 0046, 0046-a1, 0049, 0062; W#32 substrate
- **Effort estimate:** medium-large (~12–18h authoring + council review)
- **Council review posture:** standard adversarial + **WCAG / a11y subagent**; identity surfaces are sensitive (recovery-contact verification, key-fingerprint display) — accessibility for low-vision and motor-impaired users is load-bearing

## Affected Areas

- foundation-recovery: composed (W#32 substrate)
- ui-core: Helm widget contract; identity Atlas surface contract
- ui-adapters-blazor / ui-adapters-react: per-adapter rendering
- accelerators/anchor: Anchor Helm widget set
- accelerators/bridge: Bridge admin Helm rendering (subset)

## Downstream Consumers

- W#23 iOS Field-Capture (paired-device identity surface)
- Phase 2 commercial MVP (W#5) — multi-actor delegation needs identity Atlas
- W#22 Leasing Pipeline + W#28 Public Listings (identity glance via Helm)

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery **after ~ADR 0065 lands** (Wayfinder system + Standing Order contract).

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §5.8 + §6.2 + §7
- Active workstream: W#34 in `icm/_state/active-workstreams.md`
- Sibling intake: `icm/00_intake/output/2026-05-01_wayfinder-system-and-standing-order-intake.md` (~ADR 0065; hard prerequisite)
