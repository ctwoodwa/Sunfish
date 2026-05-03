# Stage 05 Implementation Plan — MVP Phase 1

**Date:** 2026-04-26
**Predecessor:** `icm/01_discovery/output/business-mvp-phase-1-discovery-final-2026-04-26.md` (Stage 01 final)
**Pipeline variant:** `sunfish-inverted-stack-conformance` (per variant landed in PR #162)
**Stages skipped per fast-track:** 02 Architecture (ADRs 0044+0046 cover decisions), 03 Package design (no new public APIs — extends existing `kernel-sync` / `kernel-security` / Bridge surface), 04 Scaffolding (no generator templates needed)

## Updated scope statement (post-Discovery)

Stage 01 Discovery surfaced that Phase 1 is **far more done than the intake assumed**. Specifically:

- Anchor identity layer (root seed → Ed25519 → NodeIdentity → team subkeys → SqlCipher KDF → default-team registrar → store activator → bootstrap + session + QR onboarding hosted services) — **DONE** in `accelerators/anchor/MauiProgram.cs` Wave 6.x work
- kernel-sync transport (UDS / NamedPipe / WebSocket / InMemory), CBOR codec, handshake, vector-clock gossip daemon, mDNS peer discovery (Wave 2.2) — **DONE** in `packages/kernel-sync/`
- Bridge sync wiring + dual-posture (SaaS / Relay) + `RelayServer` + tenant-aware accept loop — **DONE** in `accelerators/bridge/Sunfish.Bridge/Program.cs` + `Relay/`
- Phase 1 #6 (ICM pipeline variant) — **DONE** via PR #162

The remaining Phase 1 work is **7 concrete gaps**, listed below with per-task Kleppmann property labels, acceptance criteria, and execution order.

## Gap inventory + ordered tasks

### G1 — Anchor GossipDaemon hosted service

**Property:** P2 (multi-device), P4 (collaboration)
**Status:** Infrastructure exists; not wired into Anchor's MAUI hosted-service pipeline.
**Evidence:** `accelerators/anchor/Services/AnchorSessionService.cs:18` says *"kernel-sync's IGossipDaemon and local-node-host IPC is deferred to future waves"*. The daemon, transport, mDNS, and DI extensions all exist in `packages/kernel-sync/` (see kernel-sync README §"Using it" for the canonical wire-up).

**Tasks:**
1. Add `using Sunfish.Kernel.Sync.DependencyInjection;` to `MauiProgram.cs`
2. Resolve listen-endpoint convention for Anchor (Bridge uses Unix-domain-socket / Named Pipe per `BridgeOptions.SyncListenEndpoint`; Anchor needs same convention with per-install path under `FileSystem.AppDataDirectory/sync/`)
3. Register `ISyncDaemonTransport` → `UnixSocketSyncDaemonTransport(listenEndpoint)` (UDS on POSIX / Named Pipe on Windows)
4. Call `builder.Services.AddSunfishKernelSync()` (registers `IGossipDaemon` + handshake protocol)
5. Call `builder.Services.AddMdnsPeerDiscovery()` for tier-1 LAN discovery
6. Add new `AnchorSyncHostedService` to drive `daemon.AttachDiscovery(discovery)` then `daemon.StartAsync(...)` on Anchor lifetime
7. Register the new hosted service via `builder.Services.AddHostedService<AnchorSyncHostedService>()` AFTER `AnchorBootstrapHostedService` (the bootstrap service guarantees default-team materialization completes before sync starts)

**Files affected:**
- `accelerators/anchor/MauiProgram.cs` (modified, ~10 lines added)
- `accelerators/anchor/Services/AnchorSyncHostedService.cs` (new, ~50 lines)

**Acceptance criterion (conformance test):**
A new test `Anchor_StartsGossipDaemon_OnHostStartup` in a new `accelerators/anchor/tests/` project (or extends `accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/` if Anchor doesn't have a unit test project yet) asserts: building `MauiApp`, starting hosted services, then querying `IGossipDaemon.State` returns `Started` within 2 seconds.

### G2 — Wave 2.5 DELTA_STREAM → ICrdtDocument application

**Property:** P3 (network optional), P4 (collaboration)
**Status:** kernel-sync's `GossipDaemon` accepts `DELTA_STREAM` messages via the wire codec, but per kernel-sync README *"Wave 2.5 — Round-loop wiring into ICrdtDocument for actual delta application on receipt"* is deferred. This means deltas are received but discarded today.
**Evidence:** Read `packages/kernel-sync/Gossip/GossipDaemon.cs` to confirm where the DELTA_STREAM handler currently no-ops or logs.

**Tasks:**
1. Inspect current `DELTA_STREAM` receive path in `GossipDaemon.cs`
2. Identify the integration seam — likely a callback or `IDeltaStreamSubscriber` interface that GossipDaemon publishes to
3. Wire Anchor's `IActiveTeamAccessor` → resolve the active team's `ICrdtDocument` → call `ApplyDelta(...)` on each received DELTA_STREAM
4. Handle delta-application errors (corrupt delta, schema mismatch, unknown CRDT type) with structured logging + per-error metric
5. Add per-team back-pressure if delta application is slower than receive rate (existing `Protocol/DeltaStreamRateLimiter.cs` may help)

**Files affected:**
- `packages/kernel-sync/Gossip/GossipDaemon.cs` OR a new `Sunfish.Kernel.Sync.Application` sub-folder (depends on the seam shape — Stage 06 detail)
- `accelerators/anchor/Services/AnchorSyncHostedService.cs` (extends G1 with the subscriber)
- New tests in `packages/kernel-sync/tests/`

**Acceptance criterion (conformance test):**
A test `TwoNode_DeltaStream_AppliesToReceiver_CRDT` spins up 2 in-process Anchor-equivalent contexts on `InMemorySyncDaemonTransport`, has node A mutate a CRDT, runs a round, asserts node B's local CRDT projection reflects the mutation.

### G3 — Bridge posture decision for Phase 1 (SaaS + Relay vs Relay-only)

**Property:** P2 (multi-device), P3 (network optional), P6 (security — ciphertext-only relay)
**Status:** Bridge supports both postures via `BridgeOptions.Mode` (SaaS | Relay). Phase 1 spec says "Bridge service shell" — doesn't specify which posture(s).
**Decision needed:** Phase 1 ships Relay-mode only? Both? Default = SaaS with Relay opt-in?
**Recommendation:** **Both, with SaaS as default**. The Bridge.AppHost (Aspire) already orchestrates multiple processes. SaaS gives the browser-accessible shell (matches ADR 0031 Zone-C posture); Relay provides the tier-3 managed-relay path Anchors connect to over WAN. Phase 1 needs the Relay path operational; SaaS shell remains as-is from prior work.

**Tasks:**
1. Verify Bridge default config sets `BridgeOptions.Mode = SaaS` (today's behavior)
2. Add a Phase 1 smoke-test config (`accelerators/bridge/Sunfish.Bridge.AppHost/appsettings.Phase1Smoke.json`) that runs both SaaS + Relay posture in the AppHost orchestration
3. Document the default Anchor↔Bridge connection topology in `docs/specifications/anchor-bridge-connection-topology.md` (new spec doc)

**Files affected:**
- `accelerators/bridge/Sunfish.Bridge.AppHost/appsettings.Phase1Smoke.json` (new)
- `docs/specifications/anchor-bridge-connection-topology.md` (new)

**Acceptance criterion:**
Manual smoke test: launch Bridge.AppHost with `--launch-profile Phase1Smoke`, observe both SaaS Bridge AND Relay listener come up, verify health-check endpoints return 200 for both.

### G4 — Anchor↔Bridge WAN connection (ciphertext-only relay)

**Property:** P3 (network optional), P6 (security)
**Status:** Bridge has `RelayServer` accepting `ISyncDaemonTransport` connections; Anchor (post-G1) will run a `GossipDaemon` capable of dialing transports. Need: (a) a discovery mechanism for Anchor to find the Bridge relay endpoint (mDNS doesn't span WAN); (b) a TCP-or-WebSocket transport variant for crossing NAT; (c) confirm ciphertext-only invariant per ADR 0031 / paper §17.2.
**Evidence:** `WebSocketSyncDaemonTransport.cs` already exists in `kernel-sync/Protocol/` — that's the WAN substrate.

**Tasks:**
1. Add `ManagedRelayPeerDiscovery` to `kernel-sync/Discovery/` (new) — a peer-discovery impl that reads a configured Bridge relay URL from Anchor settings and emits a single `PeerAdvertisement` for the Bridge endpoint
2. Wire Anchor's settings UI to capture the Bridge relay URL (new settings page or extend existing one)
3. Anchor's GossipDaemon attaches BOTH `MdnsPeerDiscovery` (LAN) AND `ManagedRelayPeerDiscovery` (WAN to Bridge) — they coexist; daemon round picks from the combined peer set
4. Bridge's `RelayServer` accepts WebSocket connections via the existing transport; verify ciphertext-only invariant (relay sees CBOR envelopes; the inner payload bytes are encrypted end-to-end via the role-key derivation from `kernel-security/Keys/`)

**Files affected:**
- `packages/kernel-sync/Discovery/ManagedRelayPeerDiscovery.cs` (new)
- `packages/kernel-sync/DependencyInjection/ServiceCollectionExtensions.cs` (extended with `AddManagedRelayPeerDiscovery(...)`)
- `accelerators/anchor/Services/` settings-capture wiring (new)
- `accelerators/anchor/MauiProgram.cs` (extended G1 wiring)

**Acceptance criterion (smoke test):**
Two Anchors + one Bridge running in Aspire; Anchor A mutates a CRDT, Anchor B (configured with the same Bridge relay URL but on a different network namespace if available) receives the delta within 30s.

### G5 — Anchor backup orchestration

**Property:** P5 (long-now), P7 (ownership)
**Status:** No existing `AnchorBackupService` or equivalent in `accelerators/anchor/Services/`. Backup primitives at the SQLCipher level exist (the encrypted DB file IS the backup substrate); orchestration that exports a versioned, signed, user-portable blob does not.
**Evidence:** `find` for `Backup` in `accelerators/anchor/` returns nothing; same for `Restore`.

**Tasks:**
1. Define backup blob format (proposed: tar.gz of `{rootSeed.kdfwrapped, attestations.cbor, team-shards/<teamId>/{schema-epoch.cbor, sqlcipher.db.snapshot, plain-export.beancount.txt}}`)
2. Implement `AnchorBackupService` with `ExportAsync(Stream destination, BackupOptions options)` and `ImportAsync(Stream source, RestoreOptions options)` methods
3. Wire a Backup page in Anchor's UI (Razor component under `accelerators/anchor/Components/Pages/`)
4. SQLCipher snapshot via `VACUUM INTO` (atomic per SQLite docs)
5. Signed audit log entry per backup (per primitive #48f)

**Files affected:**
- `accelerators/anchor/Services/AnchorBackupService.cs` (new, ~150 lines)
- `accelerators/anchor/Components/Pages/Backup.razor` (new, ~80 lines)
- New tests in `accelerators/anchor/tests/` (or sibling test project)

**Acceptance criterion (conformance test):**
`Anchor_BackupRoundTrip_PreservesTeamData` test: create a team, write 50 CRDT operations across 3 record types, export backup, delete team, import backup into fresh Anchor, verify all 50 operations present + projections match byte-for-byte.

### G6 — ADR 0046 key-loss recovery flow

**Property:** P7 (ownership) — non-negotiable per ADR 0046
**Status:** No existing recovery service. ADR 0046 selected sub-patterns 48a (multi-sig social) + 48e (timed grace) + 48f (signed audit) + 48c (paper-key fallback). The kernel-security primitives (Ed25519, X25519, SqlCipher KDF, root seed) cover the cryptographic substrate; the recovery LAYER on top of these does not yet exist.

**Tasks:**
1. **Trustee designation** — owner Settings page lets owner add 5 trustees by NodeIdentity public key (paste / scan QR). Each trustee receives a trustee-attestation generated by `IAttestationIssuer`.
2. **Recovery initiation** — new device sends recovery-request CBOR message to all 5 trustees via the existing sync transport (signed by new device's ephemeral key). Trustees see request in their Anchor's notifications.
3. **Trustee attestation** — each trustee's UI shows the request with origin device-fingerprint; trustee approves (or rejects). Approval = signed attestation message back to new device.
4. **Quorum + grace period** — once 3 trustees attest, new device starts 7-day timer (configurable per `LeaseCoordinatorOptions`-style options). During grace, original device (if still has keys) can dispute via signed dispute message.
5. **Key reissue** — after grace expires without dispute, new device derives new device key, re-encrypts SQLCipher with new key (via key rotation primitive — verify exists in kernel-security), writes recovery-event entry to per-tenant audit log (signed by all 3 attesting trustees + new device).
6. **Paper-key fallback** — owner Settings page generates 24-word BIP-39 phrase deterministically from root seed; offers to display once + print. Recovery via paper-key skips the trustee/grace path entirely (paper-key holder IS the legitimate owner).
7. **Audit log entry per recovery event** (per primitive #48f) — visible in tenant audit-log UI.

**Files affected:**
- `packages/kernel-security/Recovery/` (new sub-folder)
  - `IRecoveryCoordinator.cs`
  - `RecoveryCoordinator.cs`
  - `TrusteeAttestation.cs`
  - `RecoveryRequest.cs`
  - `RecoveryEvent.cs`
  - `PaperKeyDerivation.cs` (BIP-39)
- `accelerators/anchor/Components/Pages/Recovery/` (new sub-folder for UI)
  - `TrusteeSetup.razor`
  - `InitiateRecovery.razor`
  - `ApproveRecoveryRequest.razor`
  - `PaperKey.razor`
- New tests in `packages/kernel-security/tests/`

**Acceptance criterion (conformance test):**
`Recovery_3of5Trustees_GracePeriod_KeyReissue` test simulates 5 trustees, original device offline, new device initiates request, 3 trustees approve, advance simulated clock 7 days, verify new device gets reissued key + audit log entry signed by all 3 trustees + new device.

### G7 — First conformance baseline scan

**Property:** All P1-P7
**Status:** Skills exist (`local-first-properties` + `inverted-stack-conformance` in `the-inverted-stack/.claude/skills/`); they're ICM-aware and write into Sunfish's `icm/01_discovery/output/`. No baseline has been run.

**Tasks:**
1. After G1-G6 land, invoke `local-first-properties` skill from a session with both repos in scope
2. Skill writes `icm/01_discovery/output/business-mvp-phase-1-localfirst-baseline-2026-04-26.md` (or near-equivalent date)
3. Invoke `inverted-stack-conformance` skill (deeper scan)
4. Skill writes `icm/01_discovery/output/business-mvp-phase-1-conformance-baseline-2026-04-26.md`
5. Phase 1 spec target: ≥80% on `local-first-properties`, ≥60% on `inverted-stack-conformance`. Compare actual scores; gap-list anything below target with proposed remediation
6. Commit baselines to PR

**Files affected:**
- `icm/01_discovery/output/business-mvp-phase-1-localfirst-baseline-<date>.md` (new — written by skill)
- `icm/01_discovery/output/business-mvp-phase-1-conformance-baseline-<date>.md` (new — written by skill)
- `waves/conformance/phase-1-exit-report-<date>.md` (new — narrative summary + recommendations)

**Acceptance criterion (per plan §1 success criterion #2 + #3):**
- `local-first-properties` ≥80% across P1-P7
- `inverted-stack-conformance` ≥60% across the 562-concept catalog

If either fails the threshold, the Phase 1 deliverable is INCOMPLETE — surface as a stop condition; gap-list iterates back into Stage 06 for additional work.

## Execution order + dependencies

```
G3 (Bridge posture decision) → independent → can ship anytime
G1 (Anchor GossipDaemon HS) → blocks G2, G4
G2 (DELTA_STREAM application) → blocks G7 (delta sync must work for conformance)
G4 (Anchor↔Bridge WAN) → blocks G7
G5 (Backup) → independent → can ship after G1
G6 (Recovery) → independent of sync; depends on identity (DONE)
G7 (Conformance scan) → ALL of G1-G6 should land first; otherwise the scan baseline understates gaps
```

Recommended order:
1. G3 (config + spec doc — small, gates nothing — lands first to derisk Bridge posture confusion)
2. G1 (Anchor sync hosted service — wiring, low risk)
3. G2 (DELTA_STREAM application — modifies kernel-sync surface; coordinate with kernel-sync test suite)
4. G4 (Anchor↔Bridge WAN — depends on G1)
5. G5 + G6 in parallel (independent of each other and of G1-G4 once identity is in place)
6. G7 (conformance scan — final gate)

## Per-task acceptance ↔ Kleppmann property cross-reference

| Task | P1 | P2 | P3 | P4 | P5 | P6 | P7 |
|---|---|---|---|---|---|---|---|
| G1 Anchor GossipDaemon | | ✅ | | ✅ | | | |
| G2 DELTA_STREAM application | | | ✅ | ✅ | | | |
| G3 Bridge posture | | ✅ | ✅ | | | ✅ | |
| G4 Anchor↔Bridge WAN | | ✅ | ✅ | | | ✅ | |
| G5 Backup | | | | | ✅ | | ✅ |
| G6 Recovery | | | | | | | ✅ |
| G7 Conformance scan | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

P1 (no spinners) is implicitly tested at every task — daemon ops + UI ops + backup all must hit the 16 ms / 100 ms / 30 s budgets per primitive #43.

## Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| G2's DELTA_STREAM application uncovers ICrdtDocument shape mismatches | Medium | High | Review `kernel-crdt` package's `ICrdtDocument` surface during G2 design; spike a 1-day prototype before full implementation |
| G4's NAT traversal complexity (two Anchors behind different NATs both connecting to Bridge) | Medium | Medium | Bridge as central rendezvous handles NAT — both Anchors initiate outbound to Bridge; no peer-to-peer NAT punching needed for Phase 1; defer P2P NAT-punch to post-MVP |
| G5's SQLCipher VACUUM INTO atomicity assumption | Low | High | Verify VACUUM INTO holds the SQLite write lock for the duration; add lockfile around backup operation; backup tool documents "single-writer-during-backup" requirement |
| G6's 7-day grace period needs persistent timer that survives device restart | Medium | High | Persist recovery-request state in encrypted local DB (per-tenant audit log table); on Anchor startup, re-evaluate any in-flight recovery requests against current wall-clock |
| G6's trustee distribution mechanism doesn't exist yet (how does owner SEND attestation to trustees if they're on different devices?) | Medium | Medium | Use the same QR onboarding flow that exists for join-additional-team (`QrOnboardingService.cs`) — owner generates trustee-attestation QR; trustee scans to receive |
| G7 conformance scan reveals catalog primitives we haven't implemented yet | High | Medium | Expected — that's the point of the baseline. Gap-list informs Phase 1.5 / Phase 6 polish. The plan §1 success criterion is ≥80% local-first / ≥60% conformance, not 100%. |
| MAUI 10 Mono-runtime instability surfaces during G1's hosted-service wiring (per ADR 0044's revisit triggers) | Medium | High | Test G1 on Win64-only first per ADR 0044; if MAUI 10 stabilizes mid-Phase-1, re-enable Mac/Linux targets per ADR 0044 §"Revisit triggers" |

## Stage-completion checklist

When all 7 gaps land + the conformance baseline meets thresholds:

- [ ] G1 Anchor GossipDaemon hosted service shipped + test passing
- [ ] G2 DELTA_STREAM application shipped + 2-node delta-propagation test passing
- [ ] G3 Bridge dual-posture smoke config shipped + manual smoke verified
- [ ] G4 Anchor↔Bridge WAN connection shipped + 2-Anchor-via-Bridge delta test passing
- [ ] G5 Anchor backup service shipped + round-trip test passing
- [ ] G6 Recovery flow shipped + 3-of-5 trustee + 7-day grace test passing
- [ ] G7 `local-first-properties` baseline ≥80% committed
- [ ] G7 `inverted-stack-conformance` baseline ≥60% committed
- [ ] Phase 1 deliverable acceptance smoke recording: "Anchor opens, syncs with another Anchor over LAN, syncs with Bridge over WAN, key recovery flow works end-to-end" — 3 recorded runs

## Estimated effort (per gap, ranges)

| Gap | Optimistic | Realistic | Pessimistic |
|---|---|---|---|
| G1 | 0.5 day | 1 day | 2 days |
| G2 | 1 day | 2 days | 4 days |
| G3 | 0.5 day | 1 day | 2 days |
| G4 | 2 days | 3 days | 5 days |
| G5 | 1.5 days | 3 days | 5 days |
| G6 | 3 days | 5 days | 8 days |
| G7 | 0.5 day (skill runs) + variable for gap-fix iterations | 1 day + variable | depends on baseline gaps |
| **Total** | **9 days** | **16 days** | **26+ days** |

The intake estimated 8 weeks (40 working days) for Phase 1. Even pessimistic Stage 05 estimate (26 days) leaves 14 days of buffer for Phase 2 prep / additional polish / unforeseen integration issues. **Phase 1 should ship inside the 8-week budget comfortably.**

## Out-of-scope for Phase 1 (deferred to Phase 1.5 or later)

- Mac/Linux Anchor (per ADR 0044)
- WireGuard mesh-VPN peer discovery (per kernel-sync README — Wave 2.2 tier-2 deferred)
- Bucket-eligibility evaluation (Wave 2.4 deferred per kernel-sync README — handshake policy is "grant everything" today)
- Institutional custodian recovery + biometric recovery (per ADR 0046 — 48b + 48d deferred)
- Cross-platform NAT-punch P2P sync (G4 risk register — Bridge-mediated is sufficient for Phase 1)

## Next stage

Advance to **Stage 06 Build**. Per the variant routing, each PR for G1-G7 must include the conformance test specified in the per-task acceptance criterion above (or a tracked deferral). Stage 07 Review re-runs the conformance scan delta after each PR and verifies no regression against the prior baseline.

## References

- Predecessor: `icm/01_discovery/output/business-mvp-phase-1-discovery-final-2026-04-26.md`
- Pipeline variant routing: `icm/pipelines/sunfish-inverted-stack-conformance/routing.md`
- ADR 0044 (Anchor Windows-only): `docs/adrs/0044-anchor-windows-only-phase-1.md`
- ADR 0046 (recovery scheme): `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md`
- kernel-sync README: `packages/kernel-sync/README.md`
- Sync daemon protocol spec: `docs/specifications/sync-daemon-protocol.md`
- Anchor MauiProgram (current wiring depth): `accelerators/anchor/MauiProgram.cs`
- Bridge posture + relay wiring: `accelerators/bridge/Sunfish.Bridge/Program.cs` + `accelerators/bridge/Sunfish.Bridge/Relay/`
- Foundational paper (do not modify): `C:/Projects/the-inverted-stack/_shared/product/local-node-architecture-paper.md`
- MVP plan (do not modify): `C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md` §10 Phase 1
