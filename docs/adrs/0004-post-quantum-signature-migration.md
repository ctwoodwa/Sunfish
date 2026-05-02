---
id: 4
title: Post-Quantum Signature Migration Plan
status: Accepted
date: 2026-04-19
tier: kernel
concern:
  - audit
  - security
  - version-management
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0004 — Post-Quantum Signature Migration Plan

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** G34 (Appendix C #4)

---

## Context

The Sunfish platform specification asks (Appendix C #4): *"Post-quantum signature algorithm
migration plan."*

The spec §3.3 references Ed25519 as the current signature algorithm. The question is how Sunfish
will migrate to post-quantum (PQ) algorithms when they are required.

**Shipped state — `Sunfish.Foundation.Crypto.Signature`:**

```csharp
// packages/foundation/Crypto/Signature.cs (as of this ADR)
public readonly record struct Signature
{
    public const int LengthInBytes = 64;    // Ed25519 fixed — 64 bytes
    // ...
    public static Signature FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != LengthInBytes)
            throw new ArgumentException(...);
        // ...
    }
}
```

**Critical fact: `Signature` is NOT algorithm-agile today.** It is a fixed-size (64-byte)
Ed25519-only type. There is no algorithm tag, no discriminated envelope, and no extension point
for other algorithms. Calling `FromBytes` with a non-Ed25519-sized payload throws immediately.

This means the algorithm-agility prerequisite — carrying the algorithm identifier alongside the
bytes — is a **spec clarification requirement before v1 ships**, not a v0 code change.

**PQ timeline context (as of April 2026):**
- NIST finalized ML-DSA (CRYSTALS-Dilithium), SLH-DSA (SPHINCS+), and ML-KEM (CRYSTALS-Kyber)
  in FIPS 203/204/205 (August 2024).
- Harvest-now-decrypt-later (HNDL) attacks on asymmetric encryption are already a concern for
  long-term data confidentiality.
- For **signatures** (the Sunfish use case), the threat model is weaker: a quantum computer must
  exist and be available to an attacker at the time of signature forging. NIST and NSA guidance
  as of 2026 does not require PQ signature migration before 2030 for most categories.
- Sunfish's long-horizon customers (government, military, 10+ year audit retention) are the
  primary audience for a PQ migration plan.

---

## Decision

**Algorithm-agility is a first-class design concern for Sunfish signatures. The migration path is
dual-sign with a transition window. No code ships with this ADR; the prerequisite tagging work
is a v1 requirement.**

### 1. Algorithm-agility tagging is a v1 requirement (spec clarification)

Before v1 ships, `Sunfish.Foundation.Crypto.Signature` MUST be refactored into a discriminated
envelope that carries an algorithm tag alongside the signature bytes. The recommended shape:

```csharp
// Target shape — spec §3.3 clarification required before v1
public readonly record struct Signature
{
    public SignatureAlgorithm Algorithm { get; }  // e.g., Ed25519, MlDsa65
    // ...signature bytes, sized per algorithm...
}

public enum SignatureAlgorithm
{
    Ed25519 = 1,
    MlDsa44 = 2,   // ML-DSA-44 (FIPS 204, smallest parameter set)
    MlDsa65 = 3,   // ML-DSA-65 (FIPS 204, medium — NIST security level 3)
    MlDsa87 = 4,   // ML-DSA-87 (FIPS 204, largest)
    SLHDsaSha2_128s = 5,  // SLH-DSA-SHA2-128s (FIPS 205, stateless hash-based)
}
```

This is a **breaking change** to `Signature` and a MAJOR version bump. A separate ADR or spec
PR should track this work. **It is a prerequisite for any PQ migration; without the tag, the
dual-sign plan below cannot be implemented.**

### 2. Dual-sign transition window (when PQ support ships)

When NIST PQ algorithms are implemented in a future Sunfish release, the migration plan is:

**Phase 1 — Dual-sign introduction (opt-in)**
- Producers may optionally attach a second PQ signature alongside Ed25519.
- Verifiers accept either signature as sufficient for validity (OR logic, not AND).
- Signing configurations declare which algorithms to emit.

**Phase 2 — Dual-sign required (transition window)**
- Producers MUST attach both Ed25519 and a NIST PQ signature (e.g., ML-DSA-65).
- Verifiers accept either, giving legacy consumers time to update.
- Transition window duration: a minimum of 24 months (to be confirmed at the time of
  Phase 2 launch). The window start date is the Phase 2 release date.

**Phase 3 — Ed25519 deprecated**
- Ed25519 is removed from the required signing set.
- Verifiers may drop Ed25519 support after a further 12-month notice period.

```
Timeline (illustrative — actual dates depend on PQ implementation readiness):

  Now (2026)          ~2028           ~2030           ~2032
  |                   |               |               |
  [Ed25519 only]  [dual-sign opt-in] [dual-sign req] [PQ only]
  |_______________|_______________|_______________|
        v0/v1              v2              v3
```

### 3. Algorithm selection recommendation

When PQ signatures ship, the default recommended algorithm is **ML-DSA-65** (CRYSTALS-Dilithium,
security level 3, FIPS 204). Rationale:
- NIST-standardized, not just a finalist.
- Security level 3 (equivalent to AES-192) is appropriate for government and commercial use.
- ~2.4 KB public key, ~3.3 KB signature — larger than Ed25519 but manageable for Sunfish's
  signing envelope pattern.
- `.NET 10+` is expected to provide first-class `System.Security.Cryptography` support;
  pre-.NET 10, a NuGet package (e.g., `BouncyCastle.Cryptography`) would be required.

SLH-DSA (SPHINCS+, FIPS 205) is the conservative stateless hash-based alternative; it has
larger signatures (~8–49 KB depending on parameter set) and is recommended only for highest-
assurance contexts where Dilithium's lattice hardness assumptions are not trusted.

### 4. No code changes with this ADR

This ADR is a position statement. No code changes ship with it. The next concrete action is the
spec §3.3 clarification PR to add the algorithm-agility requirement for the `Signature` type,
tracked as a v1 prerequisite.

---

## Consequences

**Positive**
- Post-quantum readiness is a stated design goal — Sunfish can communicate this to long-horizon
  customers today.
- The dual-sign transition window preserves backward compatibility across the migration.
- Algorithm selection is deferred to when the standards are stable and .NET support is available.
- The prerequisite tagging work is identified clearly; teams know what must happen before PQ
  can be implemented.

**Negative / Trade-offs**
- `Signature` is currently algorithm-locked to Ed25519. The required refactor is a breaking
  change; all consumers of `Signature.FromBytes` and `Signature.LengthInBytes` must be updated.
- ML-DSA signatures are ~50x larger than Ed25519 (3.3 KB vs 64 bytes). Deployments that store
  large numbers of signed events will see meaningful storage growth during the dual-sign window.
- The `.NET` PQ cryptography story is not finalized; timeline depends on upstream.
- The dual-sign transition window adds operational complexity (two signing keys, two verification
  paths).

**Open questions**
- Which Sunfish version will contain the `Signature` algorithm-agility refactor?
- Will the PQ key material be included in the `SignedOperation<T>` envelope, or managed
  separately via `IOperationSigner` / `IOperationVerifier`?
- Key rotation ceremony for PQ keys (larger keys may impact key derivation workflows).

---

## References

- Sunfish platform spec §3.3
- `packages/foundation/Crypto/Signature.cs` — current Ed25519-only, fixed-size implementation
- NIST FIPS 204 (ML-DSA / CRYSTALS-Dilithium): https://doi.org/10.6028/NIST.FIPS.204
- NIST FIPS 205 (SLH-DSA / SPHINCS+): https://doi.org/10.6028/NIST.FIPS.205
- NSA CNSA 2.0 suite: https://media.defense.gov/2022/Sep/07/2003071834/-1/-1/0/CSA_CNSA_2.0_ALGORITHMS_.PDF
- Gap analysis G34: `icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md`
