# ADR 0046 — Key-loss recovery scheme for Business MVP Phase 1

**Status:** Accepted (2026-04-26)
**Date:** 2026-04-26
**Resolves:** Open Question Q5 from `icm/01_discovery/output/business-mvp-phase-1-discovery-interim-2026-04-26.md` (key-loss recovery implementation reference for primitive #48).

## Context

The Sunfish Business MVP Plan (`C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md` §10 Phase 1, deliverable #5) calls for *"Backup/restore + multi-sig social recovery (#48 key-loss recovery)."* The book's `design-decisions.md` §5 Volume 1 extensions defines primitive #48 with **6 sub-patterns** (48a-48f), but the plan does not specify which combination Sunfish should ship for Phase 1.

P7 (ownership) is the most user-visible Kleppmann property and the most common real-world failure mode in local-first deployments — *"user loses their key → loses all data."* Sub-patterns 48a-48f differ substantially in cryptographic complexity, UX burden, and post-MVP extensibility. Phase 1 needs a defined posture so the implementation can land without re-litigating the choice mid-build.

## Decision drivers

- **P7 ownership is non-negotiable.** Without recovery, a forgotten password = bankrupt customer's books are gone. Sunfish loses on Day 1.
- **MVP audience is SMB owners**, not crypto-natives. Recovery UX must work without trustees being technical, without paper-key handling expertise, without smartphone biometric adoption assumptions.
- **Primitive #48 was added to the catalog in v1.1** specifically because earlier Kleppmann property analysis ASSUMED keys are never lost. Sunfish cannot afford the same assumption.
- **Failed-conditions clause for #48** (per `concept-index.yaml` P7 kill-trigger): "Key-loss recovery path inadequate (no multi-sig OR custodian OR paper-key fallback)." MVP must satisfy at least one of those three.
- **Existing kernel-security primitives** (Ed25519, X25519, SqlCipher key derivation, root seed provider, role keys, team subkeys) already cover the cryptographic substrate. The recovery scheme is a layer ON TOP of these, not a replacement.
- **Post-MVP extensibility** matters — institutional custodians (48b) and biometric-derived keys (48d) are real customer asks for v1.x but require significant per-platform work. They should not gate Phase 1.

## Considered options

### Option A — 48a only (multi-sig social recovery, 3-of-5 friends)

- **Pro:** simplest cryptographic implementation; well-understood Argent / Vitalik-essay pattern; works for any user with 5 contacts.
- **Con:** no offline fallback (lost laptop AND lost trustees = no recovery); no audit trail; no time-locked grace period (race with potential attackers).
- **Verdict:** insufficient on its own. P7 kill-trigger requires "multi-sig OR custodian OR paper-key" — single-leg recovery is brittle.

### Option B — 48a + 48c (social + paper-key fallback)

- **Pro:** social recovery covers the common case; paper-key (printed seed phrase stored offline) is the disaster-recovery fallback. Two independent legs.
- **Con:** still no audit trail; still no time-locked grace period; no defense against trustee-collusion attacks.
- **Verdict:** acceptable but missing key safety patterns from the catalog.

### Option C — 48a + 48e + 48f + 48c (social + grace period + audit trail + paper fallback) **[RECOMMENDED]**

- **48a multi-sig social recovery** — 3-of-5 trustees can attest to a recovery request
- **48e timed-recovery with grace period** — once 3 trustees attest, original holder gets 7-day window to dispute (pushes attacker timeline outside reasonable response window)
- **48f cryptographically-signed audit trail** — recovery event recorded as signed log entry; future devices can replay and verify
- **48c paper-key fallback** — printed recovery phrase stored offline (bank safe deposit box, fire-safe at home); independent of all 5 trustees being available
- **Pro:** covers the catalog's full failed-conditions list; defense in depth; matches Argent + Apple iCloud Keychain + crypto-wallet patterns; survives most realistic threat scenarios
- **Con:** 4 sub-patterns to implement vs 1 or 2; UX flow has 2 entry points (social + paper); initial Trustee Setup wizard has more steps
- **Verdict:** the right Phase 1 posture. The complexity is unavoidable for P7 to actually hold.

### Option D — 48a + 48b + 48c + 48d + 48e + 48f (all six)

- **Pro:** maximum coverage; matches Apple iCloud Keychain comprehensiveness
- **Con:** 48b (institutional custodian) requires legal partnerships, attestation infrastructure, and per-jurisdiction compliance — months of out-of-band work; 48d (biometric-derived) requires per-platform native API integration (Win Hello / Touch ID / fingerprint reader / Linux PAM) — broad platform surface; both are post-MVP per plan §13
- **Verdict:** out of Phase 1 scope; deferred to post-MVP roadmap

## Decision

**Adopt Option C.** Phase 1 ships:

1. **48a multi-sig social recovery** — owner designates 3-of-5 trustees; each receives an Ed25519 signing key during onboarding; quorum of 3 attests to recovery request via signed message
2. **48e timed-recovery with grace period** — once quorum attested, original holder has 7 days (configurable per deployment, 7-30 day range) to dispute via any device still holding original keys; only after window expires does new key issue
3. **48f cryptographically-signed audit trail** — every recovery event written to per-tenant audit log (encrypted, signed by attesting trustees, timestamped); replicated via the same sync protocol as business data; visible in tenant audit-log UI
4. **48c paper-key fallback** — at first-run, owner can optionally print/save a 24-word recovery phrase (BIP-39 wordlist for industry-standard compatibility); recovery phrase derives the same root seed as device-key, bypassing trustee quorum

48b (institutional custodian) and 48d (biometric-derived) are explicitly **deferred to post-MVP**. The Phase 1 architecture must not preclude adding them later (the recovery surface is an extensible enum, not hard-coded to the 4 ship-now flows).

## Package placement (added 2026-04-29)

The Phase-1 implementation is split across two packages per the paper's §5 tier model:

- **`packages/kernel-security/`** — kernel-tier crypto primitives that recovery depends on:
  - `Crypto/Ed25519Signer.cs`, `Crypto/X25519KeyAgreement.cs` (signature primitives)
  - `Keys/SqlCipherKeyDerivation.cs`, `Keys/KeystoreRootSeedProvider.cs` (key derivation primitives)
  - `Keys/RotateKeyAsync` SQLCipher rekey primitive
  - `AddSunfishKernelSecurity` + `AddSunfishRootSeedProvider` DI extensions

- **`packages/foundation-recovery/`** — foundation-tier orchestration over those primitives:
  - `IRecoveryCoordinator` + `RecoveryCoordinator` (pure orchestration; sub-patterns #48a / #48e / #48f)
  - `RecoveryRequest`, `TrusteeAttestation`, `RecoveryDispute`, `RecoveryEvent` (signed message envelopes)
  - `TrusteeDesignation`, `RecoveryCoordinatorOptions`, `RecoveryCoordinatorState`, `RecoveryStatus`
  - `IRecoveryStateStore` + `InMemoryRecoveryStateStore`
  - `IRecoveryClock` + `SystemRecoveryClock`
  - `IDisputerValidator` + `FixedDisputerValidator`
  - `PaperKeyDerivation` (#48c — BIP-39 wrapper over kernel-security PRFs)
  - BIP-39 English wordlist (`bip39-english.txt` embedded; LogicalName `Sunfish.Foundation.Recovery.bip39-english.txt`)
  - `IAuditTrail` integration per ADR 0049 (event emission target)
  - `AddSunfishRecoveryCoordinator` DI extension (registers all the above)

This split was identified during the 2026-04-28 ADR audit (audit finding C-2 in
`icm/07_review/output/adr-audits/CONSOLIDATED-HUMAN-REVIEW.md`). The original ADR
0046 text referenced `Sunfish.Foundation.Recovery` for the entire scheme — that
was a planning-time guess made before the kernel-security substrate was complete.
The shipped reality (split-by-concern) is the de-facto correct decision; this
section ratifies it. Phase 1 inventory + research-session sign-off lives at
`icm/_state/handoffs/adr-0046-recovery-package-split-INVENTORY.md`.

## Consequences

### Positive

- P7 ownership demonstrably works; user can recover from forgotten password OR lost laptop OR lost trustees (any one of those failure modes survivable as long as the OTHER one isn't simultaneously broken)
- Audit trail provides legally-defensible record of recovery events (matters for accountant-of-record liability, for divorce/dispute scenarios, for regulator audits)
- Paper-key offers fully-offline disaster recovery (works even if Sunfish, the trustees, the company, and the internet are all unavailable)
- Argent / Apple-Keychain pattern alignment makes the UX familiar for users who've already done crypto wallet setup or Apple-Account recovery
- Architecture is forward-compatible with 48b (custodian) and 48d (biometric) when those are added post-MVP

### Negative

- Trustee Setup wizard adds 5-10 min to first-run onboarding (mitigation: defer trustee designation; allow setup with paper-key only first, prompt for trustees after first week of use)
- Paper-key UX requires user discipline (print + secure offline storage); historically poor adoption in crypto wallets; need clear messaging that this IS the disaster recovery
- 7-day grace period means actual recovery is not instant; users locked out for a week if they don't have paper-key
- Trustees must keep their Ed25519 signing keys safe; trustee key loss reduces effective quorum; recommend N+M trustees for resilience (e.g., name 5, but allow 3 to attest — rebuilds when any trustee replaces their key)

## Revisit triggers

- A real customer scenario hits the trustee-coordination problem (e.g., 5 trustees but 3 forgot they were trustees) — may need UX or process adjustment
- Apple ships a new Secure Enclave recovery primitive that meaningfully changes 48d's complexity calculus
- Regulated SMB segment (healthcare, finance) requires institutional-custodian-of-record (48b) to satisfy compliance — promote 48b from post-MVP to a v1.x deliverable
- A real recovery incident produces a post-mortem that surfaces a missing pattern
- Cryptocurrency seed-phrase UX research advances meaningfully (e.g., Apple-Account recovery improvements; new hardware-wallet patterns)

## References

- Spec source: `C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md` §10 Phase 1 deliverable #5
- Primitive #48 catalog entry: `C:/Projects/the-inverted-stack/docs/reference-implementation/design-decisions.md` §5 Volume 1 extensions item 48
- Failed-conditions for P7: `C:/Projects/the-inverted-stack/docs/reference-implementation/concept-index.yaml` P7 kill-triggers (key-loss recovery path inadequate)
- Existing kernel-security substrate: `packages/kernel-security/Crypto/Ed25519Signer.cs`, `Crypto/X25519KeyAgreement.cs`, `Keys/SqlCipherKeyDerivation.cs`, `Keys/KeystoreRootSeedProvider.cs`
- Phase 1 intake: `icm/00_intake/output/business-mvp-phase-1-foundation-intake-2026-04-26.md`
- Sister ADR 0044: Anchor ships Windows-only for Phase 1 (D1 outcome)
- External pattern references (per primitive #48): Argent wallet social recovery; Vitalik Buterin "Why we need wide adoption of social recovery wallets" (2021); Apple iCloud Keychain recovery; BIP-39 wordlist for paper-key
