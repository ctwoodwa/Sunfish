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
| G6 | ADR 0046 recovery flow | **PARTIAL** | SQLCipher rekey stub + ApproveRecoveryPage placeholder |

---

## Phase 1 verdict: **PARTIAL — 5 PASS, 1 PARTIAL**

G1–G5 are fully satisfied. G6 has its core state machine, hosting infrastructure, and 5 Razor pages wired — but two surfaces are deferred: the SQLCipher rekey path on recovery completion and the live attestation submission from `ApproveRecoveryPage`. Both are tracked as active workstreams. Phase 2 can begin in parallel; G6 closure is ~3-4h of net additional COB effort.

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

### Gaps ⚠

**Gap G6-A: SQLCipher rekey path stubbed**
- `AnchorRecoveryCompletionHandler.HandleAsync` logs the `RecoveryCompleted` event but does not call `IEncryptedStore.RotateKeyAsync` — the call is explicitly stubbed pending `IEncryptedStore.RotateKeyAsync` (a `foundation-localfirst` api-change, not yet surfaced in the public contract).
- Effort to close: ~1-2h once `IEncryptedStore.RotateKeyAsync` is added to `foundation-localfirst`. Filed as a follow-on to W#63 (no separate workstream yet — should be authored as part of the `foundation-localfirst` api-change workstream).
- **Impact:** Recovery completion does NOT rekey SQLCipher. The state machine correctly records completion and the UI reflects it, but the cryptographic key rotation that ADR 0046 requires does not execute. This means G6 does not satisfy the "new device gets access to existing data" invariant.

**Gap G6-B: `ApproveRecoveryPage` is a placeholder**
- `ApproveRecoveryPage.razor` renders the pending request but its "Approve" button does not call `IRecoveryCoordinator.SubmitAttestationAsync` with a real `TrusteeAttestation`. It shows a "Coming soon" banner pending a kernel-security session-signer accessor API.
- **Tracked as:** W#66 (`anchor-approve-recovery-page-live-attestation`), gated on W#65 (`ISessionSignerAccessor` + `IBoundEd25519Signer`).
- W#65 status: `ready-to-build` (hand-off at `icm/_state/handoffs/kernel-security-session-signer-accessor-stage06-handoff.md`).
- W#66 status: `blocked` on W#65.
- **Impact:** Trustees cannot submit live attestations from Anchor. The recovery flow cannot reach quorum on a real device. G6's multi-sig social recovery is functionally incomplete until W#65 + W#66 land.

**Verdict: PARTIAL** — infrastructure is built and wired; two surfaces (rekey + attestation) are explicitly deferred as tracked workstreams.

---

## Gaps summary

| Gap ID | Description | Effort | Workstream |
|---|---|---|---|
| G6-A | SQLCipher rekey on RecoveryCompleted stubbed | ~1-2h | Follow-on to foundation-localfirst api-change; unowned |
| G6-B | ApproveRecoveryPage placeholder (no live attestation) | ~1.5-2h | W#66 (blocked on W#65 ~2-3h) |
| G3-H | Rate limiting in Bridge Program.cs missing | ~2-4h | Phase 2 hardening; unowned |
| G3-H | Security response headers (CSP, X-Frame-Options) missing | ~1-2h | Phase 2 hardening; unowned |
| G4-T | No WAN relay integration test (Anchor→Bridge relay→Anchor) | ~3-4h | Phase 2 test-expansion; unowned |

**Total effort to fully close G6:** ~4-5h (W#65 ~2-3h + W#66 ~1.5-2h). G6-A (rekey) is blocked on a foundation-localfirst api-change workstream that hasn't been filed yet.

---

## Next steps

1. **COB:** build W#65 (`ISessionSignerAccessor` + `IBoundEd25519Signer`) — `ready-to-build`
2. **COB:** after W#65 lands, build W#66 (`ApproveRecoveryPage` live attestation) — then unblocked
3. **XO:** file a workstream for G6-A (foundation-localfirst `IEncryptedStore.RotateKeyAsync` api-change) — currently unowned
4. **Phase 2 workstreams (W#60 P3+, W#64, etc.) can begin now** — G1–G5 are fully green; G6's gaps are tracked and do not block Phase 2
5. **CO:** ADR decisions needed to unblock Phase 2 branches:
   - ADR 0086 Proposed→Accepted (unblocks W#60 P3 Tauri offline shell)
   - W#64 A vs B design decision (ERPNext company↔team context binding)
   - ADR 0055 Proposed→Accepted (unblocks W#26 Property Receipts)
