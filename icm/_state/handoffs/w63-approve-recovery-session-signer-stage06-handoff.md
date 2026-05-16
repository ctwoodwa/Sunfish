# Stage 06 Hand-off — W#63 follow-on: ApproveRecoveryPage live signing via `ISessionSignerAccessor`

**Workstream:** W#63 — Anchor Recovery Host (W#63 P1 + P2 + P3 already `built`; this is a P1.5 follow-on)
**Pipeline variant:** `sunfish-feature-change`
**Status gate:** PR #868 (W#65 — `ISessionSignerAccessor`) MERGED on main. **DO NOT START** until PR #868 lands.
**Predecessor:** XO ruling `xo-ruling-2026-05-16T04-30Z-w63-recovery-ux-and-polling.md` §(b) — promised this follow-on once kernel-security exposed a session-signer accessor.
**Estimated effort:** 1 PR / ~1–2h COB time

---

## Context

When the W#63 hand-off shipped, `ApproveRecoveryPage.razor` was placeholdered (XO ruling §(b)) because no session-signer accessor existed on `IKeyStore` / `IActiveSessionAccessor`. COB shipped PR #868 (`ISessionSignerAccessor`) to close that gap. **Once #868 lands**, the placeholder can be replaced with a real signing flow.

The full XO ruling §(b) (re-stated for the implementer):

> The trustee's Anchor session is signed in as the trustee identity. Read pending request via `IRecoveryCoordinator.GetStatusAsync()`, compute the request hash (same canonical scheme `RecoveryRequest.Create` uses internally), sign with the session signer, construct `TrusteeAttestation(nodeId, hash, signature)`, submit via `SubmitAttestationAsync`.
>
> Don't store the trustee's private key in the page's state. The signer is retrieved fresh per signing operation.

---

## Pre-build checklist

1. Verify PR #868 is merged on main and `ISessionSignerAccessor` is reachable from Anchor's DI graph: `grep -r "ISessionSignerAccessor" packages/kernel-security/ accelerators/anchor/`.
2. Read `packages/foundation-recovery/RecoveryRequest.cs` for the canonical hash scheme (likely SHA-256 over canonical CBOR / JSON of the request fields; mirror its `Create` factory).
3. Read existing `ApproveRecoveryPage.razor` (`accelerators/anchor/Components/Pages/Recovery/ApproveRecoveryPage.razor`) — it currently has the placeholder block with TODO referencing the XO ruling. Replace from the top of the `<section class="recovery-approve__placeholder">` to the closing `</section>`.

---

## Deliverables (1 PR)

### Page wiring

```razor
@page "/recovery/approve"
@using Sunfish.Foundation.Recovery
@using Sunfish.Kernel.Security.Sessions
@inject IRecoveryCoordinator Recovery
@inject ISessionSignerAccessor SessionSigner
@inject IJSRuntime Js
@inject ILogger<ApproveRecoveryPage> Logger
```

### Page state

```csharp
private RecoveryStatus? _status;
private TrusteeAttestationDraft? _draft;
private SubmitResult? _result;
private string? _errorMessage;
private bool _submitting;
```

Where `TrusteeAttestationDraft` is a small page-private record holding `(string TrusteeNodeId, byte[] RequestHash)` and `SubmitResult` is `(bool QuorumReached, DateTimeOffset GracePeriodEndsAt?)`. Avoid surfacing the request bytes directly to the page model — render fingerprint + summary only.

### Approval handler

```csharp
private async Task ApproveAsync()
{
    if (_status is null || _draft is null) return;
    _submitting = true;
    _errorMessage = null;
    try
    {
        var signer = await SessionSigner.GetCurrentAsync(default);
        if (signer is null)
        {
            _errorMessage = "No active session signer. Sign in to the trustee account first.";
            return;
        }

        var signature = await signer.SignAsync(_draft.RequestHash, default);
        var attestation = new TrusteeAttestation(
            trusteeNodeId: _draft.TrusteeNodeId,
            recoveryRequestHash: _draft.RequestHash,
            signature: signature);

        var outcome = await Recovery.SubmitAttestationAsync(attestation, default);
        if (!outcome.Accepted)
        {
            _errorMessage = "Attestation rejected — verify the trustee identity and the pending request hash.";
            return;
        }

        var quorumEvent = outcome.Events
            .FirstOrDefault(e => e.EventType == RecoveryEventType.GracePeriodStarted);
        _result = new SubmitResult(
            QuorumReached: quorumEvent is not null,
            GracePeriodEndsAt: quorumEvent?.OccurredAt.Add(TimeSpan.FromDays(7)));
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Approve recovery failed");
        _errorMessage = "Approval failed. See logs.";
    }
    finally
    {
        _submitting = false;
        StateHasChanged();
    }
}
```

