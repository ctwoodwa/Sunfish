# Workstream #21 — Signatures + Document Binding — Stage 06 hand-off

**Workstream:** #21 (Signatures + Document Binding — cluster cross-cutting)
**Spec:** [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) (Accepted 2026-04-29; 7 amendments landed) + [ADR 0046-a1](../../docs/adrs/0046-a1-historical-keys-projection.md)
**Pipeline variant:** `sunfish-feature-change` (new substrate; sibling to kernel-audit + kernel-security)
**Estimated effort:** 12–16 hours focused sunfish-PM time
**Decomposition:** 7 phases, ~5 PRs
**Prerequisites:** W#31 Foundation.Taxonomy ✓ (PR #263) — `Sunfish.Signature.Scopes@1.0.0` seed shipped; `Foundation.Recovery` ✓ (PR #223) — per-tenant key provider; ADR 0049 audit substrate ✓

**Phase 1 scope** — kernel-signatures substrate end-to-end with InMemory implementations and **deferred** native iOS PencilKit integration (that's a separate iOS-specific hand-off when W#23 iOS Field-Capture App ships).

---

## Scope summary

Build the kernel-signatures substrate per ADR 0054:

1. **`kernel-signatures` package** — sibling to `kernel-audit` + `kernel-security`. New package per ADR 0015 conventions.
2. **`SignatureEvent` + supporting types** — `ContentHash`, `PenStrokeBlobRef`, `DeviceAttestation`, `ConsentRecord`, `SignatureRevocation` with append-only revocation log
3. **Canonicalization rule** (per ADR 0054 amendment A1) — JSON Canonical Form (RFC 8785) for SHA-256 input; pinned at substrate boundary
4. **`SignatureScope = IReadOnlyList<TaxonomyClassification>`** (per A7) — consumes W#31 `Foundation.Taxonomy` `Sunfish.Signature.Scopes@1.0.0`
5. **`SignatureEnvelope` from `Foundation.Crypto`** (per A2) — algorithm-agility container; if ADR 0004 envelope not yet shipped, halt-condition triggers
6. **Audit emission** — 5 new `AuditEventType` constants per ADR 0049 pattern (mirrors W#31 + W#19 + W#20)
7. **Concurrent-revocation merge rule** (per A4 + A5) — last-revocation-wins under partition; revocations are append-only events

**NOT in scope:** native iOS PencilKit + CryptoKit integration (deferred to W#23 iOS App hand-off); historical-keys projection ADR 0046-a1 implementation (separate hand-off; this hand-off references the projection but doesn't author it).

---

## Phases

### Phase 1 — `kernel-signatures` package scaffold + contracts (~3–4h)

Audit-then-create: `ls packages/ | grep -E "^kernel-signatures"` MUST return empty before proceeding (per `feedback_audit_existing_blocks_before_handoff`).

New package `packages/kernel-signatures/`:
- `SignatureEventId.cs`, `ContentHash.cs`, `PenStrokeBlobRef.cs`, `PenStrokeFidelity.cs`, `CaptureQuality.cs`, `ClockSource.cs`, `Geolocation.cs`, `ConsentRecordId.cs`, `SignatureRevocation.cs`, `DeviceAttestation.cs`, `SignatureEvent.cs`
- `ISignatureCapture.cs` interface + `InMemorySignatureCapture.cs` (no-op stub for tests/demos)
- `ISignatureRevocationLog.cs` + `InMemorySignatureRevocationLog.cs` (append-only)
- `IConsentRegistry.cs` + `InMemoryConsentRegistry.cs` (UETA/E-SIGN consent gate per ADR 0054 §"Consent prerequisite")
- `KernelSignaturesEntityModule.cs` per ADR 0015
- `DependencyInjection/ServiceCollectionExtensions.cs` — `AddInMemoryKernelSignatures()`

Reference `Foundation.Crypto.SignatureEnvelope` per A2; halt-condition fires if ADR 0004 envelope isn't shipped (see § Halt conditions).

**Gate:** package builds; entity-module registers; XML doc + nullability + `required` complete.

**PR title:** `feat(kernel-signatures): Phase 1 substrate scaffold + contracts (ADR 0054)`

### Phase 2 — `ContentHash` canonicalization rule (~1–2h)

Per ADR 0054 amendment A1: pin canonical-bytes rule = **JSON Canonical Form (RFC 8785)** for structured-document hashing; **deterministic UTF-8** for plain-text; **PDF/A** for PDF documents (deterministic xref + no creation timestamps).

Implement in `packages/kernel-signatures/Canonicalization/`:
- `IContentCanonicalizer.cs` interface
- `JsonCanonicalCanonicalizer.cs` (RFC 8785 implementation; use existing `System.Text.Json` with custom property-ordering)
- `PdfACanonicalizer.cs` (Phase 1 stub: throw `NotImplementedException("PDF canonicalization deferred to W#21 Phase X")` — actual PDF/A rendering lives downstream)
- `ContentHash.Compute` overloads: `(byte[])`, `(string utf8)`, `(JsonNode)`, `(IReadOnlyDictionary<string,object?>)`

Tests (≥6): JSON-property-reorder produces same hash; whitespace-difference produces same hash for canonical JSON; UTF-8 normalization (NFC) handled; PDF stub throws cleanly with a guidance message.

**Gate:** `ContentHash.Compute` is deterministic across reorders + whitespace; PDF stub clearly marks deferred work.

**PR title:** `feat(kernel-signatures): ContentHash canonicalization (RFC 8785 + UTF-8 NFC, ADR 0054 A1)`

### Phase 3 — Append-only revocation log + concurrent-revocation merge rule (~2–3h)

Per ADR 0054 amendments A4 + A5: revocations are append-only events; concurrent revocations under AP/CRDT model (ADR 0028) merge by **last-revocation-wins** (latest `RevokedAt` in partial order; ties broken by `RevocationEventId.Value` total order).

Implement:
- `SignatureRevocation` record with `RevocationEventId`, `SignatureEventId`, `RevokedAt`, `RevokedBy IdentityRef`, `RevocationReason`
- `ISignatureRevocationLog.AppendAsync(SignatureRevocation, CancellationToken)`
- `ISignatureRevocationLog.GetCurrentValidityAsync(SignatureEventId)` — projection that scans the log + applies merge rule
- `RevocationProjection.cs` — pure function; takes log entries + returns current-valid status
- Tests: concurrent-revocation merge (two revocations from offline devices, sync at T+N hours, asserts final order); revoke-then-revoke (idempotent); revoke nonexistent signature (graceful).

**Gate:** concurrent-revocation merge produces deterministic order; `GetCurrentValidityAsync` returns expected validity.

**PR title:** `feat(kernel-signatures): append-only revocation log + concurrent merge rule (ADR 0054 A4+A5)`

### Phase 4 — `SignatureScope` integration with W#31 Foundation.Taxonomy (~1–2h)

Per ADR 0054 amendment A7: `SignatureEvent.Scopes : IReadOnlyList<TaxonomyClassification>` references nodes in `Sunfish.Signature.Scopes@1.0.0` (already seeded by W#31 PR #263).

- Update `SignatureEvent` definition to use `IReadOnlyList<TaxonomyClassification>` (already in spec but verify type import)
- Add `ISignatureScopeValidator.cs` — validates that each `TaxonomyClassification` resolves to an active node in `Sunfish.Signature.Scopes` (consumes `ITaxonomyResolver` from W#31)
- `InMemorySignatureScopeValidator.cs` — Phase 1 implementation
- Tests: valid scope resolves; tombstoned scope rejected (per W#31 governance); cross-taxonomy scope (e.g., from `Sunfish.WorkOrder.Categories`) rejected

**Gate:** scope validation works against the W#31-shipped seed; rejects out-of-taxonomy refs.

**PR title:** `feat(kernel-signatures): SignatureScope validation via Foundation.Taxonomy (ADR 0054 A7)`

### Phase 5 — Audit emission (~1–2h)

Add 5 new `AuditEventType` constants under `===== ADR 0054 — Signatures =====` divider in `packages/kernel-audit/AuditEventType.cs`:

```csharp
public static readonly AuditEventType SignatureCaptured = new("SignatureCaptured");
public static readonly AuditEventType SignatureRevoked = new("SignatureRevoked");
public static readonly AuditEventType SignatureValidityProjected = new("SignatureValidityProjected"); // emitted on each projection re-compute
public static readonly AuditEventType ConsentRecorded = new("ConsentRecorded");
public static readonly AuditEventType ConsentRevoked = new("ConsentRevoked");
```

Author `SignatureAuditPayloadFactory` mirroring W#31 + W#19 + W#20 patterns. Wire `InMemorySignatureCapture` + `InMemorySignatureRevocationLog` + `InMemoryConsentRegistry` to call `IAuditTrail.AppendAsync(...)` after each operation.

Tests (5): one per event type; assert discriminator + payload body keys.

**Gate:** 5 event types ship; factory works; audit emission verified.

**PR title:** `feat(kernel-signatures): audit emission — 5 AuditEventType + factory (ADR 0049)`

### Phase 6 — Cross-package wiring + W#19 wiring point + apps/docs (~2–3h)

Verify wiring points consumed by other workstreams:

- W#19 Phase 6 expects `kernel-signatures.ISignatureCapture` to exist for `WorkOrderCompletionAttestation` capture. Verify the interface signature matches what W#19 hand-off specifies.
- W#27 Leases EXTEND will eventually consume `kernel-signatures` for `Lease.SignatureEventRef`. Verify the type `SignatureEventRef` (= `SignatureEventId` per spec) is exported.

apps/docs:
- `apps/docs/kernel/signatures/overview.md` — substrate overview, capture flow, revocation semantics, scope validation, consent gate
- `apps/docs/kernel/signatures/integration-guide.md` — how a consumer block (Lease, WorkOrder, Inspection) integrates with the substrate

Tests: end-to-end integration test covering: capture → audit emit → revoke → projection updates → audit emit.

**Gate:** wiring points work; apps/docs builds; integration test passes.

**PR title:** `feat(kernel-signatures): cross-package wiring + apps/docs`

### Phase 7 — Archive cob-idle beacon + ledger flip (~0.5h)

This hand-off PR archives the COB idle beacon (`cob-idle-2026-04-29T20-42Z-31-built-queue-dry.md`) since W#19 + W#20 + W#21 hand-offs collectively refill the queue.

Update `icm/_state/active-workstreams.md` row #21 from `ready-to-build` → `built` after Phase 6 ships. Append entry to `## Last updated` footer.

**PR title:** `chore(icm): flip W#21 ledger row → built` (or bundled into Phase 6 PR)

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | Package scaffold + contracts | 3–4 |
| 2 | ContentHash canonicalization (RFC 8785) | 1–2 |
| 3 | Revocation log + concurrent merge | 2–3 |
| 4 | SignatureScope via Foundation.Taxonomy | 1–2 |
| 5 | Audit emission (5 AuditEventType) | 1–2 |
| 6 | Cross-package wiring + apps/docs | 2–3 |
| 7 | Ledger flip | 0.5 |
| **Total** | | **10.5–16.5h** |

---

## Halt conditions

- **`Foundation.Crypto.SignatureEnvelope` not yet shipped** (per ADR 0054 A2 + ADR 0004 dependency) → write `cob-question-*` beacon naming the gap; XO may unblock by stubbing `SignatureEnvelope` in `Foundation.Crypto` ahead of full ADR 0004 Stage 06
- **`ITaxonomyResolver` interface signature ambiguity** in W#31 substrate → `cob-question-*`
- **PDF/A canonicalization spec divergence** from RFC 8785 / library availability → halt at Phase 2; PDF rendering is deferred but if the substrate-level interface signature can't accommodate later PDF support, redesign needed
- **Concurrent-revocation merge edge case** that the partial-order rule doesn't cover (e.g., simultaneous revoke + un-revoke; spec says revocations are append-only so un-revoke isn't a thing — verify) → `cob-question-*`
- **Existing `kernel-security` overlap** discovered during scaffold (e.g., signature-related types already exist there) → `cob-question-*` flagging the collision

---

## Acceptance criteria

- [ ] `packages/kernel-signatures/` ships with full XML doc + nullability + `required`
- [ ] `ContentHash.Compute` is deterministic per RFC 8785 + UTF-8 NFC; PDF stub clearly marks deferred work
- [ ] Append-only `ISignatureRevocationLog` with deterministic concurrent-revocation merge
- [ ] `SignatureScope` validates against `Sunfish.Signature.Scopes@1.0.0` (W#31 seed); tombstoned + cross-taxonomy refs rejected
- [ ] 5 new `AuditEventType` constants in kernel-audit
- [ ] `SignatureAuditPayloadFactory` ships with one factory per event type
- [ ] `apps/docs/kernel/signatures/` overview + integration-guide pages exist
- [ ] W#19 Phase 6 wiring point (`ISignatureCapture`) is callable from `blocks-maintenance`
- [ ] All tests pass; build clean
- [ ] Ledger row #21 → `built`

---

## References

- [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — full spec + 7 amendments
- [ADR 0046-a1](../../docs/adrs/0046-a1-historical-keys-projection.md) — companion historical-keys projection
- [ADR 0056](../../docs/adrs/0056-foundation-taxonomy-substrate.md) — Foundation.Taxonomy substrate consumed for `SignatureScope`
- [W#31 hand-off](./foundation-taxonomy-phase1-stage06-handoff.md) + [W#31 addendum](./foundation-taxonomy-phase1-stage06-addendum.md) — taxonomy substrate now real
- [W#19 hand-off](./property-work-orders-stage06-handoff.md) — Phase 6 consumes `kernel-signatures.ISignatureCapture`
- ADR 0008, 0013, 0015, 0028 (CP/AP), 0046 (Foundation.Recovery for keys), 0049 (audit substrate), 0004 (algorithm-agility — `SignatureEnvelope`)
