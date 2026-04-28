# Phase 1 Inventory — `packages/kernel-security/Recovery/` Type Classification

**Hand-off:** [`adr-0046-recovery-package-split.md`](./adr-0046-recovery-package-split.md) — Phase 1 deliverable
**From:** sunfish-PM session
**To:** research session (Phase 1 PASS gate review)
**Created:** 2026-04-28
**Source ref:** commit `cb3fe60` on `origin/main`

---

## Executive summary

Walked every type in `packages/kernel-security/Recovery/` and classified each per the hand-off heuristic. **Result: all 20 types in 16 source files plus the embedded BIP-39 wordlist resource and all 4 corresponding test files classify as `foundation-orchestration`. No types classify as `kernel-substrate`. No types are `ambiguous`.**

The folder structure already implements the architectural split the hand-off describes: kernel-tier crypto primitives live in `packages/kernel-security/Crypto/` (Ed25519 signers, X25519 key agreement, key-derivation primitives) and `packages/kernel-security/Keys/` (SqlCipher key derivation, keystore root-seed provider, rekey orchestration). The `Recovery/` subfolder uses those primitives but never extends or implements them — it's pure orchestration over the kernel substrate. `RecoveryCoordinator.cs` self-describes as "Pure orchestration — all I/O is delegated to `IRecoveryStateStore`; all time-of-day reads go through `IRecoveryClock`; signature operations via `IEd25519Signer`; dispute authorization via `IDisputerValidator`."

**Implication for Phase 3 (research-session decision required):** the entire `packages/kernel-security/Recovery/` folder moves to the new `packages/foundation-recovery/`, plus the embedded BIP-39 wordlist resource and the 4 test files under `packages/kernel-security/tests/Recovery/`. After the move, the `Recovery/` subfolder under kernel-security ceases to exist; the kernel-security package no longer references `IEd25519Signer` for orchestration purposes (only the substrate primitives keep it).

---

## Classification table

