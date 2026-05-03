# Kernel.Signatures Substrate

`Sunfish.Kernel.Signatures` is the legally-binding signature-capture substrate per [ADR 0054](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md). It sits alongside `kernel-audit` and `kernel-security`, and ships with InMemory implementations suitable for tests, demos, and kitchen-sink scenarios. Native iOS PencilKit + CryptoKit integration is deferred to W#23 (iOS Field-Capture App).

## What gets captured

A `SignatureEvent` binds five proofs together:

1. **Signer identity** — the `ActorId` who signed
2. **Document binding** — a `ContentHash` (SHA-256 over canonical bytes per ADR 0054 amendment A1)
3. **Legal scope** — one or more `TaxonomyClassification` references into `Sunfish.Signature.Scopes@1.0.0` (ADR 0054 amendment A7)
4. **Algorithm-agility** — a `SignatureEnvelope` (ADR 0054 amendment A2; full envelope semantics ship in ADR 0004 Stage 06)
5. **Capture quality** — clock source, device touch, document-reviewed-before-sign, optional pen-stroke fidelity, optional geolocation, optional device attestation

## Capture flow

```text
                              ┌───────────────────────┐
                              │  IConsentRegistry     │
                              │  (UETA / E-SIGN gate) │
                              └───────────┬───────────┘
                                          │  current consent?
                                          ▼
┌──────────────────────────┐    ┌──────────────────────────┐
│ ISignatureScopeValidator │    │  ISignatureCapture       │
│  (Sunfish.Signature.     │───▶│  • verify consent        │
│   Scopes resolver)       │    │  • verify scope          │
└──────────────────────────┘    │  • build SignatureEvent  │
                                │  • emit audit            │
                                └───────────┬──────────────┘
                                            │
                                            ▼
                                ┌──────────────────────────┐
                                │  IAuditTrail.Append      │
                                │  → SignatureCaptured     │
                                └──────────────────────────┘
```

## Revocation semantics (ADR 0054 A4 + A5)

Revocations are append-only events. Concurrent revocations under the AP / CRDT model (ADR 0028) merge by **last-revocation-wins** — latest `RevokedAt` in partial order; ties broken by total order on `RevocationEventId.Value` (Guid byte sequence).

`RevocationProjection.Project` is a pure function: every storage backend (InMemory today; EFCore + CRDT-replicated tomorrow) delegates to identical merge semantics, so verdicts stay consistent across deployment topologies.

## Scope validation (A7)

`ISignatureScopeValidator` enforces that every `TaxonomyClassification` in `SignatureEvent.Scope` resolves to an active node in `Sunfish.Signature.Scopes@1.0.0` (seeded by W#31). Four categorical failures:

| Category | Trigger |
|---|---|
| `EmptyScope` | Scope list is empty (a signature MUST have at least one scope) |
| `OutOfTaxonomy` | Classification references a different taxonomy entirely |
| `UnknownNode` | Code + version triple doesn't resolve |
| `TombstonedNode` | Node resolves but is tombstoned per ADR 0056 governance |

Validation is opt-in via the 3-arg `InMemorySignatureCapture` ctor; production hosts wire it.

## Consent gate (UETA / E-SIGN)

A `SignatureEvent` cannot be captured without a current `ConsentRecord` for the signing principal. `IsCurrentAt(now)` is true when the consent has not been revoked AND has not expired. The capture flow refuses (throws `InvalidOperationException`) when:

- No current consent exists for `(Tenant, Signer)`
- The `SignatureCaptureRequest.Consent` id doesn't match the current consent id

## Audit emission (5 `AuditEventType`)

| Event | Emitted by |
|---|---|
| `ConsentRecorded` | `IConsentRegistry.RecordAsync` |
| `ConsentRevoked` | `IConsentRegistry.RevokeAsync` (only when id resolves) |
| `SignatureCaptured` | `ISignatureCapture.CaptureAsync` (after consent + scope validation) |
| `SignatureRevoked` | `ISignatureRevocationLog.AppendAsync` (idempotent on duplicate event id) |
| `SignatureValidityProjected` | `ISignatureRevocationLog.GetCurrentValidityAsync` (every query) |

`SignatureCaptured` payload bodies carry `envelope_algorithm` + `clock_source` + `stroke_fidelity` for forensic reconstruction.

## Wiring (opt-in)

Each service has a multi-arg ctor that accepts the optional dependencies; callers compose the parts they need.

```csharp
var emitter = new SignatureAuditEmitter(auditTrail, signer, tenantId);
var consents = new InMemoryConsentRegistry(emitter, time: null);
var scopeValidator = new InMemorySignatureScopeValidator(taxonomyResolver);
var capture = new InMemorySignatureCapture(consents, scopeValidator, emitter, time: null);
var revocations = new InMemorySignatureRevocationLog(emitter, time: null);
```

Or via DI:

```csharp
services.AddInMemoryKernelSignatures();
```

The DI helper registers the three services as singletons; callers then layer in the audit emitter + scope validator at composition time.

## See also

- [Integration Guide](./integration-guide.md) — how a consumer block (Lease, WorkOrder, Inspection) integrates with the substrate
- [ADR 0054](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — Electronic-signature capture + document binding
- [ADR 0056](../../../docs/adrs/0056-foundation-taxonomy-substrate.md) — Foundation.Taxonomy substrate (used for scopes)
- [ADR 0049](../../../docs/adrs/0049-audit-trail-substrate.md) — Audit-trail substrate
