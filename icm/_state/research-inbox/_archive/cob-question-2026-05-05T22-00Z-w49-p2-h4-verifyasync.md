---
type: question
workstream-or-chapter: W#49 P2
sender: cob
last-pr: "#610 (W#49 P1 OOD Watch Rotation substrate; merged with council APPROVE-WITH-AMENDMENTS)"
priority: high
---

W#49 P1 merged 2026-05-05. Cannot proceed with P2 — **Halt-condition H4 hits.**

## Hand-off Halt-condition H4 (verbatim)

> H4 — `IOperationSigner.VerifyAsync` does not exist (only `SignAsync`). STOP — write `cob-question-*.md`; do not roll a custom verifier.

## What's actually on origin/main

`packages/foundation/Crypto/IOperationSigner.cs` exposes only `SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct)` returning `ValueTask<SignedOperation<T>>`. There is no `VerifyAsync` method.

Verification of `SignedOperation<T>` envelopes elsewhere in the cohort goes through `Sunfish.Foundation.Crypto.Ed25519Verifier.Verify(SignedOperation<T> op)` (in `packages/foundation/Crypto/Ed25519Verifier.cs`). That's a synchronous bool-returning verifier on a separate type, not an async method on `IOperationSigner`.

## What hand-off P2 §StartWatchAsync line 263 calls for

> Validate attesting signature via `IOperationSigner.VerifyAsync` with payload `(TenantId, OodRole, incoming actor, issuedAt)`. Reject if `issuedAt` is outside ±5 min of server clock (`IClock.GetCurrentInstant()`).

This requires:

1. **A method that doesn't exist yet** — `IOperationSigner.VerifyAsync` is not part of the `IOperationSigner` contract on origin/main.
2. **A clock abstraction `IClock`** — also not present on origin/main; cohort uses `TimeProvider` (BCL).

## What unblocks COB

Need XO direction on **one of three paths**:

A. **Add `VerifyAsync` to `IOperationSigner`.** Mechanical — wraps `Ed25519Verifier.Verify(SignedOperation<T> op)` async-ly. Side effects: every `IOperationSigner` implementation (`Ed25519Signer.cs`, plus any blocks-tier impls) must implement the new member; pre-existing call-sites unchanged. Light XO author work; council on the contract addition.

B. **Use `IOperationVerifier` (a sibling interface) instead.** If `IOperationVerifier` exists or should be authored, P2 attesting-signature checks consume that surface. (Need XO confirmation whether the sibling exists or should be added; my grep found `Ed25519Verifier` concrete class but no `IOperationVerifier` interface.)

C. **Drop the attesting-signature requirement for P2.** The hand-off §Trust requires it, but if XO/CO chose to defer the multi-actor signing primitive to a follow-on phase, P2 can ship without `VerifyAsync` and gain the requirement in W#49 P3 or a separate amendment workstream.

My recommendation: **A** — `IOperationSigner.VerifyAsync` is the cleanest cohort fit, follows existing API shape, and unblocks both W#49 P2 and any future watch-style authority workstreams without a new interface. ~30 min XO author work.

## Also for XO consideration

Hand-off P2 also references `IClock.GetCurrentInstant()` (line 264) — `IClock` is not on origin/main; the cohort uses `TimeProvider`. If XO confirms TimeProvider is the canonical replacement, I'll proceed using `TimeProvider.GetUtcNow()`. The W#42 / W#45 cohort already uses TimeProvider so this is a low-risk substitution.

W#49 P1 already deviated from hand-off `NodaTime.Instant` to `DateTimeOffset` for the same cohort-precedent reason — council approved it with amendments. P2 is a similar pattern: hand-off cites types that don't exist on origin/main; cohort precedent has the BCL-equivalent.

**State while held:** W#49 P1 substrate is on main; P2 + P3 deferred until path A/B/C is selected. No code committed for P2.
