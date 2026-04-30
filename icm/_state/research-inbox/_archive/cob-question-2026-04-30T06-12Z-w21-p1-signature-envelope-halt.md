---
type: cob-question
workstream: 21
last-pr: 340
filed-by: COB
filed-at: 2026-04-30T06-12Z
---

# W#21 Phase 1 — Foundation.Crypto.SignatureEnvelope halt

## Context

W#21 Signatures hand-off Phase 1 (`kernel-signatures` substrate scaffold) explicitly references `Foundation.Crypto.SignatureEnvelope` per ADR 0054 amendment A2. Hand-off halt-condition #1:

> **Foundation.Crypto.SignatureEnvelope (ADR 0004) not shipped** at Phase 1 → halt; XO may stub the envelope ahead of full ADR 0004 Stage 06.

Verified absent on origin/main:

```bash
git ls-tree -r origin/main packages/foundation/Crypto/ --name-only
# Returns: CanonicalJson, DevKeyStore, Ed25519Signer/Verifier, IOperationSigner/Verifier, KeyPair, PrincipalId, Signature, SignedOperation
# Does NOT return: SignatureEnvelope
```

Halt-condition tripped.

## What COB needs from XO

The same pattern that resolved W#19 Phase 3 (Money / ThreadId / SignatureEventRef stubs via Phase 3 prereq addendum, PR #274) — XO authorizes a minimal stub shape in `packages/foundation/Crypto/SignatureEnvelope.cs` that W#21 Phase 1 can compile against. Full ADR 0004 envelope semantics follow in a dedicated Stage 06 hand-off.

Suggested minimal stub shape (per ADR 0054 A2):

```csharp
namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Algorithm-agility container for cryptographic signatures per ADR 0004.
/// W#21 Phase 0 stub mirroring W#19 Phase 0 pattern; full envelope semantics
/// land in dedicated Stage 06 hand-off.
/// </summary>
/// <param name="Algorithm">Signature algorithm identifier (e.g., "ed25519", "ecdsa-p256-sha256").</param>
/// <param name="Signature">Raw signature bytes per the algorithm spec.</param>
/// <param name="Headers">Algorithm-agility headers (key id, certificate chain, etc.); opaque dictionary.</param>
public sealed record SignatureEnvelope(
    string Algorithm,
    byte[] Signature,
    IReadOnlyDictionary<string, string> Headers);
```

Alternative path: XO may direct COB to ship the stub inline as part of Phase 1 (Option A precedent from W#19 Phase 3 addendum). Either path unblocks Phase 1.

## What I shipped instead this iteration

Pivoting to **W#28 Phase 3.1** (providers-recaptcha first adapter — already declared as the follow-on in PR #320 description). Phase 3.1 is contained + sets up the providers-* convention + first BannedSymbols.txt expansion per ADR 0013.

## Cross-references

- W#21 hand-off: `icm/_state/handoffs/property-signatures-stage06-handoff.md` §"Phase 1" + §"Halt conditions" #1
- ADR 0054 amendment A2 (Foundation.Crypto.SignatureEnvelope reference)
- W#19 Phase 3 addendum precedent: `icm/_state/handoffs/property-work-orders-stage06-addendum.md` (Money/ThreadId/SignatureEventRef stubs via Option A)
- ADR 0004 (originally specified the envelope; not yet Stage 06)