### Page-load handler

`OnInitializedAsync`: fetch `_status = await Recovery.GetStatusAsync(default)`. If status indicates a pending request (`Status == RecoveryStatusKind.AwaitingAttestation` or similar — verify enum), populate `_draft` with the request hash + the *current session's trustee NodeId* from `SessionSigner.GetCurrentAsync()`. If no pending request, render the "no recovery in flight" state from the existing placeholder; skip the approve button.

### Markup

Replace the placeholder section with:

- Pending-request summary card (request ID, initiated-at timestamp, owner-of-record fingerprint, your-NodeId-as-trustee row)
- "Sign + submit attestation" button (disabled when `_submitting` or `_status is null`)
- Error banner (if `_errorMessage` non-null)
- Success card after submit: shows whether quorum reached + grace-period end date

Keep the placeholder's CSS class `.recovery-approve__placeholder` — rename to `.recovery-approve__pending` and reuse styles for layout consistency.

### Tests

`accelerators/anchor/tests/Components/Pages/Recovery/ApproveRecoveryPageTests.cs` (new file):

1. **No-pending-request state** — `IRecoveryCoordinator` returns a `RecoveryStatus` with no active request; page renders the "no recovery in flight" message and no approve button.
2. **Happy-path approval** — fake `ISessionSignerAccessor` returns a deterministic signer; fake `IRecoveryCoordinator` accepts the attestation and returns `Events = [AttestationReceived, GracePeriodStarted]`; page submits, shows success with grace-period end date.
3. **Attestation rejected** — coordinator returns `Accepted = false`; page renders the rejection banner.
4. **No signer** — `SessionSigner.GetCurrentAsync` returns null; page renders the "sign in first" error.
5. **Signing throws** — signer throws; page catches, logs, shows the generic error banner.

Use the bUnit test pattern already established in `accelerators/anchor/tests/Components/`.

---

## PASS gate

1. PR adds 4–6 LOC to `ApproveRecoveryPage.razor` markup, replaces the placeholder block, wires `ISessionSignerAccessor` injection, adds the 5 bUnit tests
2. `dotnet test --filter "FullyQualifiedName~ApproveRecoveryPageTests"` → 5 passed, 0 failed
3. Manual smoke (optional): with a fake/in-memory recovery coordinator + the W#65 in-memory session signer in dev mode, the approve button submits and the page advances to the success state

## HALT conditions

- `ISessionSignerAccessor.GetCurrentAsync` doesn't return an `IEd25519Signer` (returns a different abstraction): adapt or file a cob-question against W#65 owners. Don't invent a new signer abstraction in W#63.
- `RecoveryStatus` doesn't expose the request bytes / hash: file a cob-question against `foundation-recovery` to add a `GetPendingRequestAsync` or include the hash in `RecoveryStatus`. Don't compute the hash from a guess of internal field layout.
- `TrusteeAttestation` constructor signature differs from `(string, byte[], byte[])`: verify the actual constructor; the ruling assumed Ed25519 raw bytes but the wire-format may want base64url-encoded strings. Match what `SubmitAttestationAsync` validates internally.

## Workstream flip

This is a P1.5 follow-on, not a new workstream. After this PR merges:

- W#63 ledger row stays `built`
- Add a one-line note to `icm/_state/workstreams/W63-anchor-recovery-host.md`'s `notes` field: "P1.5 follow-on: ApproveRecoveryPage upgraded to use `ISessionSignerAccessor` (PR #<N>)."
- Re-render the ledger via `python3 tools/icm/render-ledger.py`

## References

- XO ruling: `coordination/_archive/xo-ruling-2026-05-16T04-30Z-w63-recovery-ux-and-polling.md` §(b)
- PR #868 (W#65 `ISessionSignerAccessor`) — must be merged before this hand-off starts
- ADR 0046 sub-pattern #48a (multi-sig recovery), #48f (signed audit)
- `packages/foundation-recovery/IRecoveryCoordinator.cs` — `SubmitAttestationAsync`, `RecoveryAttestationOutcome`, `RecoveryEventType.GracePeriodStarted`
- `packages/foundation-recovery/TrusteeAttestation.cs` — attestation type to construct
- Existing placeholder: `accelerators/anchor/Components/Pages/Recovery/ApproveRecoveryPage.razor`
