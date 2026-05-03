# Stage 01 Discovery — Phase 1 Final Report

**Date:** 2026-04-26
**Status:** FINAL — all 8 Open Questions resolved; ready to advance to Stage 05 Implementation Plan
**Predecessor:** `icm/00_intake/output/business-mvp-phase-1-foundation-intake-2026-04-26.md`
**Interim:** `icm/01_discovery/output/business-mvp-phase-1-discovery-interim-2026-04-26.md` (superseded)

## Open Question resolution summary

| # | Question | Status | Outcome |
|---|---|---|---|
| Q1 | `packages/kernel-security` exists? | ✅ Yes | Has Ed25519, X25519, SqlCipher key derivation, root-seed provider, role-key manager, team-subkey derivation. Most Phase 1 identity primitives already exist. |
| Q2 | Anchor MAUI vs Tauri | ✅ ADR 0044 | **Option A — Windows-only Phase 1**. Mac/Linux deferred until MAUI 10/11 stabilizes. Tauri evaluation runs in parallel as a recommendation memo, not a binding choice. |
| Q3 | Sync wire protocol formalization | ✅ Spec exists | `docs/specifications/sync-daemon-protocol.md` (Wave 0.1, 2026-04-22) is the canonical wire format aligned with paper §6 + §11.3. **No new ADR (0045) needed.** Phase 1 sync work is implementation, not design. |
| Q4 | `packages/kernel-schema-registry` exists? | ✅ Yes | Has Compaction, Epochs, Lenses, Migration. Schema-evolution primitive (paper Ch13) implemented. |
| Q5 | Multi-sig social recovery scheme | ✅ ADR 0046 | **Option C — 48a + 48e + 48f + 48c** (multi-sig social + 7-day grace period + signed audit trail + paper-key fallback). 48b (institutional custodian) and 48d (biometric) deferred to post-MVP. |
| Q6 | Bridge tenancy posture | ✅ Pre-existing ADR 0031 | **Bridge is multi-tenant from day one** (Zone C Hybrid, paper §17.2 ciphertext-at-rest invariant). The intake's "single-tenant first" assumption was stale. **No new ADR (0047) needed.** |
| Q7 | Conformance skill invocability | ✅ ICM-aware | Both `local-first-properties` and `inverted-stack-conformance` skills are designed to write findings into `icm/01_discovery/output/` automatically when run from a session with the `the-inverted-stack` repo path available. Defer concrete invocation to Milestone 3's actual scan step. |
| Q8 | Concurrent-session seam | ✅ No coordination needed | Phase 1 work is in `packages/kernel-*` + `accelerators/*`, not UI components. SunfishUserMenu (the example case) lives in `packages/ui-adapters-blazor/Shell/` — Anchor consumes it directly. Phase 2-5 modules may surface real component dependencies; Phase 1 doesn't. |

## Major scope-altering finding

The intake assumed Phase 1 was substantially **greenfield work** (build identity layer, build sync protocol, build schema registry, build Bridge tenancy model). Codebase + spec discovery shows the opposite:

- ✅ Identity primitives already exist (kernel-security: Ed25519, X25519, SqlCipher KDF, root seed, role keys, team subkeys)
- ✅ Sync wire protocol already specified (`docs/specifications/sync-daemon-protocol.md`)
- ✅ Sync transport scaffolded (`kernel-sync/{Discovery,Gossip,Handshake,Identity,Protocol}/`)
- ✅ Schema-evolution primitive already exists (`kernel-schema-registry`)
- ✅ Bridge multi-tier shape already exists (Bridge + AppHost + Client + Data + MockOktaService + deploy)
- ✅ Bridge tenancy posture already settled (ADR 0031: multi-tenant from day one)
- ⚠️ Anchor Windows-only currently (per ADR 0044, accepted)

**Implication:** Phase 1 is **integration + product wiring**, not greenfield primitive implementation. ADR count drops from intake's 4 → 2 (only 0044 + 0046; 0045 + 0047 moot). The 8-week milestone budget likely runs faster than estimated, freeing time for Phase 2 prep.

## ADRs landed this stage

| ADR | Title | File |
|---|---|---|
| 0044 | Anchor ships Windows-only for Business MVP Phase 1 | `docs/adrs/0044-anchor-windows-only-phase-1.md` |
| 0046 | Key-loss recovery scheme for Business MVP Phase 1 | `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md` |

ADRs 0045 + 0047 were proposed in the intake but became moot per Q3 + Q6 findings.

## Pipeline variant landed this stage

