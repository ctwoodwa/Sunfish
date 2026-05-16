---
sort_order: 75
number: 66
slug: anchor-approve-recovery-page-live-attestation
title: "W#66 — Anchor ApproveRecoveryPage live attestation submission"
status: "built"
status_cell: "`built` — PR #870 merged 2026-05-16; `RecoveryAttestationSubmitter.cs` + live `ApproveRecoveryPage.razor` + 5 tests; security council APPROVED; G6-B gap CLOSED"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/w63-approve-recovery-session-signer-stage06-handoff.md` (PR #869) + `accelerators/anchor/Components/Pages/Recovery/ApproveRecoveryPage.razor` + `packages/foundation-recovery/TrusteeAttestation.cs`"
---

## Notes

`ApproveRecoveryPage.razor` (W#63 P1, PR #866) is a placeholder — renders the pending recovery request but cannot sign or submit attestations. The page's "Coming soon" banner cites a missing kernel-security session-signer API.

W#65 fills that gap: `ISessionSignerAccessor.GetCurrentAsync()` returns an `IBoundEd25519Signer` that signs with the team's identity Ed25519 key without exposing raw key bytes.

**This workstream replaces the placeholder with a live approve flow:** inject `ISessionSignerAccessor` + `IActiveTeamAccessor`, compute the `TrusteeAttestation` canonical bytes via `TrusteeAttestation.HashOf(request)` + `CanonicalBytesForSigning(nodeId, hash, attestedAt)`, sign via `IBoundEd25519Signer.SignAsync`, and call `IRecoveryCoordinator.SubmitAttestationAsync(attestation)`.

**Implementer note:** COB's hand-off (PR #869) uses `@using Sunfish.Kernel.Security.Sessions` — correct namespace is `Sunfish.Kernel.Security.Session` (singular). Also verify `TrusteeAttestation` constructor requires 5 args: `TrusteeNodeId`, `TrusteePublicKey` (from `signer.PublicKey.ToArray()`), `RecoveryRequestHash`, `AttestedAt`, `Signature`.

**Scope:** 1 PR, ~1.5-2h. Single file change + tests.

**Gate:** W#65 merged (provides `Sunfish.Kernel.Security.Session.ISessionSignerAccessor`; PR #868).
