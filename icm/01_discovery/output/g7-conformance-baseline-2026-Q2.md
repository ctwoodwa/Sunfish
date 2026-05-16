# G7 Conformance Baseline Scan — 2026-Q2

**Date:** 2026-05-16
**Scanner:** XO research session
**Sunfish commit:** `6c262a69` (W#63 P2+3 — recovery-host polling pipeline + ledger flip)
**CI state at scan:** all green (W#63 P2 PR #867 merged, all 7 checks SUCCESS)

---

## Summary

| G-item | Description | Status | Gap |
|---|---|---|---|
| G1 | GossipDaemon hosted service | **PASS** | — |
| G2 | DELTA_STREAM → ICrdtDocument | **PASS** | — |
| G3 | Bridge posture | **PASS** | rate-limiting + security headers (Phase 2 hardening) |
| G4 | Anchor↔Bridge WAN relay | **PASS** | — |
| G5 | Backup orchestration | **PASS** | — |
| G6 | ADR 0046 recovery flow | **CLOSED** | W#65 + W#66 closed G6-B (ApproveRecoveryPage live); W#67 closed G6-A (SQLCipher rekey via seed-delivery protocol per ADR 0046-A6) |

---

## Phase 1 verdict: **PASS — 6/6 PASS**

All six G-items are fully satisfied. G6 closed 2026-05-16 via W#65 (PR #868 — `ISessionSignerAccessor` + `IBoundEd25519Signer`), W#66 (PR #870 — `ApproveRecoveryPage` live attestation), and W#67 (PRs #875–#903 — ADR 0046-A6 seed-delivery protocol + full social-recovery implementation). Phase 2 is unblocked.

---

## G1 — GossipDaemon hosted service

**Paper property:** P2 (multi-device), P4 (collaboration)

**Files verified:**
- `accelerators/anchor/Services/AnchorSyncHostedService.cs` ✅
- `accelerators/anchor/tests/AnchorSyncHostedServiceTests.cs` ✅

**DI wiring in `MauiProgram.cs`:**
- `builder.Services.AddSunfishKernelSync()` (line 205) ✅
- `builder.Services.AddHostedService<AnchorSyncHostedService>()` (line 221) ✅

**Tests confirmed:**
- `StartAsync_starts_the_gossip_daemon` ✅
- `StopAsync_stops_the_gossip_daemon` ✅
- `StartAsync_seeds_daemon_with_already_discovered_LAN_peers` ✅
- `StopAsync_disposes_discovery_subscription_so_later_PeerDiscovered_does_not_reach_daemon` ✅

**Verdict: PASS** — GossipDaemon is wired, starts on host startup, and is covered by four behavioral tests.

---

## G2 — Wave 2.5 DELTA_STREAM → ICrdtDocument application

**Paper property:** P3 (network optional), P4 (collaboration)

**Files verified:**
- `packages/kernel-sync/tests/TwoNodeDeltaStreamTests.cs` ✅

**Test confirmed:**
- `TwoNode_DeltaStream_AppliesToReceiver_CRDT` ✅

The test was thought to potentially be missing — it exists. `TwoNodeDeltaStreamTests` uses two in-process gossip daemon contexts on `InMemoryPeerDiscovery`; node A mutates a CRDT document, rounds the gossip protocol, and node B's projection is verified to match.

**Verdict: PASS** — the two-node CRDT delta-stream round-trip test exists and is CI-green.

---

## G3 — Bridge posture decision

**Paper property:** P2, P3, P6

**Posture verified:**
- `accelerators/bridge/Sunfish.Bridge/BridgeMode.cs`: defines `BridgeMode.SaaS` + `BridgeMode.Relay` ✅
- `accelerators/bridge/Sunfish.Bridge/BridgeOptions.cs`: `Mode` defaults to `BridgeMode.SaaS` ✅
- Matches Phase 1 intent: Bridge runs SaaS posture for Phase 1 (multi-tenant shell + relay)

**Security middleware in `Program.cs`:**
- `UseHsts()` ✅
- `UseHttpsRedirection()` ✅
- `UseAuthorization()` + `UseAntiforgery()` ✅
- `TenantSubdomainResolutionMiddleware` ✅
- `UseWebSockets()` + `MapHub<BridgeHub>` ✅

**Phase 2 hardening gaps (not G3 criteria, documented for tracking):**
- No `AddRateLimiter()` / `UseRateLimiter()` — not in Program.cs
- No explicit security response headers beyond HSTS (no CSP, no X-Frame-Options header middleware)

**Verdict: PASS** — Bridge posture is `SaaS` (the Phase 1 intent). Rate limiting and CSP headers are Phase 2 hardening items, not Phase 1 criteria.

---

## G4 — Anchor↔Bridge WAN connection (ciphertext-only relay)

**Paper property:** P3, P6

**Files verified:**
- `packages/kernel-sync/Discovery/ManagedRelayPeerDiscovery.cs` ✅
- `packages/kernel-sync/Discovery/ManagedRelayPeerDiscoveryOptions.cs` ✅
- `packages/kernel-sync/tests/ManagedRelayPeerDiscoveryTests.cs` ✅

**DI wiring in `MauiProgram.cs`:**
- `builder.Services.AddManagedRelayPeerDiscovery(opts => ...)` (line 207) ✅

**Ciphertext-only invariant:**
- Bridge relay (`TenantWebSocketProxy`) forwards CBOR gossip frames as opaque blobs to the peer — no inner-payload decryption call found in the relay path ✅
- `ManagedRelayPeerDiscovery` on the Anchor side holds the relay endpoint URL + options; the inner CRDT deltas are E2E encrypted before the CBOR envelope is formed ✅

**Two-node WAN integration test:** does not exist. The `TwoNodeDeltaStreamTests` uses `InMemoryPeerDiscovery`, not the managed-relay transport. A relay integration test (Anchor A → Bridge websocket relay → Anchor B) is a Phase 2 test-expansion item.

**Verdict: PASS** — relay peer discovery is wired, ciphertext-only invariant holds by code inspection, tests pass. WAN integration test is a Phase 2 gap (not a Phase 1 criterion).

---

## G5 — Anchor backup orchestration

**Paper property:** P5 (long-now), P7 (ownership)

**Files verified:**
- `accelerators/anchor/Services/AnchorBackupService.cs` ✅
- `accelerators/anchor/tests/AnchorBackupServiceTests.cs` ✅
- `accelerators/anchor/Components/Pages/BackupPage.razor` ✅

**Test confirmed:**
- `ExportImport_round_trip_preserves_50_text_operations` (line 21 of `AnchorBackupServiceTests.cs`) ✅

The round-trip test exports a snapshot of 50 text CRDT operations, reimports into a fresh Anchor context, and verifies the document projection matches.

**Verdict: PASS** — backup service, round-trip test, and UI page are all present and CI-green.

---

## G6 — ADR 0046 key-loss recovery flow

**Paper property:** P7 (ownership) — non-negotiable per ADR 0046

### What is built ✅

**Backend substrate (W#8, PRs #178 + #185, 2026-04-28):**
- `packages/foundation-recovery/RecoveryCoordinator.cs` ✅
- `packages/foundation-recovery/IRecoveryCoordinator.cs` ✅
- Full `RecoveryEvent` / `RecoveryEventType` surface ✅
- `PaperKeyDerivation` ✅

**Anchor UI + wiring (W#63, PRs #866 + #867, 2026-05-16):**
- `accelerators/anchor/Components/Pages/Recovery/TrusteeSetupPage.razor` ✅
- `accelerators/anchor/Components/Pages/Recovery/InitiateRecoveryPage.razor` ✅
- `accelerators/anchor/Components/Pages/Recovery/ApproveRecoveryPage.razor` ✅ (placeholder — see gap below)
- `accelerators/anchor/Components/Pages/Recovery/RecoveryStatusPage.razor` ✅
- `accelerators/anchor/Components/Pages/Recovery/PaperKeyPage.razor` ✅
- `accelerators/anchor/Services/RecoveryGracePollingService.cs` ✅ (polling-based host per ruling 2026-05-16 §c — `IRecoveryCoordinator` exposes no event subscription)
- `accelerators/anchor/Services/IRecoveryCompletionHandler.cs` + `AnchorRecoveryCompletionHandler.cs` ✅
- `accelerators/anchor/Services/RecoveryHostExtensions.cs` ✅
- DI: `builder.Services.AddAnchorRecoveryHost(builder.Configuration)` in `MauiProgram.cs` ✅

**Tests confirmed (CI green):**
- `Recovery_3of5Trustees_7DayGrace_Completion_Canonical` (`RecoveryCoordinatorTests.cs`, line 428) ✅ — 5-trustee, 3-quorum canonical scenario
- `StartAsync_DispatchesRecoveryCompleted_WhenStartupPollReturnsIt` ✅
- `StartAsync_DoesNotDispatch_WhenStartupPollReturnsNonCompletedEvent` ✅
- `StartAsync_DoesNotDispatch_WhenStartupPollReturnsNull` ✅
- `StopAsync_AllowsSubsequentStart_WithoutDispatchingStaleEvents` ✅
- `LoopTick_DispatchesRecoveryCompleted_AfterFirstInterval` ✅

### Gaps closed ✅ (all resolved 2026-05-16)

**Gap G6-A: SQLCipher rekey — CLOSED by W#67 (PRs #875–#903)**
- ADR 0046-A6 (seed-delivery protocol) authored and accepted. `TrusteeAttestation.EncryptedSeedShare` added via ECIES key transport. `AnchorRecoveryCompletionHandler.HandleAsync` now calls `IEncryptedStore.RotateKeyAsync` with the decrypted seed. Full 6-PR social-recovery chain shipped.

**Gap G6-B: `ApproveRecoveryPage` — CLOSED by W#65 + W#66 (PRs #868 + #870)**
- W#65 shipped `ISessionSignerAccessor` + `IBoundEd25519Signer` (PR #868). W#66 wired live attestation submission from `ApproveRecoveryPage` (PR #870). Trustees can now submit real `TrusteeAttestation` objects.

**Verdict: PASS** — all recovery surfaces are fully wired and tested; G6 is complete.

---

## Gaps summary

| Gap ID | Description | Effort | Workstream |
|---|---|---|---|
| ~~G6-A~~ | ~~SQLCipher rekey stubbed~~ | **CLOSED** — W#67 PRs #875–#903 | W#67 `built` |
| ~~G6-B~~ | ~~ApproveRecoveryPage placeholder~~ | **CLOSED** — W#65 PR #868 + W#66 PR #870 | W#65+W#66 `built` |
| G3-H | Rate limiting in Bridge Program.cs missing | ~2-4h | Phase 2 hardening; unowned |
| G3-H | Security response headers (CSP, X-Frame-Options) missing | ~1-2h | Phase 2 hardening; unowned |
| G4-T | No WAN relay integration test (Anchor→Bridge relay→Anchor) | ~3-4h | Phase 2 test-expansion; unowned |

**G6 fully closed 2026-05-16:** W#65 (PR #868), W#66 (PR #870), and W#67 (PRs #875–#903) all merged. Phase 1 G7 conformance = **PASS**.

---

## Next steps

1. ~~**COB:** build W#65~~ — **DONE** PR #868 merged 2026-05-16
2. ~~**COB:** build W#66~~ — **DONE** PR #870 merged 2026-05-16
3. ~~**XO:** author ADR 0046 amendment (A6) defining seed-delivery protocol~~ — **DONE** W#67 6-PR chain (PRs #875–#903) merged 2026-05-16
4. **Phase 2 workstreams unblocked** — G1–G6 all green; conformance is PASS
5. ~~**CO:** ADR 0055 Proposed→Accepted~~ — **DONE** PR #916 merged 2026-05-16
6. **CO:** W#64 A vs B design decision (entity-switcher approach) — still pending
