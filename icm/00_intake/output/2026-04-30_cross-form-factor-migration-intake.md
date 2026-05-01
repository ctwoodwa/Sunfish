# Intake — ADR 0028 Amendment A5: Cross-Device + Cross-Form-Factor Migration Semantics

**Date:** 2026-04-30
**Requestor:** XO research session (synthesis output of W#33 Mission Space Matrix discovery)
**Request:** Amend ADR 0028 (CRDT Engine Selection) with a new amendment A5 specifying the semantics of moving a Sunfish deployment between devices, between form factors (laptop ↔ tablet ↔ watch ↔ IoT), or between hardware tiers — including snapshot portability, encrypted-state key transfer, data-loss-vs-feature-loss invariant, forward-compat behavior, and rollback semantics.
**Pipeline variant:** `sunfish-api-change` (introduces migration-semantics contract; affects cross-form-factor data flow)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

Sunfish has no specification of cross-device or cross-form-factor migration semantics. The Mission Space Matrix (W#33) identifies this as a **Gap** with one peripheral hint — §5.7 Migration: ADR 0028-A1 covers iOS Phase 2.1 capture-only events (lines 144–154) but does not generalize. Paper §15.2 (lines 483–490) covers schema-version mixed-cluster testing scenarios but not feature-surface migration. Paper §13.4 (lines 428–432) covers QR-onboarding but does not cover cross-form-factor data filtering.

Concretely missing: cross-form-factor migration table; cross-hardware-tier re-evaluation rules; data-loss-vs-feature-loss invariant; forward-compat policy; rollback semantics; encrypted-state key transfer formalization.

## Predecessor

**Clean amendment slot:** ADR 0028 — CRDT Engine Selection. Builds on:
- ADR 0028-A1 (iOS Phase 2.1 capture-only events; the one peripheral hint)
- ADR 0028-A6 sibling intake (version-vector compatibility contract; A5 builds on A6's compatibility relation)
- ADR 0046 (encrypted field + IFieldDecryptor; cross-device key transfer composes this)
- Paper §13.4 (QR-onboarding multi-device flow)

**Why amendment, not new ADR:** migration semantics for the CRDT layer are intrinsically tied to the CRDT engine ADR 0028 governs. A5 + A6 together complete the cluster-membership lifecycle (federation handshake + member migration).

## Industry prior-art

Per discovery §5.7:
- **NASA flight-envelope expansion** — test-engineering process of incrementally pushing the boundary outward as hardware capability grows. Direct analog to upgrade-side migration.
- **Database migration patterns** (Liquibase / Flyway / Avro evolution) — schema-version-vector with backward/forward compatibility windows
- **iOS device-to-device data restoration** — Apple's encrypted-iCloud-backup migration pattern; form-factor-aware (restoring iPhone data to iPad applies a derived-surface filter)

## Scope

- **Cross-form-factor migration table** — when a user adds form factor F to their team, what's F's expected feature surface and what data does F sync vs not? (Generalizes ADR 0028-A1's iOS-specific case to laptop / tablet / watch / IoT / headless.)
- **Cross-hardware-tier migration semantics** — when hardware is upgraded or downgraded, rule for re-evaluating each feature gate. Does feature presence persist (cached) until re-probed, or is each gate re-evaluated immediately?
- **Data-loss-vs-feature-loss invariant** — explicit: feature deactivation never causes data loss; data created under a feature that's now unavailable is preserved read-only
- **Forward-compat policy** — when an older deployment receives data from a newer one (newer schema epoch, newer Mission Envelope), what does it show?
- **Rollback semantics** — if a user creates data under capability Z, then downgrades to a hardware tier where Z is unavailable, the data is read-only-but-not-lost
- **Encrypted-state key transfer formalization** — ADR 0046 covers key rotation; cross-device transfer is implicit in QR-onboarding (paper §13.4) but not formalized as a Mission-Space-aware migration

## Dependencies and Constraints

- **Hard dependency**: ADR 0028-A6 (version-vector compatibility contract). A5 requires A6's compatibility relation as input. Authoring sequence: A6 first, A5 second.
- **Cross-references** ~ADR 0063 (Mission Space Negotiation Protocol) — re-evaluation cadence on cross-tier migration is part of negotiation
- **Effort estimate:** medium-large (~10–14h authoring + council review)
- **Council review posture:** pre-merge canonical (cohort lesson 7-of-7); attention to data-loss-vs-feature-loss invariant edge cases

## Affected Areas

- foundation: migration-semantics contract
- foundation-recovery: ADR 0046 EncryptedField cross-device transfer
- accelerators/anchor: per-form-factor expected feature surface
- accelerators/bridge: cross-instance migration behavior

## Downstream Consumers

- **W#23 iOS Field-Capture App** — generalized cross-form-factor migration replaces ad-hoc ADR 0028-A1 carve-out
- **Phase 2 commercial MVP** — multi-device household deployments (BDFL's spouse-recovery use case + dual-device field workflows)
- **Long-offline reconnect** scenarios with hardware change during the offline window

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery. Author **second** of the four follow-on items per discovery §7.2, after ADR 0028-A6.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.7 + §6.4 + §7
- Active workstream: W#33 in `icm/_state/active-workstreams.md`
- ADR 0028 + ADR 0028-A1 (iOS append-only event queue precedent)
- ADR 0046 (encrypted-state key handling)
- Sibling intake: `icm/00_intake/output/2026-04-30_version-vector-compatibility-intake.md` (ADR 0028-A6)
- Mission Space plan: `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md`
