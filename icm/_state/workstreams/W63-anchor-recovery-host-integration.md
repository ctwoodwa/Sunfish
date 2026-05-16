---
sort_order: 72
number: 63
slug: anchor-recovery-host-integration
title: "**G6 Anchor Recovery Host Integration + Razor UI** (`sunfish-feature-change` pipeline) — ADR 0046 Phase 1 UX; closes MASTER-PLAN G-1 Phase 1 final items"
status: "built"
status_cell: "`built` — Phase 1 (PR #866) DI wiring + 5 ADR 0046 Razor pages (Status read-only; TrusteeSetup base64url paste form + fingerprint render; InitiateRecovery ephemeral-keypair-then-sign; PaperKey BIP-39 validate; ApproveRecovery placeholder pending kernel-security session-signer accessor). Phase 2 (this PR) `IRecoveryCompletionHandler` + `RecoveryGracePollingService` IHostedService polling per XO ruling 2026-05-16 §c — coordinator has no event subscription, host adapts via polling EvaluateGracePeriodAsync on a 60s cadence; 5/5 unit tests pass. SQLCipher rekey + audit emission stubbed pending IEncryptedStore.RotateKeyAsync + session-signer accessor (cob-question 2026-05-16T04-42Z filed)."
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/anchor-recovery-host-integration-stage06-handoff.md` + XO ruling 2026-05-16 (coordination/_archive/) + cob-question 2026-05-16T04-42Z (kernel-security session-signer accessor)"
---

## Notes

**G6 closes the last MASTER-PLAN G-1 Phase 1 items.** Per the W#15 + W#32 substrate, Foundation.Recovery shipped `IRecoveryCoordinator` + `RecoveryCoordinator` + state-store + paper-key surface. W#63 wires those into Anchor's MAUI shell.

**XO ruling 2026-05-16 amendments to hand-off:**

- **(a) Trustee public-key acquisition:** hex/base64url paste for Phase 1; trustee transmits the `{nodeId, publicKey}` pair out-of-band (Signal / encrypted email / in-person). QR-code pairing deferred to Phase 1.x.
- **(b) ApproveRecoveryPage signer retrieval:** kernel-security has no per-session signer accessor today. Page ships as placeholder; follow-up cob-question filed against kernel-security.
- **(c) Phase 2 event handling:** `IRecoveryCoordinator` has no `OnEventRaised`. Replaced with `RecoveryGracePollingService : IHostedService` polling `EvaluateGracePeriodAsync` on a 60s cadence + `IRecoveryCompletionHandler` seam.

**Surfaces shipped (Phase 1 + 2):**

- `accelerators/anchor/Components/Pages/Recovery/{RecoveryStatusPage,TrusteeSetupPage,InitiateRecoveryPage,PaperKeyPage,ApproveRecoveryPage}.razor`
- `accelerators/anchor/Services/IRecoveryCompletionHandler.cs` + `AnchorRecoveryCompletionHandler` (stubs rekey + audit pending follow-up surfaces)
- `accelerators/anchor/Services/RecoveryHostOptions.cs` (section `Anchor:Recovery`; default `GracePollIntervalSeconds=60`)
- `accelerators/anchor/Services/RecoveryGracePollingService.cs` (IHostedService; restart-safety startup poll + interval loop)
- `accelerators/anchor/Services/RecoveryHostExtensions.cs` (`AddAnchorRecoveryHost(IServiceCollection, IConfiguration?)`)
- `accelerators/anchor/MauiProgram.cs` adds DI for `IRecoveryStateStore` + `IRecoveryClock` + `IDisputerValidator` + `IRecoveryCoordinator` + the recovery-host pipeline.
- `accelerators/anchor/Components/Layout/NavMenu.razor` adds "Recovery" entry.

**Tests:** 5 unit tests in `accelerators/anchor/tests/RecoveryGracePollingServiceTests.cs`:
1. StartAsync dispatches `RecoveryCompleted` when the startup poll returns it
2. StartAsync does NOT dispatch on a non-`Completed` event (e.g., `GracePeriodStarted`)
3. StartAsync does NOT dispatch when the startup poll returns null
4. StopAsync allows clean shutdown; no stale dispatches after stop
5. Loop tick dispatches `RecoveryCompleted` after the first interval (verifies the loop body, not just the startup path)

**Deferred (post-W#63):**

- IEncryptedStore.RotateKeyAsync (foundation-localfirst api-change)
- Kernel-security per-session signer accessor (cob-question 2026-05-16T04-42Z)
- Typed `RecoveryRekey` audit event + payload schema
- QR-code pairing for trustee invitation (Phase 1.x)
- Multi-device owner `IDisputerValidator` (Phase 1.x per IDisputerValidator.cs)
- ApproveRecoveryPage upgrade from placeholder to live signer-using attestation flow (depends on cob-question 2026-05-16T04-42Z resolution)

**MASTER-PLAN G-1 Phase 1 status:** G6 closed; G7 conformance baseline scan now unblocked.
