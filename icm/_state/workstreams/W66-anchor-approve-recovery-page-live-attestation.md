---
sort_order: 75
number: 66
slug: anchor-approve-recovery-page-live-attestation
title: "W#66 — Anchor ApproveRecoveryPage live attestation submission"
status: "blocked"
status_cell: "`blocked` — gated on **W#65 built** (`ISessionSignerAccessor` + `IBoundEd25519Signer`); hand-off pre-authored 2026-05-16 at `icm/_state/handoffs/anchor-approve-recovery-page-live-attestation-stage06-handoff.md`; immediately buildable once gate clears"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/anchor-approve-recovery-page-live-attestation-stage06-handoff.md` + `accelerators/anchor/Components/Pages/Recovery/ApproveRecoveryPage.razor` + `packages/foundation-recovery/TrusteeAttestation.cs`"
---

## Notes

`ApproveRecoveryPage.razor` (W#63 P1, PR #866) is a placeholder — renders the pending recovery request but cannot sign or submit attestations. The page's "Coming soon" banner cites a missing kernel-security session-signer API.

W#65 fills that gap: `ISessionSignerAccessor.GetCurrentAsync()` returns an `IBoundEd25519Signer` that signs with the team's identity Ed25519 key without exposing raw key bytes.

**This workstream replaces the placeholder with a live approve flow:** inject `ISessionSignerAccessor` + `IActiveTeamAccessor`, compute the `TrusteeAttestation` canonical bytes, sign via the bound signer, and call `IRecoveryCoordinator.SubmitAttestationAsync(attestation)`.

**Scope:** 1 PR, ~1.5-2h. Single file change + 3 tests.

**Gate:** W#65 merged (provides `Sunfish.Kernel.Security.Session.ISessionSignerAccessor`).
