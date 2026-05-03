---
id: 46
title: Historical-Keys Projection for Signature Survival under Operator-Key Rotation
status: Proposed
date: 2026-04-29
tier: foundation
concern:
  - security
  - audit
composes: []
extends: []
supersedes: []
superseded_by: null
amendments:
  - A2
  - A3
---
# ADR-0046-A1 — Historical-Keys Projection for Signature Survival under Operator-Key Rotation

**Status:** Proposed
**Date:** 2026-04-29
**Amends:** [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — Key-loss recovery scheme for Business MVP Phase 1 (Accepted 2026-04-26)
**Driven by:** [ADR 0054 Amendment A3](./0054-electronic-signature-capture-and-document-binding.md#amendment-a3--adr-0046-a1-promoted-to-standalone-adr-major) (council review §5 A3 — "Operator-as-witness device-key rotation has no end-to-end verification path")

---

## Context

ADR 0054 (Electronic Signatures, Accepted 2026-04-29 with 7 amendments) introduces an **operator-as-witness** signature model: the operator (property manager holding the iPad) signs the SignatureEvent payload with their device-key, alongside the signer's pen-stroke biometric. This produces a two-identity cryptographic record that is stronger evidence than either identity alone.

The operator's device-key is custodied by Foundation.Recovery (per ADR 0046). When the operator rotates their device-key — either through normal lifecycle (new iPad, new role, scheduled rotation) or emergency recovery (lost device, multi-sig social recovery 48a, paper-key fallback 48c) — the question becomes: **what happens to the legal validity of signatures captured under the old device-key?**

Three constraints converge:

1. **Signatures retain 10 years.** They are the longest-retention data class in the system. A lease signed in 2026 may need to be verified in 2036 — across many key rotations.
2. **Verification must be cryptographically reproducible.** Any party (including a defense lawyer in 2036) must be able to recompute that the captured signature was genuinely produced by the device-key custodian at the time of capture.
3. **Recovery primitives are stateful.** The current `KeyHistoryProjection` in Foundation.Recovery records *the current owner state*. It does not record the operator-key timeline as a chain of (PreviousKey, NextKey, RotationEvent) tuples.

ADR 0054's Amendment A3 promoted the original inline paragraph in ADR 0054's "Compatibility plan" to this standalone companion ADR. The original paragraph specified `historical_keys[]` as a concept; this ADR specifies the schema, verification flow, and audit-event emission rigorously enough that:

- **Stage 06 implementer** can build the projection without ambiguity
- **Council review** can ratify the design before kernel-signatures Stage 06 build begins
- **Forensic dispute** in 2036 can be answered with substrate-only artifacts (not engineer interviews)

ADR 0054's Stage 06 build does not start until this ADR is Accepted.

---

## Decision drivers

- **ADR 0054 sequencing.** kernel-signatures Stage 06 build is gated on this ADR.
- **10-year retention horizon.** Verification must work after multiple operator-key rotations, including post-Sunfish-employment of the original operator (e.g., a contractor who later rotates) and post-algorithm-deprecation of Ed25519 (per ADR 0004).
- **Forensic-dispute defensibility.** Defense lawyers must be able to ask "show me how this 2026 signature was produced and verify against the operator's then-current key" and receive a deterministic, substrate-only answer.
- **Multi-sig social recovery (48a) compatibility.** When operator-key recovery happens via 3-of-5 social recovery (per ADR 0046 Decision §"Recommended Phase 1: 48a + 48c + 48f"), the historical-keys projection must capture the recovery event so verifiers can distinguish "key rotated by routine lifecycle" from "key rotated by social recovery" (the latter is a higher-evidence-weight rotation in court).
- **Paper-key fallback (48c) compatibility.** Same constraint: paper-key activation is a special-case rotation event with distinct audit/legal weight.
- **Local-first sync.** Historical-keys projection must sync via ADR 0028 CRDT substrate so all nodes (and offline iPad) can verify any signature against any historical key at any time.
- **Append-only audit.** ADR 0049 audit substrate emits each rotation event; the historical-keys projection is computed by reading the audit stream.
- **Algorithm-agility.** ADR 0004 SignatureEnvelope adoption (per ADR 0054 Amendment A2) means historical keys may be in different algorithms (e.g., 2026 Ed25519, 2030 PQ-Mode-A, 2034 PQ-Mode-B). Projection must support multi-algorithm verification.

---

## Considered options

### Option A — `historical_keys[]` projection on Foundation.Recovery state [RECOMMENDED]

Store the operator-key timeline as an append-only projection on `Foundation.Recovery` derived from kernel-audit's rotation events. Each historical key entry carries: key id, public-key bytes, algorithm, valid-from timestamp, valid-until timestamp (null for current), rotation reason, rotation event audit-id.

Verification flow:
1. Verifier receives `SignatureEvent` with `DeviceAttestation.OperatorDeviceKeyId` + `DeviceAttestation.Envelope` + `CapturedAt` timestamp
2. Verifier consults `historical_keys[OperatorIdentity]` projection at `CapturedAt`
3. Verifier finds the operator's then-current public-key + algorithm
4. Verifier verifies `Envelope.Payload` against `Envelope.Algorithm` using the historical public-key
5. Result: SignatureValid / WitnessKeyRevoked / SignatureCorrupted (all per ADR 0054 SignatureValidityReason enum)

- **Pro:** clean projection over append-only audit stream; matches ADR 0049 substrate pattern.
- **Pro:** no schema changes to existing Foundation.Recovery state machine — projection is computed read-only.
- **Pro:** multi-algorithm support is intrinsic (each historical key declares its own algorithm).
- **Pro:** local-first sync via ADR 0028 — projection is a CRDT G-Set of audit-derived rotation events.
- **Pro:** rotation reason field enables legal weighting (routine vs social recovery vs paper-key fallback).
- **Con:** projection complexity — must handle late-arriving offline rotation events from CRDT sync.

**Verdict:** Recommended.

### Option B — Embed historical key in each `SignatureEvent.DeviceAttestation`

Each SignatureEvent stores the operator's then-current public-key directly in `DeviceAttestation`. No external projection needed; verification is fully self-contained per signature.

- **Pro:** maximum self-containment; verification has zero dependencies on external state.
- **Con:** denormalization — every signature carries 32-256 bytes of redundant key data; over millions of signatures this adds storage cost.
- **Con:** key-revocation handling becomes unclear — if a key is later compromised, every signature carrying that embedded key is now suspect; with the projection approach, revocation flows through the audit stream and verifiers consult both.
- **Con:** cannot retroactively *invalidate* a key (e.g., compromised key discovered post-fact); embedded copies remain "valid-looking" forever.
- **Verdict:** Rejected. Forensic-dispute scenarios where a key is later discovered compromised require a centralized truth source (the projection); embedded copies create ambiguity.

### Option C — External Certificate Authority + X.509 certificates per operator-key

Adopt PKI: each operator-key is wrapped in an X.509 certificate signed by a Sunfish-operated CA. Verification consults CA + revocation list (CRL/OCSP).

- **Pro:** industry-standard PKI; lawyers and courts understand X.509.
- **Pro:** revocation handled by CRL/OCSP per RFC 5280.
- **Con:** introduces a server dependency (CA) — violates Sunfish's local-first paradigm.
- **Con:** Sunfish would have to operate a CA, which is a substantial infrastructure + legal liability burden (CA operations are a regulated, audited activity).
- **Con:** CRL/OCSP requires online verification; iPad in basement can't verify signatures that way.
- **Con:** doesn't compose with ADR 0028 CRDT model — CA chains are a centralized trust hierarchy.
- **Verdict:** Rejected. Inconsistent with Sunfish's local-first + paradigm + sustainability model. Future PKI integration as an opt-in `providers-pki-*` adapter is fine, but cannot be the substrate.

---

## Decision

**Adopt Option A.** Implement the `historical_keys[]` projection on `Foundation.Recovery` state derived from append-only kernel-audit rotation events. SignatureEvent verification reads the projection at the SignatureEvent's `CapturedAt` timestamp to find the operator's then-current public-key.

### Projection schema

```csharp
namespace Sunfish.Foundation.Recovery;

// New projection (read-only; computed from audit stream)
public sealed record HistoricalKeysProjection
{
    public required IdentityRef OperatorIdentity { get; init; }
    public required IReadOnlyList<HistoricalKeyEntry> Entries { get; init; }   // ordered by ValidFrom
}

public sealed record HistoricalKeyEntry
{
    public required OperatorDeviceKeyId KeyId { get; init; }
    public required SignatureAlgorithm Algorithm { get; init; }                // per ADR 0004
    public required byte[] PublicKey { get; init; }                            // raw public key bytes (algorithm-specific encoding)
    public required DateTimeOffset ValidFrom { get; init; }                    // when this key first became active
    public DateTimeOffset? ValidUntil { get; init; }                           // when this key was rotated out; null = current
    public required KeyRotationReason RotationReason { get; init; }            // why this entry's predecessor was rotated
    public required AuditEventId RotationEventAuditRef { get; init; }          // pointer into kernel-audit
    public required HistoricalKeyEntryStatus Status { get; init; }             // Active / RotatedOut / Compromised
}

public enum KeyRotationReason
{
    InitialIssuance,                  // first-time issuance for a new operator
    RoutineLifecycle,                 // scheduled rotation (e.g., 24-month rotation policy)
    DeviceLost,                       // device lost; rotation is part of recovery
    DeviceReplaced,                   // device replaced (upgrade / damage / corp re-issuance)
    SocialRecovery,                   // ADR 0046 48a — multi-sig social recovery executed
    PaperKeyFallback,                 // ADR 0046 48c — paper key was activated to bootstrap new device-key
    AlgorithmRotation,                // ADR 0004 algorithm agility — same operator, new algorithm
    SuspectedCompromise,              // operator or admin requested rotation due to suspected compromise
    AdminForcedRotation,              // tenant admin forced rotation (governance)
    OperatorOffboarding,              // operator left organization; key revoked permanently
}

public enum HistoricalKeyEntryStatus
{
    Active,                           // ValidUntil is null; this is the current key
    RotatedOut,                       // ValidUntil is set; key is no longer in use but signatures captured under it remain valid
    Compromised,                      // discovered compromised; signatures captured under it after CompromiseDetectedAt are SUSPECT
}

// Resolved by ITaxonomyResolver against Sunfish.Operator.RotationReason@1.0.0 OR enum ⟷ taxonomy mapping
// (TaxonomyClassification preferred for civilian extensibility; enum acceptable for v1)
```

### Kernel-audit rotation event types (additions per this ADR)

The following audit record types are added to `Sunfish.Kernel.Audit` per ADR 0049:

| Audit record type | Emitted on | Carries |
|---|---|---|
| `OperatorKeyIssued` | initial device-key issuance for a new operator | OperatorIdentity, KeyId, PublicKey, Algorithm, IssuedAt, IssuedBy |
| `OperatorKeyRotated` | any rotation (covers all KeyRotationReason values except SuspectedCompromise + Compromised) | OperatorIdentity, OldKeyId, NewKeyId, NewPublicKey, NewAlgorithm, RotatedAt, RotationReason, RotatedBy |
| `OperatorKeyMarkedCompromised` | compromise detection (admin or system) | OperatorIdentity, KeyId, CompromiseDetectedAt, ReportedBy, EvidenceRef |

These join the 5 audit record types ADR 0054 already adds to kernel-audit (Consent + 4 signature lifecycle events).

### Verification flow (canonical)

```csharp
public interface IHistoricalKeyResolver
{
    HistoricalKeyEntry? ResolveKeyAt(IdentityRef operatorIdentity, DateTimeOffset capturedAt);
    SignatureValidityReason VerifySignatureWithHistoricalKey(
        SignatureEvent signature,
        IdentityRef operatorIdentity);
}

// Pseudo-code for verification flow:
public SignatureValidityReason VerifySignatureWithHistoricalKey(
    SignatureEvent signature,
    IdentityRef operatorIdentity)
{
    var historical = _resolver.ResolveKeyAt(operatorIdentity, signature.CapturedAt);
    if (historical is null) return SignatureValidityReason.WitnessKeyRevoked; // no key existed at that time

    if (historical.Status == HistoricalKeyEntryStatus.Compromised
        && signature.CapturedAt >= historical.CompromiseDetectedAt)
    {
        // signed with a key that was already known compromised at capture time; treat as invalid
        return SignatureValidityReason.WitnessKeyRevoked;
    }

    // Verify signature.DeviceAttestation.Envelope using historical.PublicKey + historical.Algorithm
    var ok = SignatureEnvelope.Verify(
        signature.DeviceAttestation.Envelope,
        historical.PublicKey,
        historical.Algorithm,
        signature.DeviceAttestation.CanonicalPayloadHash);

    if (!ok) return SignatureValidityReason.SignatureCorrupted;
    return SignatureValidityReason.SignatureValid;
}
```

### Compatibility with multi-sig social recovery (ADR 0046 48a)

When ADR 0046's 48a recovery flow executes (3-of-5 social recovery quorum bootstraps a new device-key for the operator):

1. Recovery state machine emits `OperatorKeyIssued` with `KeyRotationReason.SocialRecovery` for the new key
2. Recovery state machine emits `OperatorKeyRotated` for the old key (transitions `ValidUntil` to recovery timestamp)
3. Audit-trail captures the recovery quorum (which 3 of the 5 friends ratified) — already in ADR 0046's audit emission
4. `historical_keys[OperatorIdentity]` projection auto-updates with both events
5. Future signatures validate against the new key; past signatures validate against the old key

Critical: the 48a recovery process **does not invalidate signatures** captured under the rotated-out key. Only `OperatorKeyMarkedCompromised` invalidates signatures captured after the compromise-detection timestamp.

### Compatibility with paper-key fallback (ADR 0046 48c)

When ADR 0046's 48c paper-key fallback bootstraps a new device-key:

1. Recovery state machine emits `OperatorKeyIssued` with `KeyRotationReason.PaperKeyFallback`
2. Old key's `ValidUntil` set to fallback timestamp
3. Paper-key consumed (one-shot per ADR 0046)

Same forensic property as 48a: past signatures remain valid against the old key.

### Compatibility with algorithm rotation (ADR 0004)

Per ADR 0054 Amendment A2 + ADR 0004 algorithm-agility:

- `HistoricalKeyEntry.Algorithm` is per-entry (different historical entries may have different algorithms)
- `OperatorKeyRotated` with `KeyRotationReason.AlgorithmRotation` covers algorithm transitions for the same operator
- Verification uses the entry's algorithm — a 2026 signature verifies under Ed25519 even after the operator rotates to PQ-Mode-A in 2030

### Sync semantics under ADR 0028 CRDT

`historical_keys[OperatorIdentity]` is a CRDT G-Set of `HistoricalKeyEntry` records keyed by `KeyId`. Late-arriving offline rotation events merge naturally:

- New entries always added (G-Set property)
- Concurrent issuances of the same `KeyId` are impossible by Foundation.Recovery construction (KeyId is content-addressed against the public-key bytes + issuance audit event)
- `ValidUntil` updates: handled as separate compaction projection over the audit stream (each rotation event updates the predecessor entry's `ValidUntil`)
- `Status` updates: `OperatorKeyMarkedCompromised` is a separate event class; convergence is monotonic (Compromised wins over Active/RotatedOut)

---

## Consequences

### Positive

- ADR 0054 Amendment A3 fulfilled; kernel-signatures Stage 06 build can begin
- 10-year-retention signature verification has a substrate-only answer
- Multi-sig social recovery + paper-key fallback compose cleanly (no special-case verification logic)
- Algorithm rotation per ADR 0004 composes cleanly (per-entry algorithm)
- Forensic dispute path is deterministic: any party reading the substrate can verify any signature
- 3 new audit record types extend ADR 0049 substrate vocabulary; no structural change

### Negative

- Foundation.Recovery package gains a new projection type + 3 audit emission paths
- Verifier code path adds dependency on historical-key resolution (mitigated: resolver is in-process for Anchor; cached projection in Bridge)
- Schema evolution: future addition of new `KeyRotationReason` values requires care (additive only; tombstone old enum values per Foundation.Taxonomy semantics if those reasons later become a TaxonomyClassification)

### Trust impact / Security & privacy

- Historical-keys projection is **public to authorized verifiers within the tenant**. Per-operator history reveals rotation cadence, which is mildly sensitive but already implicitly available via audit substrate consumers
- `OperatorKeyMarkedCompromised` is the strongest invalidation primitive; admins must use it carefully (compromise detection is a high-impact admin-only action)
- The projection itself does NOT store private keys — only public keys + metadata. Private keys remain in Foundation.Recovery's existing custody primitives

---

## Compatibility plan

### Existing callers / consumers

No production code consumes operator-key history today. ADR 0054's Stage 06 build (kernel-signatures) is the first consumer.

### Affected packages

| Package | Change |
|---|---|
| `packages/foundation-recovery` (existing; workstream #15 Phase 1 shipped) | **Modified** — adds `HistoricalKeysProjection` + `HistoricalKeyEntry` + `KeyRotationReason` + `IHistoricalKeyResolver` |
| `packages/kernel-audit` (existing) | **Modified** — adds 3 typed audit record subtypes (`OperatorKeyIssued`, `OperatorKeyRotated`, `OperatorKeyMarkedCompromised`) |
| `packages/kernel-signatures` (planned per ADR 0054) | **Eventual consumer** — verification flow consults `IHistoricalKeyResolver` |

### ADR amendments triggered by this ADR

1. **ADR 0046 confirmation.** This ADR amends ADR 0046's Phase 1 surface by adding a projection. ADR 0046 itself does not need re-acceptance; the amendment is additive (new projection on existing state machine).
2. **ADR 0049 audit substrate confirmation.** 3 new audit record subtypes added per the table above. No structural change to audit substrate; new vocabulary only.
3. **ADR 0028 CRDT consumer confirmation.** `historical_keys[]` is a G-Set under the existing CRDT substrate; no engine change.
4. **ADR 0054 Amendment A3 fulfilled.** This ADR is the deliverable.

---

## Implementation checklist

- [ ] `Sunfish.Foundation.Recovery.HistoricalKeysProjection` + `HistoricalKeyEntry` + `KeyRotationReason` + `HistoricalKeyEntryStatus` types defined
- [ ] `IHistoricalKeyResolver` interface + `InMemoryHistoricalKeyResolver` reference implementation
- [ ] 3 audit record types added to `Sunfish.Kernel.Audit` (`OperatorKeyIssued`, `OperatorKeyRotated`, `OperatorKeyMarkedCompromised`)
- [ ] Foundation.Recovery state machine emits `OperatorKeyIssued` on every issuance (initial + recovery + algorithm rotation)
- [ ] Foundation.Recovery state machine emits `OperatorKeyRotated` on every rotation (old key's `ValidUntil` set; new key's entry created)
- [ ] Foundation.Recovery surfaces an admin-callable `MarkOperatorKeyCompromised(KeyId, CompromiseDetectedAt, EvidenceRef)` method that emits `OperatorKeyMarkedCompromised`
- [ ] CRDT G-Set semantics for `historical_keys[OperatorIdentity]` — projection rebuilds on audit-stream replay; late-arriving offline events merge correctly
- [ ] **Acceptance test 1 (3-rotation chain):** simulate 3 rotations across simulated time period; signature captured under key v1 still verifies after key rotates to v2 + v3
- [ ] **Acceptance test 2 (social recovery composition):** simulate ADR 0046 48a 3-of-5 social recovery; verify rotation event captures `KeyRotationReason.SocialRecovery`; verify pre-recovery signature still validates
- [ ] **Acceptance test 3 (paper-key fallback composition):** simulate ADR 0046 48c paper-key activation; verify rotation event captures `KeyRotationReason.PaperKeyFallback`; verify pre-recovery signature still validates
- [ ] **Acceptance test 4 (algorithm rotation):** simulate operator rotating from Ed25519 to PQ-Mode-A; verify pre-rotation Ed25519 signature still validates under historical-key entry with Ed25519 algorithm
- [ ] **Acceptance test 5 (compromise invalidation):** simulate `MarkOperatorKeyCompromised` event; verify signatures captured AFTER `CompromiseDetectedAt` return `WitnessKeyRevoked`; verify signatures captured BEFORE remain valid
- [ ] **Acceptance test 6 (offline late-arriving rotation):** simulate offline rotation on iPad; sync after 24 hours; verify projection updates correctly across both nodes
- [ ] apps/docs entry covering historical-keys projection + verification flow + recovery composition
- [ ] kitchen-sink demo: pre-rotation signature + rotation + post-rotation verification flow

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-A1 | Should `KeyRotationReason` be enum or `TaxonomyClassification` reference? | Stage 02 — recommend enum for v1 (closed set; Sunfish-owned); promote to taxonomy-backed reference if civilian-vertical extensibility demand emerges. Mapping table to a future `Sunfish.Operator.RotationReason@1.0.0` taxonomy is straightforward. |
| OQ-A2 | Verification of compromised keys — is "signed before CompromiseDetectedAt" sufficient, or should we treat all signatures by a compromised key as suspect? | Stage 02 — recommend: pre-detection signatures remain valid (presumption of integrity until evidence shows otherwise); admin can additionally call `RevokeSignaturesByCompromisedKey(KeyId, RevocationReason)` to mass-revoke if forensics warrants. The substrate distinguishes between "key compromised" (default: pre-detection signatures valid) and "signatures revoked due to compromise" (explicit admin action). |
| OQ-A3 | Cross-tenant operator (e.g., property manager works for multiple tenant LLCs simultaneously) — separate `IdentityRef` per tenant? | Out-of-scope for this ADR; per-tenant `IdentityRef` is the existing Foundation.Identity convention. |
| OQ-A4 | Long-term archival: after 10-year retention expires + tenant chooses to delete signatures, do we delete historical-keys entries too? | Stage 02 — recommend: historical-keys projection retains entries indefinitely (small data footprint; ~256 bytes per rotation); deletion follows tenant-data-deletion policy (per ADR 0049 audit retention rules). |
| OQ-A5 | Bridge-side (hosted-node) verification — does the verifier consult Anchor's historical-keys projection or its own? | Each Anchor + Bridge syncs the same projection via ADR 0028 CRDT; verification works on either. Bridge-side verification used for cross-tenant Sunfish-to-Sunfish workflows or for emailed-PDF lookback flows. |
| OQ-A6 | If a signature's `CapturedAt` is **before** any historical-key entry exists for the operator (clock skew or pre-issuance edge case), what's the verification result? | Recommend: `WitnessKeyRevoked` with diagnostic note "no key existed at signature time"; this is correct because no key was authorized to sign at that moment. |

---

## Revisit triggers

This ADR should be re-evaluated when any of the following fire:

- **Algorithm rotation tested in production** — first time an operator rotates from Ed25519 to PQ algorithm; validate that historical-key projection handles correctly.
- **Compromise detection in production** — first `MarkOperatorKeyCompromised` event fired; review whether the substrate's response is correct.
- **CRDT merge anomaly** — late-arriving offline rotation events produce inconsistent historical-keys projections across nodes.
- **Forensic dispute that exercises the historical-keys lookup** — first court case where historical-key resolution is challenged; review against the dispute's specific requirements.
- **Multi-sig social recovery is invoked** — first time ADR 0046 48a recovery executes; validate audit-event emission + projection update.
- **Paper-key fallback is invoked** — first time ADR 0046 48c paper-key fallback executes.
- **Civilian extensibility request** — vertical-specific KeyRotationReason needed (e.g., regulated industry-specific); promote to taxonomy.
- **Cross-tenant operator scenarios** — first multi-tenant operator concurrent rotation conflict.

---

## References

### Predecessor + sister ADRs

- [ADR 0028](./0028-crdt-engine-selection.md) — CRDT substrate; G-Set semantics for historical-keys projection
- [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — Key-loss recovery; this ADR amends it with the historical-keys projection
- [ADR 0049](./0049-audit-trail-substrate.md) — Audit substrate; 3 new audit record subtypes added
- [ADR 0054](./0054-electronic-signature-capture-and-document-binding.md) — Electronic Signatures; Amendment A3 drives this ADR
- [ADR 0004](./0004-post-quantum-signature-migration.md) — Algorithm-agility for crypto primitives
- [ADR 0056](./0056-foundation-taxonomy-substrate.md) — Foundation.Taxonomy substrate (potential future home for KeyRotationReason values)

### Roadmap and specifications

- [ADR 0054 council review](../../icm/07_review/output/adr-audits/0054-council-review-2026-04-29.md) — §5 A3 amendment that drives this ADR
- [ADR 0046 base specification](./0046-key-loss-recovery-scheme-phase-1.md) — substrate this ADR amends

### Existing code

- `packages/foundation-recovery/` — existing Foundation.Recovery package (workstream #15 Phase 1 shipped via PR #223)
- `packages/kernel-audit/` — audit substrate consumer
- `packages/kernel-security/` — Ed25519 + signing primitives
- `packages/kernel-signatures/` (planned per ADR 0054) — verification consumer

### External

- ADR 0046 §"Recommended Phase 1: 48a + 48c + 48f" (multi-sig social recovery + paper-key fallback + post-MVP biometric extensibility)
- RFC 5280 (X.509 PKI) — referenced in Option C rejection rationale only

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options considered: projection (A), embedded-per-event (B), external CA (C). Option A chosen with explicit rejection rationale.
- [x] **FAILED conditions / kill triggers.** 8 revisit triggers, each tied to externally-observable signals.
- [x] **Rollback strategy.** No production code consumes historical-keys projection today (kernel-signatures is the first consumer; not yet built). Rollback = revert this ADR + revert projection-related code in Foundation.Recovery + revert 3 audit subtype additions in kernel-audit.
- [x] **Confidence level.** **HIGH.** Composes well-understood substrates (ADR 0046 + ADR 0049 + ADR 0028). Verification flow is deterministic + algorithm-agnostic. Risk surface is in CRDT-merge edge cases (covered by acceptance test 6) and compromised-key handling (covered by OQ-A2 resolution).
- [x] **Anti-pattern scan.** None of AP-1 (unvalidated assumptions — flow specified), AP-3 (vague success — 6 acceptance tests), AP-9 (skipping Stage 0 — 3 options sparred), AP-21 (assumed facts — RFC 5280 cited; CRDT G-Set semantics cited). 
- [x] **Revisit triggers.** 8 conditions with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 14 specific tasks. Fresh contributor reading this ADR + ADR 0046 + ADR 0049 + ADR 0054 should be able to implement projection without ambiguity.
- [x] **Sources cited.** ADR 0028, 0046, 0049, 0054, 0004, 0056 referenced. RFC 5280 cited. ADR 0054 council review cited as origin of Amendment A3.