| # | File | Type | Kind | Classification | Rationale |
|---|---|---|---|---|---|
| 1 | `FixedDisputerValidator.cs` | `FixedDisputerValidator` | sealed class | foundation-orchestration | Implements `IDisputerValidator` over a fixed set of authorized public keys using `CryptographicOperations.FixedTimeEquals` (BCL primitive). Pure orchestration logic; does not extend any crypto primitive. |
| 2 | `InMemoryRecoveryStateStore.cs` | `InMemoryRecoveryStateStore` | sealed class | foundation-orchestration | In-memory implementation of `IRecoveryStateStore`. Pure storage orchestration; no crypto. |
| 3 | `IRecoveryClock.cs` | `IRecoveryClock` | interface | foundation-orchestration | Clock abstraction for the grace-period timer. Time-of-day seam; not a crypto primitive. |
| 4 | `IRecoveryCoordinator.cs` | `IRecoveryCoordinator` | interface | foundation-orchestration | The coordinator interface itself — drives the multi-sig recovery state machine (sub-patterns #48a/#48e/#48f). Hand-off explicitly names this as foundation. |
| 5 | `IRecoveryCoordinator.cs` | `RecoveryAttestationOutcome` | sealed record | foundation-orchestration | Outcome envelope for `SubmitAttestationAsync` — `(Accepted, Events)`. Domain return type. |
| 6 | `IRecoveryCoordinator.cs` | `IDisputerValidator` | interface | foundation-orchestration | Validator abstraction over disputer public keys. Implementations may use crypto primitives but the interface itself is orchestration. |
| 7 | `IRecoveryStateStore.cs` | `IRecoveryStateStore` | interface | foundation-orchestration | Persistence abstraction for `RecoveryCoordinatorState`. Domain repository contract; no crypto. |
| 8 | `PaperKeyDerivation.cs` | `PaperKeyDerivation` | static class | foundation-orchestration | BIP-39 24-word codec wrapper over a 32-byte root seed. Uses BCL `SHA256.HashData` for the BIP-39 checksum byte but does not extend SHA-256 — it orchestrates the BIP-39 algorithm on top of an existing primitive. Hand-off explicitly names "paper-key derivation orchestration (the BIP-39 wrapper that calls into kernel-security crypto)" as foundation. |
| 9 | `RecoveryCoordinator.cs` | `RecoveryCoordinator` | sealed class | foundation-orchestration | Production implementation of `IRecoveryCoordinator`. Self-described as "Pure orchestration — all I/O is delegated…". |
| 10 | `RecoveryCoordinatorOptions.cs` | `RecoveryCoordinatorOptions` | sealed record | foundation-orchestration | Configuration options (`MaxTrustees`, `QuorumThreshold`, `GracePeriodDuration`). Domain config; no crypto. |
| 11 | `RecoveryCoordinatorState.cs` | `RecoveryCoordinatorState` | sealed class | foundation-orchestration | Persistable state snapshot for the coordinator. Domain state container; no crypto. |
| 12 | `RecoveryDispute.cs` | `RecoveryDispute` | sealed record | foundation-orchestration | Signed dispute envelope (sub-pattern #48e). Uses BCL SHA-256 + `CryptographicOperations.FixedTimeEquals` + `IEd25519Signer`; domain message envelope analogous to `RecoveryRequest` and `TrusteeAttestation`. |
| 13 | `RecoveryEvent.cs` | `RecoveryEvent` | sealed record | foundation-orchestration | Hash-chained audit-log envelope (sub-pattern #48f). The structural envelope; the audit-log substrate itself is `Sunfish.Kernel.Audit` (separate package). |
| 14 | `RecoveryEvent.cs` | `RecoveryEventType` | enum | foundation-orchestration | Discriminator enum for `RecoveryEvent`. Domain vocabulary. |
| 15 | `RecoveryRequest.cs` | `RecoveryRequest` | sealed record | foundation-orchestration | Signed request envelope (sub-pattern #48a). Uses `IEd25519Signer` for signing/verification and a domain-separated canonical-bytes pattern. Hand-off explicitly names this as foundation. |
| 16 | `RecoveryStatus.cs` | `RecoveryStatusKind` | enum | foundation-orchestration | Status enum (`NoRequest`, `AwaitingAttestations`, `GracePeriodOpen`, `Completed`, `Disputed`). Domain vocabulary. |
| 17 | `RecoveryStatus.cs` | `RecoveryStatus` | sealed record | foundation-orchestration | Snapshot of in-flight state for host UI consumption. Domain read model. |
| 18 | `SystemRecoveryClock.cs` | `SystemRecoveryClock` | sealed class | foundation-orchestration | `IRecoveryClock` implementation backed by `DateTimeOffset.UtcNow`. Pure orchestration. |
| 19 | `TrusteeAttestation.cs` | `TrusteeAttestation` | sealed record | foundation-orchestration | Signed attestation envelope (sub-pattern #48a). Uses BCL SHA-256 + `IEd25519Signer` + domain-separated canonical-bytes pattern. Hand-off explicitly names this as foundation. |
| 20 | `TrusteeDesignation.cs` | `TrusteeDesignation` | sealed record | foundation-orchestration | Designation record `(NodeId, PublicKey, DesignatedAt)`. Pure domain. |

---

## Non-source files (also moving)

| # | Path | Kind | Notes |
|---|---|---|---|
| 21 | `Recovery/bip39-english.txt` | embedded resource | BIP-39 English wordlist (2048 words). Referenced from `Sunfish.Kernel.Security.csproj` line 34 as `<EmbeddedResource Include="Recovery/bip39-english.txt" LogicalName="Sunfish.Kernel.Security.Recovery.bip39-english.txt" />`. Must be moved alongside `PaperKeyDerivation.cs` and the `LogicalName` updated to `Sunfish.Foundation.Recovery.bip39-english.txt` so `PaperKeyDerivation.LoadEnglishWordlist` keeps resolving the resource (the resource lookup is namespace-derived). |

---

## Test files (also moving)

| # | Path | Subject | Notes |
|---|---|---|---|
| T1 | `packages/kernel-security/tests/Recovery/PaperKeyDerivationTests.cs` | `PaperKeyDerivation` | Tests the BIP-39 codec roundtrip + checksum-failure path. |
| T2 | `packages/kernel-security/tests/Recovery/RecoveryCoordinatorTests.cs` | `RecoveryCoordinator` | Tests the full state-machine progression: trustee designation → request → attestation → quorum → grace → completion (and dispute path). |
| T3 | `packages/kernel-security/tests/Recovery/RecoveryDisputeTests.cs` | `RecoveryDispute` | Tests dispute-message canonical-bytes + signature verification + replay protection. |
| T4 | `packages/kernel-security/tests/Recovery/TrusteeRecordTests.cs` | `TrusteeAttestation` + `TrusteeDesignation` + `RecoveryRequest` | Tests the trustee/request/attestation message envelopes (canonical bytes, sign/verify roundtrip, hash-binding). |

These move to a new `packages/foundation-recovery/tests/` test project. The folder name change from `tests/Recovery/` to `tests/` is intentional — `foundation-recovery/` *is* the recovery package, so the subfolder is redundant.

---

## What stays in `packages/kernel-security/`

Per this inventory, the following files/folders are **NOT** moved (they are kernel-tier substrate, not part of this hand-off's scope):

- `packages/kernel-security/Crypto/` — Ed25519 signer, X25519 key agreement (substrate; `RecoveryRequest`, `TrusteeAttestation`, `RecoveryDispute`, `RecoveryCoordinator` all consume `IEd25519Signer` from here)
- `packages/kernel-security/Keys/` — SqlCipher key derivation, keystore root-seed provider, rekey orchestration (substrate; `PaperKeyDerivation` codecs the root seed but is the wrapper, not the seed itself)
- All non-`Recovery/` files at the package root (assembly info, DI registration, etc.)

After Phase 3, `packages/kernel-security/Recovery/` ceases to exist. The kernel-security package's public surface no longer includes any orchestration types — only substrate primitives.

---

## Open questions for research-session review

1. **No `kernel-substrate` rows.** This is a stronger signal than the hand-off anticipated ("examples expected: any direct extensions of crypto primitives, low-level PRF wrappers, hash-chain-style helpers if present"). If research expected something to stay in `kernel-security/Recovery/`, it isn't present in the current code — the implicit Crypto/Keys-vs-Recovery split is already complete. Confirm this is acceptable, or flag the type that should be carved off as substrate.

2. **`IDisputerValidator` interface placement.** The interface is defined inside `IRecoveryCoordinator.cs` (rows #4, #6 above). It's classified as orchestration here because the interface itself describes a domain authorization concept. If research wants `IDisputerValidator` to remain a kernel-tier seam (so multiple foundation orchestrators could plug in), the file split needs to move just `IDisputerValidator` to a separate file in kernel-security and keep the rest of `IRecoveryCoordinator.cs` orchestration-side. Defaulting to "moves with the rest" pending guidance.

3. **`RecoveryEvent` vs `Sunfish.Kernel.Audit`.** Per ADR 0049, the audit-log substrate is now its own package. `RecoveryEvent` is the data record consumed by that substrate. Confirm `RecoveryEvent` moves to `foundation-recovery` (its producer) and the audit substrate references it from foundation, not the other way around.

4. **Embedded BIP-39 resource `LogicalName` change.** The current resource name is `Sunfish.Kernel.Security.Recovery.bip39-english.txt`. After move it should be `Sunfish.Foundation.Recovery.bip39-english.txt` to match the new namespace. `PaperKeyDerivation.LoadEnglishWordlist` derives the resource name from `typeof(PaperKeyDerivation).Namespace` so the lookup updates automatically — but flag this in case research wants to preserve the historical logical name for downstream resource-lookup compatibility.

---

## Phase 1 PASS gate

**sunfish-PM ⟶ research session:** Phase 1 inventory complete. Table above and open questions below await research review. **Phase 3 (the actual file moves) is BLOCKED until research signs off this inventory** per the hand-off's "Critical guard" clause.

**Recommended sign-off action:** comment on the PR landing this file (or update the workstream #15 row in `icm/_state/active-workstreams.md` notes column) with one of:
- "Inventory accepted; sunfish-PM may proceed with Phase 2 + 3."
- "Inventory accepted with the following adjustments: …"
- "Hold; research wants to revisit the architectural split."