**`sunfish-inverted-stack-conformance` pipeline variant created** at `icm/pipelines/sunfish-inverted-stack-conformance/` — closes Phase 1 deliverable #6.

Three files:
- `README.md` — when to use, affected areas, typical deliverables, stage emphasis
- `routing.md` — per-stage navigation with conformance-specific exit criteria
- `deliverables.md` — standard output specs per stage

The intake (Phase 1 foundation) was filed under `sunfish-feature-change` because this variant didn't yet exist. **Future conformance-bearing work should use the new variant.** Re-filing the Phase 1 intake against the new variant is optional — the work is already in flight; a label change adds no value.

## Coordination intakes filed

None for Phase 1. Phase 2-5 module work will likely surface real dependencies on the component / i18n / a11y concurrent sessions; Phase 1 does not.

## Updated milestone plan (post-discovery)

The intake's 3-milestone breakdown survives discovery, but Milestone 1 wraps faster than estimated:

### Milestone 1 (Weeks 1-2 → reality: Week 1) — Discovery + ADRs

- ✅ DONE: 8 Open Questions resolved
- ✅ DONE: ADR 0044 (Anchor Windows-only)
- ✅ DONE: ADR 0046 (key-loss recovery scheme)
- ✅ DONE: `sunfish-inverted-stack-conformance` pipeline variant
- ✅ DONE: Final Stage 01 Discovery report (this document)
- ⏭ ADR 0045 (sync wire protocol) — moot; spec already exists
- ⏭ ADR 0047 (Bridge tenancy) — moot; settled by ADR 0031

### Milestone 2 (Weeks 2-4) — Anchor + Bridge runnable shells

- Anchor MAUI shell launches on Windows; module-plugin host wired (no modules yet — verify with stub plugin)
- Bridge service shell starts; sync-relay endpoint exposed; accepts authenticated connection
- Identity layer wires: Ed25519 device-key → OS secure enclave (Win Credential Manager); per-tenant DEK per book Ch15; SqlCipher integration for Anchor local store
- Smoke test: Anchor launches, prompts for password, opens encrypted local DB, connects to Bridge, exchanges device-key handshake with second Anchor over LAN

### Milestone 3 (Weeks 4-7) — Sync protocol + recovery + first conformance baseline

- Implement sync-daemon-protocol.md spec end-to-end (Anchor↔Anchor LAN via mDNS, Anchor↔Bridge WAN)
- Implement ADR 0046 recovery flow (3-of-5 trustees + 7-day grace + signed audit + paper-key fallback)
- Run `local-first-properties` skill against the Phase 1 deliverable; commit baseline
- Run `inverted-stack-conformance` skill; commit baseline
- Acceptance: "Anchor opens, syncs with another Anchor over LAN, syncs with Bridge over WAN, key recovery flow works end-to-end" — demonstrated via 3 recorded smoke runs

## Next stage

Advance to **Stage 05 Implementation Plan**.

The conformance variant's routing (just authored) says Stages 02 + 03 + 04 are conditional. For Phase 1:
- **Skip Stage 02 (Architecture)** — ADRs 0031, 0044, 0046 already cover the architectural decisions; no new ADR needed for Milestone 2 / Milestone 3 implementation
- **Skip Stage 03 (Package design)** — implementation extends existing public APIs in `kernel-security` / `kernel-sync` / `kernel-schema-registry`; no new public surface
- **Skip Stage 04 (Scaffolding)** — no new generator templates needed

Fast-track path: Stage 05 → Stage 06 → Stage 07 → Stage 08.

## References

- Intake: `icm/00_intake/output/business-mvp-phase-1-foundation-intake-2026-04-26.md`
- Interim discovery: `icm/01_discovery/output/business-mvp-phase-1-discovery-interim-2026-04-26.md`
- Plan: `C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md` §10 Phase 1
- Sync protocol spec: `docs/specifications/sync-daemon-protocol.md`
- Anchor csproj: `accelerators/anchor/Sunfish.Anchor.csproj` (lines 4-9 MAUI 10 preview comment)
- Bridge tenancy ADR: `docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md`
- ADR 0044: `docs/adrs/0044-anchor-windows-only-phase-1.md`
- ADR 0046: `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md`
- New pipeline variant: `icm/pipelines/sunfish-inverted-stack-conformance/`
- kernel-security README: `packages/kernel-security/README.md`
- Foundational paper (do not modify): `C:/Projects/the-inverted-stack/_shared/product/local-node-architecture-paper.md`
- Concept catalog (do not modify): `C:/Projects/the-inverted-stack/docs/reference-implementation/{concept-index.yaml,design-decisions.md}`
