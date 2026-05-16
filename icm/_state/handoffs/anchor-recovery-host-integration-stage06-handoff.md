# Stage 06 Hand-off — G6: Anchor Recovery Host Integration + Razor UI

**From:** research session (XO)
**To:** sunfish-PM (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build`
**Gate:** None — immediately buildable. `Foundation.Recovery` (W#15 + W#32) built; `IRecoveryCoordinator` surface stable.
**Estimated effort:** ~7-10h / 3 PRs
**Pipeline:** `sunfish-feature-change`
**Unblocks:** G7 conformance baseline scan (MASTER-PLAN Phase 1 completion)

---

## Context

`Foundation.Recovery` ships `IRecoveryCoordinator`, `RecoveryCoordinator`, `InMemoryRecoveryStateStore`, `PaperKeyDerivation`, and the full `RecoveryEvent` / `RecoveryEventType` surface (ADR 0046 sub-patterns 48a + 48c + 48e + 48f). The substrate is stable and consumed in tests.

**What does not yet exist:**
- Any Anchor Razor pages for recovery (TrusteeSetup, InitiateRecovery, AttestationCollection, PaperKey, RecoveryStatus)
- DI registration of `IRecoveryCoordinator` in Anchor's `MauiProgram.cs`
- The `RecoveryCompleted` event handler that triggers SqlCipher rekey + audit emission

This hand-off builds all three layers. It does NOT implement kernel-audit persistent storage (deferred to the kernel-audit workstream) — an `InMemoryAuditTrail` stub is acceptable per the same pattern used in W#23 P4.

---

## Scope summary

3 PRs:

1. **DI registration + 5 Razor recovery pages** (scaffold with full UX structure; state flows wired to `IRecoveryCoordinator`)
2. **`RecoveryCompleted` → SqlCipher rekey + audit emission** (`RecoveryHostedService` + `InMemoryAuditTrail` registration)
3. **4 unit tests + ledger flip**

---

## Known file paths and surface

**Foundation.Recovery key types (all in `packages/foundation-recovery/`):**

| Type | Purpose |
|---|---|
| `IRecoveryCoordinator` | Orchestrates the full ADR 0046 Phase 1 state machine |
| `RecoveryCoordinator` | Concrete implementation; depends on `IRecoveryStateStore` + `IRecoveryClock` + `IDisputerValidator` |
| `InMemoryRecoveryStateStore` | In-memory `IRecoveryStateStore` for Anchor (production: EFCore store deferred) |
| `SystemRecoveryClock` | Wall-clock `IRecoveryClock` |
| `FixedDisputerValidator` | Always-valid `IDisputerValidator` (placeholder until W#37 tenant security policy ships) |
| `RecoveryEventType` | Enum: `TrusteeDesignated`, `TrusteeRevoked`, `RecoveryInitiated`, `AttestationReceived`, `GracePeriodStarted`, `RecoveryDisputed`, `RecoveryCompleted`, `PaperKeyRecoveryUsed` |
| `PaperKeyDerivation` | BIP-39 word sequence generation + verification |

**Anchor existing pages** (`accelerators/anchor/Components/Pages/`):
`Home.razor`, `Onboarding.razor`, `TeamSwitcherPage.razor`, `CrewChatPage.razor`, `NotFound.razor`

**SqlCipher integration point** (`accelerators/anchor/MauiProgram.cs` line ~103):
```csharp
var sqlCipherKeyDerivation = new SqlCipherKeyDerivation();
// already registered for general key derivation; rekey path needed
```

---

## Phase 1 — DI registration + 5 Razor recovery pages (~4-5h / 1 PR)

### DI wiring in `accelerators/anchor/MauiProgram.cs`

```csharp
// Add after existing Foundation registrations
services.AddSingleton<IRecoveryStateStore, InMemoryRecoveryStateStore>();
services.AddSingleton<IRecoveryClock, SystemRecoveryClock>();
services.AddSingleton<IDisputerValidator, FixedDisputerValidator>();
services.AddSingleton<IRecoveryCoordinator, RecoveryCoordinator>();
```

No `AddSunfishRecovery()` extension exists yet — wire inline for now; extract to extension if it feels clean.

### New Razor pages to create

All under `accelerators/anchor/Components/Pages/Recovery/`:

**`TrusteeSetupPage.razor`** — designate 3 trustees
- Form: 3 trustee entries, each with `Name` (string) + `NodeId` (string, the trustee's pairing identity)
- On submit: call `IRecoveryCoordinator.DesignateTrusteeAsync(TrusteeDesignation)` for each
- Success: show confirmation with instruction to share pairing code out-of-band
- Route: `/recovery/trustee-setup`
- Nav: accessible from a "Security" item in Anchor's sidebar (add alongside existing nav)

**`InitiateRecoveryPage.razor`** — start recovery (new device)
- Explain: "You're on a new device. Request recovery from your trustees."
- Button: "Send Recovery Request" → `IRecoveryCoordinator.InitiateRecoveryAsync(RecoveryRequest { ... })`
- After send: route to `RecoveryStatusPage.razor`
- Route: `/recovery/initiate`

**`ApproveRecoveryPage.razor`** — trustee approves an attestation
- Show pending recovery request details (requester node ID + timestamp)
- Button: "Approve" → `IRecoveryCoordinator.SubmitAttestationAsync(TrusteeAttestation)`
- Button: "Dispute" → `IRecoveryCoordinator.DisputeAsync(RecoveryDispute)`
- Route: `/recovery/approve`

**`RecoveryStatusPage.razor`** — grace-period progress
- Show: current `RecoveryStatus` from `IRecoveryCoordinator.GetStatusAsync()`
- Show: `StatusKind` (Pending, GracePeriodActive, Disputed, Completed)
- If `GracePeriodActive`: show countdown timer + "Dispute (7 days remaining)" action
- If `Completed`: show "Recovery complete — database rekeyed" confirmation
- Route: `/recovery/status`
- Poll every 5s while `StatusKind != Completed`

**`PaperKeyPage.razor`** — BIP-39 paper-key recovery
- Textarea: 24-word BIP-39 mnemonic entry
- On submit: call `PaperKeyDerivation.VerifyAndRecoverAsync(words)` (or equivalent)
- On success: trigger rekey path (same as Phase 2 `RecoveryCompleted` handler)
- Route: `/recovery/paper-key`

### Nav link

Add "Security" section to the Anchor sidebar (wherever nav is defined — check `MainLayout.razor` or `NavMenu.razor`):
```html
<NavLink href="recovery/trustee-setup">Trustee Setup</NavLink>
```

### Acceptance criteria (Phase 1)

- [ ] All 5 pages reachable in Anchor without 404
- [ ] `TrusteeSetupPage.razor` calls `DesignateTrusteeAsync` on submit; form validation present
- [ ] `InitiateRecoveryPage.razor` calls `InitiateRecoveryAsync`; routes to status page
- [ ] `ApproveRecoveryPage.razor` calls `SubmitAttestationAsync` on Approve
- [ ] `RecoveryStatusPage.razor` reads and displays live `RecoveryStatus`
- [ ] `PaperKeyPage.razor` accepts 24-word BIP-39 input; calls verification
- [ ] Anchor builds without errors; existing Onboarding + CrewChat pages unaffected

**PR title:** `feat(anchor): G6 Phase 1 — recovery DI registration + 5 Razor pages (ADR 0046 Phase 1 UX)`

---

## Phase 2 — RecoveryCompleted → SqlCipher rekey + audit emission (~2-3h / 1 PR)

### New file: `accelerators/anchor/Services/RecoveryHostedService.cs`

```csharp
using Microsoft.Extensions.Hosting;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Security;

internal sealed class RecoveryHostedService : IHostedService
{
    private readonly IRecoveryCoordinator _recovery;
    private readonly SqlCipherKeyDerivation _keyDerivation;
    private readonly InMemoryAuditTrail _audit;

    public RecoveryHostedService(
        IRecoveryCoordinator recovery,
        SqlCipherKeyDerivation keyDerivation,
        InMemoryAuditTrail audit)
    {
        _recovery = recovery;
        _keyDerivation = keyDerivation;
        _audit = audit;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _recovery.OnEventRaised += HandleRecoveryEventAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _recovery.OnEventRaised -= HandleRecoveryEventAsync;
        return Task.CompletedTask;
    }

    private async void HandleRecoveryEventAsync(RecoveryEvent evt)
    {
        if (evt.Type != RecoveryEventType.RecoveryCompleted) return;

        // Derive new key material from the recovery seed
        // The exact key derivation call depends on what IRecoveryCoordinator
        // exposes post-completion — check RecoveryCompletedEvent detail dict
        // for the new seed bytes; derive → call RotateKeyAsync.
        await _keyDerivation.RotateKeyAsync(newKeyMaterial: null /* TODO: extract from evt.Detail */);

        // Emit to in-memory audit trail (kernel-audit substrate deferred)
        await _audit.AppendAsync(evt);
    }
}
```

**Note on `RotateKeyAsync` signature:** Check `packages/kernel-security/` for the actual `SqlCipherKeyDerivation.RotateKeyAsync` signature and the `RecoveryCoordinator`'s event hook convention — if `OnEventRaised` doesn't exist, check for `IAsyncEnumerable<RecoveryEvent>` observable pattern or subscribe via constructor injection.

### Register in `MauiProgram.cs`

```csharp
services.AddSingleton<InMemoryAuditTrail>();
services.AddHostedService<RecoveryHostedService>();
```

### Acceptance criteria (Phase 2)

- [ ] `RecoveryHostedService` starts without error when Anchor launches
- [ ] `RecoveryCompleted` event triggers `RotateKeyAsync` call (manual test: step through with debugger if needed)
- [ ] `InMemoryAuditTrail` registered and receives the `RecoveryCompleted` event
- [ ] No regression on existing Anchor Blazor pages

**PR title:** `feat(anchor): G6 Phase 2 — RecoveryCompleted → SqlCipher rekey + InMemoryAuditTrail wiring`

---

## Phase 3 — Unit tests + ledger flip (~1-2h / 1 PR)

### Test file: `accelerators/anchor/tests/RecoveryHostedServiceTests.cs`

4 tests:
1. `RecoveryHostedService_OnRecoveryCompleted_CallsRotateKeyAsync` — fake coordinator emits `RecoveryCompleted`; assert `RotateKeyAsync` called once
2. `RecoveryHostedService_OnOtherEvent_DoesNotCallRotateKeyAsync` — emit `AttestationReceived`; assert `RotateKeyAsync` not called
3. `RecoveryHostedService_OnRecoveryCompleted_EmitsToAuditTrail` — assert `InMemoryAuditTrail.Events` contains the `RecoveryCompleted` event
4. `RecoveryHostedService_StopAsync_UnsubscribesFromEvents` — stop service; emit event; assert no calls

### Ledger flip

Update `icm/_state/workstreams/W-G6-anchor-recovery-host-integration.md` (the source file for this workstream, to be created) → `status: built`.

**PR title:** `test(anchor): G6 Phase 3 — RecoveryHostedService tests + ledger flip`

---

## Halt conditions

- **`IRecoveryCoordinator.OnEventRaised` doesn't exist:** inspect Foundation.Recovery for the actual event subscription pattern (could be `IAsyncEnumerable<RecoveryEvent>`, `IObservable<RecoveryEvent>`, or delegate event); adapt Phase 2 accordingly; drop a `cob-question-*.md` if unclear
- **`RotateKeyAsync` signature mismatch:** read `packages/kernel-security/Recovery/SqlCipherKeyDerivation.cs` for the exact signature; the recovery coordinator's `Detail` dict should contain new key material under a documented key name from ADR 0046 §G6
- **BIP-39 word list not in scope:** `PaperKeyDerivation` should handle the word list internally; if it requires a word list file, check `packages/foundation-recovery/` for embedded resources

---

## Workstream registration

Registered as W#63 in `icm/_state/workstreams/W63-anchor-recovery-host-integration.md` — already created by XO. No action needed.

---

## Reference

- ADR 0046 (`docs/adrs/0046-key-loss-recovery-scheme-phase-1.md`) — recovery scheme Phase 1 specification
- ADR 0049 (`docs/adrs/0049-audit-trail-substrate.md`) — audit trail (kernel-audit deferred; InMemoryAuditTrail stub acceptable)
- W#15 hand-off (`icm/_state/handoffs/adr-0046-recovery-package-split.md`) — package split spec (built)
- W#32 (`icm/_state/workstreams/W32-foundation-recovery-field-encryption-substrate.md`) — field encryption (built)
- MASTER-PLAN G-1 Phase 1 done conditions — this hand-off closes the last two items
