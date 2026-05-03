# Intake — Sick Bay Aggregation UI + IDC Role Definition

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#35 Ship Architecture discovery §5.5 + §6.4 + §8.6)
**Request:** New ADR specifying the Sick Bay aggregation UI (Pharmacy + Lab + Atmosphere monitor) operated by the IDC ("Doc"). Composes on solid substrate (ADR 0046 + 0046-a1 + 0046-A2 + W#32) but the UX surface is missing. **Overlap with W#34 ~ADR 0066 (Helm + identity Atlas) — disambiguate during authoring.**
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

W#35 §5.5 identifies Sick Bay as Partial coverage: substrate solid (ADR 0046 family + W#32 Foundation.Recovery built 2026-04-30), UX missing. The IDC ("Doc") role is uniquely fit for Sunfish's local-first-with-structured-escalation pattern but no current ADR specifies the aggregation UI that surfaces Pharmacy (key vault) + Lab (diagnostics) + Atmosphere monitor (runtime health) as a unified Sick Bay department.

## Predecessor

- **ADR 0046 + 0046-a1** — encrypted field; spouse-recovery; historical-keys projection
- **ADR 0046-A2 + W#32** — `EncryptedField` + `IFieldDecryptor` substrate
- **ADR 0049** — audit trail
- **ADR 0036** — sync states
- **ADR 0061** — managed-relay (Medevac path to Bridge)
- **W#34 ~ADR 0066** — Helm + identity Atlas (overlapping scope)

## Why net-new + overlap note

The substrate is solid; the aggregation UI is the gap. **Overlap with W#34 ~ADR 0066**: Helm + identity Atlas covers identity-glance widgets + recovery-contact UX + key-rotation flow + historical-keys browse. Sick Bay aggregation overlaps significantly but adds: Pharmacy aggregation (cross-record encrypted-field inventory) + Lab diagnostic-probe UI + Atmosphere monitor (runtime health gauge). Authoring must disambiguate — recommend ~ADR 0066 specifies *identity-specific UX* and Sick Bay specifies *cross-record aggregation + diagnostics + medevac flow*.

## Scope

- **IDC Atlas surface** — Pharmacy / Lab / Atmosphere monitor UI
- **Recovery-contact UX** — enrollment, removal, verification (composes ADR 0046 spouse-recovery). Trust decisions never rely on color/icon alone (SC 1.4.1); verification status is text-equivalent.
- **Key-rotation UX** — when does IDC trigger; rotation-window UX; pending-compromise warnings
- **Key-fingerprint display** — monospace + grouped chunks with explicit pronunciation hints (`aria-label="fingerprint, group 1 of 8: A B 1 2"`); never image-only
- **Medevac flow UX** — escalating to Bridge encrypted support channel. Encrypted-channel state changes announced via live region; consent dialogs accessible-name destination + scope
- **Stretcher-bearer cross-training** — DCA + MPA + Comms Officer + Sonar Officer can be paged for first response (formalizes W#35 §7.3)
- **First-aid contextual help** — every user surface inherits IDC-level baseline help (formalizes W#35 §7.4)
- **WCAG 2.2 AA conformance** per Shared Design System

## Industry prior-art

- 1Password / Bitwarden / KeePassXC — credential vault UX
- macOS Keychain Access — key-rotation patterns
- Apple Family Sharing (recovery-contact analog)
- Yubico / FIDO2 (key-fingerprint display patterns)

## Dependencies and Constraints

- **Hard prerequisite**: W#35 ~ADR Shared Design System
- **Hard prerequisite**: W#34 ~ADR 0066 (Helm + identity Atlas) — disambiguate scope at Phase 2
- **Soft cross-reference**: W#34 ~ADR 0068 (security policy — informs IDC authority for key rotation)
- **Effort estimate**: medium-large (~12–18h)
- **Council review posture**: pre-merge canonical + WCAG/a11y subagent (sensitive surfaces — recovery-contact verification, key-fingerprint display)

## Affected Areas

- foundation-recovery: composed (W#32 substrate)
- ui-core: Sick Bay surface contract
- ui-adapters-blazor / ui-adapters-react: per-adapter rendering
- accelerators/anchor: Anchor Sick Bay
- accelerators/bridge: Bridge Medevac receiving end

## Downstream Consumers

- W#23 iOS Field-Capture (paired-device Sick Bay surface)
- Phase 2 commercial MVP — multi-actor identity surface (BDFL spouse-recovery)
- W#22 Leasing Pipeline + W#28 Public Listings — identity glance via Helm

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery after Shared Design System + W#34 ~ADR 0066 land. Disambiguation with ~ADR 0066 happens at this ADR's Phase 1 sparring.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md` §5.5 + §6.4 + §8.6
- W#34 sibling intake: `icm/00_intake/output/2026-05-01_helm-and-identity-atlas-intake.md`
