---
sort_order: 75
number: 66
slug: anchor-approve-recovery-page-live-attestation
title: "W#66 — Anchor ApproveRecoveryPage live attestation submission"
status: "built"
status_cell: "`built` — Single PR replaces the W#63 P1 placeholder with a live signing flow. New `RecoveryAttestationSubmitter` helper composes W#65 `ISessionSignerAccessor` + `INodeIdentityProvider` + `IRecoveryCoordinator` + `TimeProvider` and exposes one `SubmitAsync(request)` call. 5/5 NSubstitute unit tests pass."
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/w63-approve-recovery-session-signer-stage06-handoff.md` (merged via #869) + W#65 PR #868"
---

## Notes

W#63 Phase 1 (PR #866) shipped `ApproveRecoveryPage.razor` as a placeholder citing the missing kernel-security signer accessor. W#65 (PR #868) added `ISessionSignerAccessor` + `IBoundEd25519Signer`. W#66 consumes that surface and replaces the placeholder with a live trustee-attestation flow.

**Architecture: helper-service extraction.** Per `accelerators/anchor/tests/tests.csproj`'s established MAUI-free convention (see `CrewChatPageTests.cs`), Razor pages can't be unit-tested with bUnit without dragging in the MAUI workload. The hand-off prescribed bUnit tests in a nonexistent `tests/Components/Pages/Recovery/` directory; instead, the signing/submission logic is extracted into a new MAUI-free helper class `Sunfish.Anchor.Services.RecoveryAttestationSubmitter` that the page delegates to via DI. The page itself stays a thin wrapper around `Submitter.SubmitAsync(req)`; the helper is unit-tested directly with NSubstitute.

**Hand-off corrections applied during the build:**

| Hand-off statement | Actual API |
|---|---|
| `@using Sunfish.Kernel.Security.Sessions` | `Sunfish.Kernel.Security.Session` (singular) |
| `TrusteeAttestation(nodeId, hash, signature)` 3 args | 5 args: `TrusteeNodeId, TrusteePublicKey, RecoveryRequestHash, AttestedAt, Signature` |
| `GetCurrentAsync` returns null when no team | Throws `InvalidOperationException` (per W#65 source XML doc) |
| `e.EventType` on `RecoveryEvent` | `e.Type` (record positional param) |
| Outcome property `outcome.Events` returns `IReadOnlyList<RecoveryEvent>` (per hand-off) | Confirmed: `RecoveryAttestationOutcome(bool Accepted, IReadOnlyList<RecoveryEvent> Events)` |

**Signing key choice.** W#65 binds the signer to the per-team subkey derived via ADR 0032 `TeamSubkeyDerivation.DeriveTeamKeypair(rootSeed, activeTeamId)`. The trustee's NodeId in the attestation comes from `INodeIdentityProvider.Current.NodeId` (their device identity); the trustee's public key in the attestation is the W#65 signer's `PublicKey` (the team-subkey-derived public key). The owner-side `TrusteeSetupPage` already accepts both fields as free-form (NodeId + base64url public key), so this pairing is consistent.

**Files:**

- `accelerators/anchor/Services/RecoveryAttestationSubmitter.cs` — new (helper + `AttestationSubmissionResult` record)
- `accelerators/anchor/Components/Pages/Recovery/ApproveRecoveryPage.razor` — replaced placeholder block with live form
- `accelerators/anchor/MauiProgram.cs` — registers `TimeProvider.System` + `RecoveryAttestationSubmitter` (Transient)
- `accelerators/anchor/tests/tests.csproj` — adds `<Compile Include="..\Services\RecoveryAttestationSubmitter.cs" />` to keep MAUI-free posture
- `accelerators/anchor/tests/RecoveryAttestationSubmitterTests.cs` — new (5 tests, NSubstitute + inline FakeTimeProvider)

**Tests:** `dotnet test --filter "FullyQualifiedName~RecoveryAttestationSubmitter"` → 5/5 pass.

1. Populates attestation from signer + node identity
2. Signs canonical bytes (not raw hash) — domain-separated by `sunfish-trustee-attestation-v1` prefix
3. Quorum reached when `GracePeriodStarted` event fires; `GracePeriodStartedAt` populated from `RecoveryEvent.OccurredAt`
4. `Accepted = false` from coordinator → result `Accepted = false`, no quorum
5. `ISessionSignerAccessor` throws `InvalidOperationException` → propagates (page surfaces as user error)

**No bUnit test of the Razor page** — see helper-service rationale above. Manual smoke remains the closing-the-loop step on the Mac↔Mac demo.

**Closes the W#63 P1 placeholder loop.** Anchor recovery UX is now end-to-end: TrusteeSetup → InitiateRecovery → PaperKey → ApproveRecovery (live) → RecoveryStatus.
