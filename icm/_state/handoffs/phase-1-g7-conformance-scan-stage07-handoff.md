# W#7 — Phase 1 G7 Conformance Baseline Scan

**Owner:** research (XO session)
**Workstream:** W#7 (`icm/_state/workstreams/W07-phase-1-g7-conformance-scan.md`)
**Gate:** W#63 (G6 Anchor Recovery UI + RecoveryHostedService) must be `built` before starting this scan.
**Estimated effort:** ~4-6h (verification + report writing)
**Deliverable:** `icm/01_discovery/output/g7-conformance-baseline-2026-Q2.md`

---

## Purpose

G7 is a **verification pass**, not a build task. It confirms that G1-G6 (the Phase 1 foundational primitives) are actually working end-to-end, documents which acceptance criteria are met and which have gaps, and produces a frozen baseline report. The report is the gate-artifact for closing Phase 1 of the MASTER-PLAN G-1 goal.

---

## Prerequisites (verify before starting)

```bash
# W#63 must be built:
grep "status:" icm/_state/workstreams/W63-anchor-recovery-host-integration.md
# Expected: status: "built"

# All six substrate workstreams must be built:
# W#8 (RecoveryCoordinator), W#63 (Recovery UI + wiring),
# W#15 (foundation-recovery split), W#32 (encrypted-field substrate)
# G1/G2/G4/G5 each have dedicated files verified below.
```

---

## Scan methodology

For each G-item:

1. **Locate acceptance criterion** — from `icm/05_implementation-plan/output/business-mvp-phase-1-plan-2026-04-26.md`
2. **Verify the test exists** — grep for the named test; if renamed, find the functional equivalent
3. **Verify the test passes** — run the relevant test project; confirm CI is green
4. **Document any gap** — if test is missing, renamed, or skipped, document the gap and estimate to close it
5. **Verify wiring** — grep `MauiProgram.cs` / `Program.cs` for the DI registration proving the feature is live in production, not test-only

---

## G-items to scan

### G1 — Anchor GossipDaemon hosted service

**Paper property:** P2 (multi-device), P4 (collaboration)

**Files to verify:**
- `accelerators/anchor/Services/AnchorSyncHostedService.cs` — exists ✓
- `accelerators/anchor/MauiProgram.cs` — confirm `AddSunfishKernelSync()` + `AddHostedService<AnchorSyncHostedService>()` present
- `accelerators/anchor/tests/AnchorSyncHostedServiceTests.cs` — exists ✓

**Test to verify (or functional equivalent):**
`Anchor_StartsGossipDaemon_OnHostStartup` (or equivalent test asserting `IGossipDaemon.State == Started`
within 2s of host startup). If renamed, document the actual test name.

**Acceptance:** PASS when test exists, passes, and wiring is confirmed in `MauiProgram.cs`.

---

### G2 — Wave 2.5 DELTA_STREAM → ICrdtDocument application

**Paper property:** P3 (network optional), P4 (collaboration)

**Files to verify:**
- Grep `packages/kernel-sync/` + `packages/kernel-crdt/` for `DeltaStream` or `ApplyDelta` — find the round-trip path
- Find the two-node in-process sync test (if authored; may not exist yet)

**Test to verify:**
`TwoNode_DeltaStream_AppliesToReceiver_CRDT` or functional equivalent: 2 in-process Anchor-equivalent
contexts on `InMemorySyncDaemonTransport`; node A mutates a CRDT; round runs; node B's projection
matches.

