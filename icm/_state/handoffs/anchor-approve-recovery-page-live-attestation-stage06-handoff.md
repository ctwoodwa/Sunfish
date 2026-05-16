# W#66 — ApproveRecoveryPage live attestation submission

**Owner:** sunfish-PM
**Workstream:** W#66 (`icm/_state/workstreams/W66-anchor-approve-recovery-page-live-attestation.md`)
**Gate:** W#65 (`ISessionSignerAccessor` + `IBoundEd25519Signer`) merged to main
**Estimated effort:** ~1.5-2h / 1 PR
**Unblocks:** G6 attestation flow — `ApproveRecoveryPage` becomes functional for trustee approval

---

## Context

`ApproveRecoveryPage.razor` (shipped in W#63 P1, PR #866) renders the pending recovery request but cannot submit a signed attestation — the page is a placeholder with a "Coming soon" banner. The gap was the absence of a DI-injectable session-bound signer (filed as W#65).

W#65 delivers `ISessionSignerAccessor.GetCurrentAsync()` → `IBoundEd25519Signer`, which:
- Holds the team's Ed25519 identity private key internally
- Exposes `SignAsync(ReadOnlyMemory<byte> data)` → `byte[]`
- Exposes `PublicKey` → `ReadOnlyMemory<byte>` (the 32-byte public key)

This workstream wires `ISessionSignerAccessor` into `ApproveRecoveryPage` and replaces the "Coming soon" placeholder with a live signing flow.

---

## Key types

| Type | Namespace | Purpose |
|---|---|---|
| `IBoundEd25519Signer` | `Sunfish.Kernel.Security.Session` | Signs with the held private key; never exposes it |
| `ISessionSignerAccessor` | `Sunfish.Kernel.Security.Session` | Resolves the session-bound signer via `GetCurrentAsync()` |
| `IActiveTeamAccessor` | `Sunfish.Kernel.Runtime.Teams` | Provides `Active.TeamId` — used as the `TrusteeNodeId` |
| `TrusteeAttestation` | `Sunfish.Foundation.Recovery` | The signed attestation record; submit via `IRecoveryCoordinator.SubmitAttestationAsync` |
| `TrusteeAttestation.HashOf(request)` | static | SHA-256 of the recovery request's canonical bytes |
| `TrusteeAttestation.CanonicalBytesForSigning(nodeId, hash, at)` | static | Domain-separated byte sequence the trustee must sign |

**Important:** `TrusteeAttestation.Create()` requires raw private key bytes — it CANNOT be used with `IBoundEd25519Signer`. Use the two public static helpers + the record constructor directly (shown below).

---

## PR — ApproveRecoveryPage live signing (1 PR, ~1.5-2h)

### Modified file: `accelerators/anchor/Components/Pages/Recovery/ApproveRecoveryPage.razor`

Full replacement of the placeholder. Key changes:

**Directive section — add `@using` + `@inject`:**

```razor
@page "/recovery/approve"
@using Sunfish.Foundation.Recovery
@using Sunfish.Kernel.Security.Session
@using Sunfish.Kernel.Runtime.Teams
@inject IRecoveryCoordinator Recovery
@inject ISessionSignerAccessor SessionSignerAccessor
@inject IActiveTeamAccessor ActiveTeam
@inject NavigationManager Nav
```

**Approve button handler (`@code` section):**

```csharp
private bool _approving;
private string? _errorMessage;
private RecoveryStatus? _status;

protected override async Task OnInitializedAsync()
{
    try { _status = await Recovery.GetStatusAsync(default); }
    catch { /* best-effort */ }
}

private async Task ApproveAsync()
{
    _errorMessage = null;
    _approving = true;
    try
    {
        var status = _status ?? await Recovery.GetStatusAsync(default);
        var pendingRequest = status.PendingRequest;
        if (pendingRequest is null)
        {
            _errorMessage = "No pending recovery request to attest.";
            return;
        }

        var team = ActiveTeam.Active
            ?? throw new InvalidOperationException("No active team.");
        var signer = await SessionSignerAccessor.GetCurrentAsync(default);
        var trusteeNodeId = team.TeamId.ToString();
        var attestedAt = DateTimeOffset.UtcNow;

        var requestHash = TrusteeAttestation.HashOf(pendingRequest);
        var canonical = TrusteeAttestation.CanonicalBytesForSigning(
            trusteeNodeId, requestHash, attestedAt);
        var signature = await signer.SignAsync(canonical, default);

        var attestation = new TrusteeAttestation(
            TrusteeNodeId: trusteeNodeId,
            TrusteePublicKey: signer.PublicKey.ToArray(),
            RecoveryRequestHash: requestHash,
            AttestedAt: attestedAt,
            Signature: signature);

        await Recovery.SubmitAttestationAsync(attestation, default);
        Nav.NavigateTo("/recovery/status");
    }
    catch (Exception ex)
    {
        _errorMessage = ex.Message;
    }
    finally
    {
        _approving = false;
    }
}
```

**Markup — replace "Coming soon" section with a functional approve/dispute layout:**

```razor
<div class="recovery-approve">
    <header>
        <a class="recovery-approve__link" href="recovery/status">← Back to recovery status</a>
        <h1>Approve recovery</h1>
    </header>

    @if (_errorMessage is not null)
    {
        <div class="recovery-approve__error">@_errorMessage</div>
    }

    @if (_status?.PendingRequest is { } req)
    {
        <section class="recovery-approve__request">
            <h2>Pending request</h2>
            <dl>
                <dt>Requesting node</dt>
                <dd><code>@req.RequestingNodeId</code></dd>
                <dt>Requested at</dt>
                <dd>@req.RequestedAt.ToLocalTime()</dd>
                <dt>Quorum progress</dt>
                <dd>@_status.AttestationsReceived of @_status.QuorumThreshold attestations received</dd>
            </dl>
            <p class="recovery-approve__prompt">
                Recognise this device as belonging to the account owner?
            </p>
            <div class="recovery-approve__actions">
                <button class="recovery-approve__btn recovery-approve__btn--approve"
                        disabled="@_approving"
                        @onclick="ApproveAsync">
                    @(_approving ? "Approving…" : "Approve recovery")
                </button>
            </div>
        </section>
    }
    else
    {
        <section class="recovery-approve__empty">
            <p>No pending recovery request. Nothing to approve.</p>
        </section>
    }
</div>
```

---

### Tests — `accelerators/anchor/tests/ApproveRecoveryPageTests.cs`

3 xUnit tests using NSubstitute for all DI dependencies. Do not test Blazor rendering directly; test the `ApproveAsync` logic via a thin wrapper or extract the logic into a testable `ApproveRecoveryViewModel`.

**Preferred approach:** Extract signing logic into a static `BuildAttestation(RecoveryRequest, string trusteeNodeId, IBoundEd25519Signer, DateTimeOffset)` helper method on the page's `@code` section (or a companion `ApproveRecoveryHelpers` static class). This makes the tests fast and avoids Blazor test infrastructure.

```csharp
// ApproveRecoveryHelpers (or inner static in @code)
internal static async ValueTask<TrusteeAttestation> BuildAttestationAsync(
    RecoveryRequest request,
    string trusteeNodeId,
    IBoundEd25519Signer signer,
    DateTimeOffset attestedAt,
    CancellationToken ct = default)
{
    var requestHash = TrusteeAttestation.HashOf(request);
    var canonical = TrusteeAttestation.CanonicalBytesForSigning(trusteeNodeId, requestHash, attestedAt);
    var signature = await signer.SignAsync(canonical, ct);
    return new TrusteeAttestation(
        TrusteeNodeId: trusteeNodeId,
        TrusteePublicKey: signer.PublicKey.ToArray(),
        RecoveryRequestHash: requestHash,
        AttestedAt: attestedAt,
        Signature: signature);
}
```

**Test 1 — `BuildAttestation_ProducesCorrectlySignedAttestation`**

Given a mock `IBoundEd25519Signer` that returns a fixed 64-byte signature and has a fixed 32-byte public key, verify:
- `attestation.TrusteeNodeId` == `trusteeNodeId`
- `attestation.TrusteePublicKey` == `signer.PublicKey.ToArray()`
- `attestation.Signature` == the mock's returned bytes
- `attestation.RecoveryRequestHash` == `TrusteeAttestation.HashOf(request)`

**Test 2 — `BuildAttestation_CanonicalBytes_MatchVerifyPath`**

Use a real `DefaultBoundEd25519Signer`-compatible signer (or the actual `IEd25519Signer` from `NaclEd25519Signer` if available in test project) to produce an attestation, then call `attestation.Verify(request, signer)` → `true`.

**Test 3 — `BuildAttestation_DifferentRequestHash_FailsVerify`**

Produce an attestation for request A, then call `attestation.Verify(differentRequest, signer)` → `false` (request hash mismatch).

---

## Acceptance criteria

- [ ] `ApproveRecoveryPage.razor` no longer shows the "Coming soon" placeholder
- [ ] "Approve recovery" button calls `ISessionSignerAccessor.GetCurrentAsync()` and `IRecoveryCoordinator.SubmitAttestationAsync()` with a correctly constructed `TrusteeAttestation`
- [ ] After successful submission, page navigates to `/recovery/status`
- [ ] Error message displayed when `GetCurrentAsync` or `SubmitAttestationAsync` throws
- [ ] 3 tests pass; no regressions in W#63 recovery suite
- [ ] Anchor builds clean (`net11.0-maccatalyst`)

---

## Halt conditions

- `ISessionSignerAccessor` not registered in Anchor DI (W#65 not yet wired) → stop; verify W#65 PR is merged and `AddKernelSecurity()` is called in `MauiProgram.cs`
- `ActiveTeam.Active` is null when the page loads → show an error banner ("No team active — cannot sign attestation"); do NOT navigate away
- `TrusteeAttestation.CanonicalBytesForSigning` or `HashOf` signature changes (breaking) → halt; check foundation-recovery version

---

## Note on `TrusteeAttestation.CreateAsync` overload

If adding an `IBoundEd25519Signer`-accepting factory method on `TrusteeAttestation` is preferred over in-page helpers, that overload belongs in `foundation-recovery` (which already depends on `kernel-security.Crypto`). Adding it in this PR or in W#65 is acceptable — either way `foundation-recovery.csproj` needs a reference to the `kernel-security` assembly that includes `IBoundEd25519Signer` (in `Session/` namespace). Discuss with XO if adding the overload in W#65 is preferred before W#66 begins.
