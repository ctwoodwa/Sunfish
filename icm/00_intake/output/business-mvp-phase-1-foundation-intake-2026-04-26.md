# Intake Note — Sunfish Business MVP Phase 1 (Foundation)

**Date:** 2026-04-26
**Requestor:** Christopher Wood (BDFL) — via dedicated business-MVP Claude session
**Spec source:** `C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md` §10 Phase 1
**Pipeline variant (this PR):** `sunfish-feature-change`
**Pipeline variant (target, blocked):** `sunfish-inverted-stack-conformance` — does not yet exist; building it is itself a Phase 1 deliverable per plan §10. This intake is filed under `sunfish-feature-change` until the new variant lands; intake will be re-filed against the new variant once available.

## Problem Statement

The Sunfish repo today implements the Anchor (Zone A local-first desktop) and Bridge (Zone C hybrid multi-tenant) accelerator zones at the **architectural-skeleton** level — kernel + plugin contracts (ADR 0011-equivalent), persistence (foundation-persistence + ADR 0015 module-entity registration), CRDT kernel (kernel-crdt + kernel-sync + kernel-lease, with the recently-fixed Flease coordinator from PR #118 + the D6 wiring via PR #155), feature management (ADR 0009), bundle catalog (ADR 0007), and shells for both accelerators. What it does NOT yet have is a **runnable end-to-end Anchor + Bridge product flow** that an SMB customer could install and use. The `mvp-plan.md` Phase 1 deliverable defines exactly that minimum viable foundation:

> Anchor opens, syncs with another Anchor over LAN, syncs with Bridge over WAN, key recovery flow works end-to-end.

Phase 1 is the prerequisite for Phases 2-5 (the four business modules — accounts, vendors, inventory, projects). Without it, those modules have nowhere to plug in.

Three signals make now the right moment:

1. **Architectural plumbing is in place.** The recent session work (D6 Flease wiring, PR #155; LocUnused analyzer wiring, PR #128; SharedResource cascade for 16 locales; Plan 5 CI close-out) means the kernel + persistence + i18n + CI-gate primitives are stable enough to build product on top of.
2. **The book chapter map is settled.** Plan §15 Appendix A pins each Phase 1 deliverable to specific book chapters (Ch11 node architecture, Ch12 CRDT engine, Ch14 sync daemon protocol, Ch15 security architecture, Ch16 persistence + recovery, Appendix A wire protocol). No ambiguity about source-of-truth.
3. **The conformance skills exist** as planned scaffolding in `the-inverted-stack/.claude/skills/local-first-properties/SKILL.md` and `inverted-stack-conformance/SKILL.md`. Phase 1 establishes the FIRST baseline scan against this Sunfish skeleton — meaningful only once Phase 1 closes the running-product gap.

## Scope Statement (Phase 1 only)

This intake covers the seven Phase 1 deliverables per `mvp-plan.md` §10:

1. **Anchor app shell (.NET MAUI Win/Mac/Linux)** — runnable shell that hosts module plugins; not yet any business modules
2. **Bridge service shell (ASP.NET Core multi-tenant)** — runnable shell that relays sync between Anchor instances; not yet any business modules
3. **User identity** — Ed25519 device keys + per-tenant data keys per book Ch15
4. **Sync protocol** — basic CRDT delta exchange per Appendix A wire format
5. **Backup/restore + multi-sig social recovery** — per primitive #48 key-loss recovery
6. **ICM pipeline variant** — build `sunfish-inverted-stack-conformance` per design-decisions.md §7
7. **First baseline conformance scan** — run both `local-first-properties` and `inverted-stack-conformance` skills against the Phase 1 skeleton

Phase 1 explicitly does NOT cover:
- Any business module (accounts, vendors, inventory, projects) — that's Phases 2-5
- Production polish, demo deployments, marketing materials — that's Phase 6
- Mobile native apps — out-of-MVP per plan §13
- Any integration with concurrent sessions' work (component/i18n/a11y) beyond what the existing repo already provides

## Affected Sunfish Areas

Impact markers approximate; Stage 01 Discovery will refine.

| Area | Impact | Note |
|---|---|---|
| `accelerators/anchor/Sunfish.Anchor` | **affected** (heavy) | Existing MAUI shell needs module-plugin host wiring + sync-daemon embed + identity-store integration. Currently a near-empty MAUI app per `accelerators/anchor/Sunfish.Anchor.csproj`. |
| `accelerators/bridge/Sunfish.Bridge` | **affected** (heavy) | Existing ASP.NET Core shell needs sync-relay endpoint + multi-tenant routing + per-tenant ciphertext-only storage per ADR 0031 invariant. |
| `accelerators/bridge/Sunfish.Bridge.Data` | **affected** | Per-tenant SQLite shard storage (ciphertext only) + tenant-id routing. ADR 0015 module-entity pattern applies. |
| `packages/kernel-runtime` | **possible** (light) | Module-plugin host orchestrator may need new contracts for "module that ships UI surface" vs "module that ships sync-only data." Stage 02 Architecture decides. |
| `packages/kernel-sync` | **affected** | Sync-daemon protocol per Appendix A wire format. Existing `InMemorySyncDaemonTransport` (Wave 2.1) is the testing substrate; this PR adds the production transport. |
| `packages/kernel-lease` | **stable** (no change) | PR #118 + PR #155 closed the Flease coordinator + ScheduleReservationCoordinator wiring. No further changes in Phase 1. |
| `packages/kernel-security` | **possible** (heavy if missing) | Ed25519 device-key store + Argon2id KEK derivation + per-tenant DEK envelope per book Ch15. Need to discover whether package exists or must be created. |
| `packages/foundation-persistence` | **affected** | SQLCipher integration for Anchor's encrypted-at-rest store. Existing pattern uses EFCore + Postgres for Bridge; need parallel SQLCipher pattern for Anchor. |
| `packages/kernel-schema` | **possible** | Schema evolution + epoch coordination per book Ch13; may exist already as `kernel-schema-registry`. |
| `apps/kitchen-sink` | **stable** (no change) | Phase 1 is pre-business-module; no new demos. |
| `tooling/scaffolding-cli` | **possible** | A `dotnet sunfish bundle install accounts` subcommand will be needed in Phase 2; out of Phase 1 scope but architecture should not preclude. |
| `icm/pipelines/sunfish-inverted-stack-conformance/` | **new** | New pipeline variant per Phase 1 deliverable #6. Includes `README.md`, `routing.md`, `deliverables.md`. |
| `icm/01_discovery/output/` | **new artifacts** | First baseline conformance scan reports committed here per plan §1 success criterion #6. |
| `Sunfish.slnx` | **affected** | If `packages/kernel-security` is created, add to slnx. |

## Open Questions

1. **kernel-security package discovery.** Does a `packages/kernel-security` exist today, or do `Sunfish.Foundation.Security` types live inside `packages/foundation/` or `packages/kernel-runtime/`? Need to map current security-primitive surface before designing the Phase 1 identity + key custody layer. Discovery task.

2. **Anchor MAUI vs Tauri evaluation.** Plan §3 risk register names Tauri as a fallback if MAUI cross-platform stability becomes a blocker. Phase 1 should benchmark MAUI on Win/Mac/Linux during the first 2 weeks; if Linux MAUI is too unstable, formal go/no-go decision before proceeding past identity layer. Need an ADR for either outcome.

3. **Sync wire protocol.** Plan §3 says "CBOR over Noise_XX over UDS / TCP" per Appendix A of the book. Confirm Appendix A is the canonical wire format spec; if it diverges from existing `InMemorySyncDaemonTransport`'s shape, need an architecture note on the shape of the migration.

4. **kernel-schema-registry vs schema-on-the-fly.** Plan §3 references book Ch13 for schema evolution, primitive #6. Does Sunfish currently have a schema-registry primitive, or do modules manage their own EFCore migrations independently? If the latter, the bundle-provisioning flow needs to coordinate.

5. **Multi-sig social recovery — implementation reference.** Plan §10 Phase 1 deliverable #5 names primitive #48 for key-loss recovery. Need to read `the-inverted-stack/docs/reference-implementation/concept-index.yaml` for #48's exact specification — is it a 3-of-5 Shamir secret share? A multi-sig threshold signature? Both? The choice meaningfully affects which crypto library Sunfish needs.

6. **Bridge multi-tenant boundaries — Phase 1 scope.** Plan §10 Phase 1 says "single-tenant Bridge first; multi-tenant in Phase 6" (per risk register). Reconcile against §8 deployment-pattern table which lists multi-tenant Bridge as a standard pattern. Clarify: does Phase 1 ship a single-tenant-capable Bridge with multi-tenant DB schema in place, or full single-tenancy with multi-tenant deferred entirely?

7. **Conformance skill location and runtime.** The `local-first-properties` and `inverted-stack-conformance` skills live in `the-inverted-stack/.claude/skills/` per the README. Are they invokable from a Sunfish session, or do they require a dedicated session in the book repo? Affects the ICM pipeline-variant design (deliverable #6).

8. **In-flight session conflicts.** The component-development / i18n / a11y concurrent sessions own dependencies for Phase 2-5 modules but the seam with Phase 1 is unclear. Concrete example: Anchor app shell will need a SunfishUserMenu component (already exists per anatomy.md) — does that component currently work standalone in Anchor, or does it require Bridge's `Sunfish.Bridge.Client` host? Discovery task per Stage 01.

## Proposed First 3 Milestones

Phase 1 spans 2 months per plan §10. These are the first 3 internal milestones, in execution order:

### Milestone 1 — Discovery + ADR landscape (Weeks 1-2)

Output: ADR set + discovery report committed to `icm/01_discovery/output/business-mvp-phase-1-discovery-2026-04-26.md`.

- Map current Sunfish package surface against Phase 1 needs (which packages exist, which need creation, which need extension)
- Resolve all 8 Open Questions above
- Author or update ADRs:
  - **ADR 0044 (or next free slot)** — Anchor app-shell technology choice (.NET MAUI vs Tauri evaluation outcome; benchmark data attached)
  - **ADR 0045** — Sync wire protocol formalization (CBOR + Noise_XX confirmation per book Appendix A)
  - **ADR 0046** — Multi-sig social recovery scheme selection (Shamir / threshold sig / hybrid)
  - **ADR 0047** — Bridge tenancy posture for Phase 1 (single-tenant-with-multi-tenant-schema vs strict-single-tenant)
- File icm/00_intake/ tickets to component / i18n / a11y sessions for any Phase 1 dependencies surfaced by Discovery
- Build the `sunfish-inverted-stack-conformance` pipeline variant (deliverable #6) — this milestone covers the README, routing, deliverables; first concrete intake comes at Milestone 3

### Milestone 2 — Anchor + Bridge runnable shells (Weeks 3-5)

Output: two binaries that launch and pass smoke tests; conformance scan baseline #1.

- Anchor app shell launches on Win/Mac/Linux with module-plugin host wired (no modules yet — verify host can load + unload a stub plugin)
- Bridge service shell starts, exposes sync-relay endpoint, accepts authenticated connection from one Anchor
- Identity layer: Ed25519 device-key generation + storage in OS-native secure enclave (Win Credential Manager / macOS Keychain / Linux libsecret)
- Per-tenant DEK envelope per book Ch15 (KEK derived from owner password via Argon2id; per-tenant DEK encrypted under KEK; per-record DEK encrypted under tenant DEK)
- SQLCipher integration for Anchor's local store
- Smoke test: Anchor launches, generates owner identity, prompts for password, opens encrypted local DB, connects to Bridge, exchanges device-key handshake with second Anchor over LAN

### Milestone 3 — Sync protocol + recovery + first conformance baseline (Weeks 6-8)

Output: end-to-end sync + recovery flow + committed baseline conformance reports.

- Sync wire protocol implementation per ADR 0045 (Milestone 1 output)
- Anchor↔Anchor LAN sync via mDNS discovery — exchange CRDT deltas over Noise_XX session
- Anchor↔Bridge WAN sync — Bridge stores per-tenant ciphertext-only blobs, relays between Anchor instances
- Backup orchestration — Anchor exports encrypted backup blob to user-chosen location
- Multi-sig social recovery (per ADR 0046) — owner can designate 3-of-5 trustees; quorum can re-issue device key on lost laptop
- Run `local-first-properties` skill against the Phase 1 deliverable; commit baseline report to `icm/01_discovery/output/business-mvp-phase-1-localfirst-baseline-2026-04-26.md`
- Run `inverted-stack-conformance` skill; commit baseline to `icm/01_discovery/output/business-mvp-phase-1-conformance-baseline-2026-04-26.md`
- Phase 1 deliverable acceptance: "Anchor opens, syncs with another Anchor over LAN, syncs with Bridge over WAN, key recovery flow works end-to-end" — demonstrated via 3 recorded smoke runs

## Next Stage

Pending review of this intake by the BDFL.

If approved, proceed to **Stage 01 Discovery** — execute Milestone 1 above.
If revisions requested, update this intake in place and re-route.

## Coordination Notes

- This session owns: business application modules (Phases 2-5) + Anchor app-shell + Bridge service + local-first compliance verification per module per Kleppmann property
- Other sessions own (do NOT touch): Sunfish.UiBlocks (component dev) / Multi-language (i18n / l10n) / Disabled-user (a11y / WCAG)
- Cross-session needs filed via `icm/00_intake/` addressed to the responsible session

## References

- Spec source (do not modify): `C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md`
- Spec orientation (do not modify): `C:/Projects/the-inverted-stack/docs/business-mvp/README.md`
- Sunfish ICM: `icm/CONTEXT.md`
- Recent session foundation work this Phase 1 builds on: PRs #118 (Flease real race fix), #128 (LocUnused analyzer wiring), #155 (D6 ScheduleReservationCoordinator), #149 (ADRs 0038-0042), #157 (council review), #158 (ADR 0043 unified threat model)
- Repo anatomy: `.wolf/anatomy.md`