**Note:** This test was specified in the plan but may not have been written. If missing, document as
a Gap (not a blocker for Phase 1 if the infrastructure is in place — the test just doesn't exist yet).

---

### G3 — Bridge posture decision for Phase 1 (SaaS + Relay vs Relay-only)

**Paper property:** P2, P3, P6

**This is a posture check, not a test.** Verify:
- `accelerators/bridge/Program.cs` or `BridgeOptions.cs` — what relay mode is configured?
- Document the actual posture: Relay-only (ciphertext-only) vs SaaS + Relay
- Confirm the posture matches the decision recorded in the Phase 1 plan (§G3)

**Acceptance:** PASS when posture is documented and matches intent.

---

### G4 — Anchor↔Bridge WAN connection (ciphertext-only relay)

**Paper property:** P3, P6

**Files to verify:**
- `packages/kernel-sync/Discovery/ManagedRelayPeerDiscovery.cs` — exists ✓
- `packages/kernel-sync/Discovery/ManagedRelayPeerDiscoveryOptions.cs` — exists ✓
- `packages/kernel-sync/tests/ManagedRelayPeerDiscoveryTests.cs` — exists ✓
- Confirm Anchor's `MauiProgram.cs` wires `AddManagedRelayPeerDiscovery(...)` (if it does)
- Confirm relay ciphertext-only invariant: search for where the relay reads the inner payload — it should not (only CBOR envelope headers should be readable by relay)

**Test to verify:**
`ManagedRelayPeerDiscoveryTests` pass. If a two-node WAN integration test exists (Anchor A → Bridge relay → Anchor B), document its name and status.

**Acceptance:** PASS when discovery is wired and relay invariant is confirmed by code inspection.

---

### G5 — Anchor backup orchestration

**Paper property:** P5 (long-now), P7 (ownership)

**Files to verify:**
- `accelerators/anchor/Services/AnchorBackupService.cs` — exists ✓
- `accelerators/anchor/Services/BackupManifest.cs` — exists ✓
- `accelerators/anchor/tests/AnchorBackupServiceTests.cs` — exists ✓

**Test to verify:**
`ExportImport_round_trip_preserves_50_text_operations` (confirmed exists) covers the G5 round-trip
criterion. Verify it passes in CI.

**Acceptance:** PASS when test passes and `AnchorBackupService` is reachable from a UI path in Anchor
(a Backup page or equivalent).

---

### G6 — ADR 0046 key-loss recovery flow

**Paper property:** P7 (ownership) — non-negotiable per ADR 0046

**Files to verify (post-W#63):**
- `accelerators/anchor/Services/RecoveryHostedService.cs` — must exist after W#63 ships
- `accelerators/anchor/Components/Pages/Recovery/TrusteeSetupPage.razor` — must exist
- `accelerators/anchor/Components/Pages/Recovery/InitiateRecoveryPage.razor` — must exist
- `accelerators/anchor/Components/Pages/Recovery/ApproveRecoveryPage.razor` — must exist
- `accelerators/anchor/Components/Pages/Recovery/RecoveryStatusPage.razor` — must exist
- `accelerators/anchor/Components/Pages/Recovery/PaperKeyPage.razor` — must exist
- `packages/kernel-security/Recovery/RecoveryCoordinator.cs` — exists (W#8 shipped) ✓
- `packages/foundation-localfirst/Encryption/SqlCipherKeyDerivation.cs` — verify `RotateKeyAsync` (W#8 shipped) ✓

**Test to verify:**
`Recovery_3of5Trustees_GracePeriod_KeyReissue` (specified in plan) or functional equivalent from W#63
Phase 3 tests. Verify it passes.

**Additional verification:**
- `RecoveryHostedService` subscribes to `RecoveryCoordinator` events → calls `RotateKeyAsync` on `RecoveryCompleted`
- Confirm wiring in `MauiProgram.cs`: `AddSingleton<IRecoveryCoordinator, RecoveryCoordinator>()` + `AddHostedService<RecoveryHostedService>()`

**Acceptance:** PASS when all 5 Razor pages exist, `RecoveryHostedService` is wired, and the conformance test passes.

---

## Report format

Output to `icm/01_discovery/output/g7-conformance-baseline-2026-Q2.md`:

```markdown
# G7 Conformance Baseline Scan — 2026-Q2
**Date:** YYYY-MM-DD
**Scanner:** XO research session
**Sunfish commit:** (git sha at time of scan)

## Summary

| G-item | Description | Status | Gap? |
|---|---|---|---|
| G1 | GossipDaemon hosted service | PASS / PARTIAL / FAIL | — or gap description |
| G2 | DELTA_STREAM → ICrdtDocument | PASS / PARTIAL / FAIL | — |
| G3 | Bridge posture | PASS / PARTIAL / FAIL | — |
| G4 | Anchor↔Bridge WAN relay | PASS / PARTIAL / FAIL | — |
| G5 | Backup orchestration | PASS / PARTIAL / FAIL | — |
| G6 | Recovery flow (W#8 + W#63) | PASS / PARTIAL / FAIL | — |

## Phase 1 verdict

[PASSED / PARTIAL] — [1-2 sentence summary of what's done and what gaps remain]

## Gaps (if any)

[For each gap: what's missing, estimated effort to close, workstream or PR to track it]

## Next steps

[G7 closes Phase 1 or lists the specific PRs needed before Phase 1 can be declared complete]
```

---

## After the scan

1. Commit the report to `icm/01_discovery/output/g7-conformance-baseline-2026-Q2.md`
2. Update `icm/_state/MASTER-PLAN.md` — Phase 1 section → reflect G7 scan complete + verdict
3. Flip W#7 workstream source to `built` + regenerate ledger
4. If Phase 1 is PASSED: announce to CO; Phase 2 workstreams (W#60 P3+ etc.) are the focus
5. If PARTIAL: file gap workstreams as `ready-to-build` for remaining items; Phase 2 can still begin in parallel

---

## Halt conditions

- Any G-item test **fails** and the failure is not a test-harness issue → stop; file a `cob-question-*` beacon pointing at the failing test + the hand-off for the relevant substrate workstream
- W#63 is not yet `built` → do not start (conformance scan of an incomplete G6 is meaningless)
- CI is broken for unrelated reasons → wait for CI green before counting any test as PASS
