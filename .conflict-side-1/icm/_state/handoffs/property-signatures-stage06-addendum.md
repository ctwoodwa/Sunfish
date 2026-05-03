# Workstream #21 — Signatures Stage 06 hand-off addendum (Phase 0 stub)

**Supersedes:** [`property-signatures-stage06-handoff.md`](./property-signatures-stage06-handoff.md) Phase 1 prereq
**Effective:** 2026-04-30 (resolves `cob-question-2026-04-30T06-12Z-w21-p1-signature-envelope-halt.md`)
**Spec source:** [ADR 0054 amendment A2](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) (algorithm-agility envelope reference) + [ADR 0004](../../docs/adrs/0004-algorithm-agility-signatures.md) (originally specified the envelope shape; not yet Stage 06)

COB (sunfish-PM) halted W#21 Phase 1 on the absence of `Sunfish.Foundation.Crypto.SignatureEnvelope` per Phase 1 halt-condition #1. This addendum authorizes a minimal-stub Phase 0 — same pattern as W#19 Phase 3 prereq addendum (Money / ThreadId / SignatureEventRef stubs, PR #274) — so Phase 1 can compile against a typed surface while full ADR 0004 envelope semantics ship in a dedicated Stage 06 hand-off.

Per Decision Discipline Rule 3 (auto-accept mechanical amendments), this addendum is mechanical: COB proposed the stub shape in the beacon (`cob-question-2026-04-30T06-12Z-w21-p1-signature-envelope-halt.md`); XO ratifies + adds the addendum-as-spec.

---

## Phase 0 — `SignatureEnvelope` minimal stub (~30 min, prereq for Phase 1)

**Path:** Option A precedent from W#19 Phase 3 addendum — COB ships the stub inline as Phase 0 of W#21 (single PR with both Phase 0 stub + Phase 1 substrate scaffold). No separate XO-authored stub PR.

**File to create:**
- `packages/foundation/Crypto/SignatureEnvelope.cs`

**Stub shape** (per ADR 0054 A2 cross-reference; matches COB beacon proposal):

```csharp
namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Algorithm-agility container for cryptographic signatures per ADR 0004.
/// W#21 Phase 0 stub authorized by ADR 0054 amendment A2; full envelope
/// semantics + verification pipeline + dual-sign window for PQC migration
/// land in a dedicated ADR 0004 Stage 06 hand-off (not yet authored).
/// </summary>
/// <param name="Algorithm">
/// Signature algorithm identifier. Phase 1 callers SHOULD use the canonical
/// strings <c>"ed25519"</c> (matching <see cref="Ed25519Signer"/>) or
/// <c>"ecdsa-p256-sha256"</c>. The Phase 0 stub does NOT validate the
/// algorithm string — that's deferred to ADR 0004 Stage 06.
/// </param>
/// <param name="Signature">
/// Raw signature bytes per the algorithm spec. For ed25519, 64 bytes.
/// </param>
/// <param name="Headers">
/// Algorithm-agility headers (key id, certificate chain, attestation tags,
/// etc.); opaque dictionary in Phase 0; ADR 0004 Stage 06 will type-narrow
/// to specific known headers (per RFC 9421 / COSE-style header registry).
/// </param>
public sealed record SignatureEnvelope(
    string Algorithm,
    byte[] Signature,
    IReadOnlyDictionary<string, string> Headers);
```

**Acceptance for Phase 0:**
- [ ] File at `packages/foundation/Crypto/SignatureEnvelope.cs`
- [ ] Compiles clean against existing `foundation` csproj (no new package references needed)
- [ ] XML doc on the type + each parameter
- [ ] Smoke test: `new SignatureEnvelope("ed25519", new byte[64], new Dictionary<string, string>())` round-trips through equality + JSON

**NOT in scope for Phase 0** (deferred to ADR 0004 Stage 06):
- Algorithm validation (Phase 0 accepts any string)
- Header registry (Phase 0 accepts any dictionary)
- Verification pipeline (`IOperationVerifier` / `VerifyAsync` over envelope)
- Dual-sign window for PQC migration (per ADR 0004 §"Open questions")
- Per-algorithm signature-length validation
- Canonical encoding for envelope-as-payload-of-`SignedOperation<T>` use cases

**Gate (Phase 0):** PASS iff `dotnet build packages/foundation/Sunfish.Foundation.csproj` clean + smoke test passes.

**PR title (bundle Phase 0 + Phase 1):** `feat(foundation-crypto,kernel-signatures): SignatureEnvelope stub + Phase 1 substrate scaffold (W#21 Phase 0+1, ADR 0054)`

---

## Phase 1 — Unblocked

W#21 Phase 1 (`kernel-signatures` substrate scaffold per the original hand-off) proceeds against this stub shape. Phase 1 references `SignatureEnvelope` exactly as the original hand-off spec'd; no Phase 1 changes required.

---

## Forward path

A future ADR 0004 Stage 06 hand-off authors the full envelope semantics. When that lands, callers using the Phase 0 stub continue to compile (the type signature is forward-compatible; only the validation behavior changes — `Algorithm` becomes registry-checked, `Headers` gets type-narrowed). Migration is mechanical.

XO will note this in a memory file so future ADR 0004 Stage 06 work picks up the dependency on the Phase 0 stub.

---

## Decision-class

Session-class per `feedback_decision_discipline` Rule 1 (NOT CO-class — pure mechanical addendum following the W#19 Phase 3 precedent). Authority: XO; COB proposed the stub shape; XO ratifies via this addendum.

---

## References

- COB beacon: `icm/_state/research-inbox/cob-question-2026-04-30T06-12Z-w21-p1-signature-envelope-halt.md` (PR #341)
- W#19 Phase 3 precedent: `icm/_state/handoffs/property-work-orders-stage06-addendum.md` (Money / ThreadId / SignatureEventRef stubs)
- ADR 0054 amendment A2: `Foundation.Crypto.SignatureEnvelope` reference
- ADR 0004: original spec for the envelope (not yet Stage 06)
- Original hand-off: `icm/_state/handoffs/property-signatures-stage06-handoff.md`
