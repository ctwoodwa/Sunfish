# Audit Emission

`Sunfish.Blocks.PublicListings` emits 6 `AuditEventType` constants per ADR 0059 + ADR 0049 (W#28 Phase 6 + 7). Every event is signed via `IOperationSigner` + appended to `IAuditTrail` per the canonical Sunfish audit pattern.

## Event surface

| Event | Emitted by | Body keys |
|---|---|---|
| `PublicListingPublished` | `InMemoryListingRepository.UpsertAsync` (status delta into Published) | `listing_id` + `tenant` + `slug` + `headline` |
| `PublicListingUnlisted` | Same (status delta out of Published into Unlisted) | `listing_id` + `tenant` + `slug` |
| `InquirySubmitted` | (W#22 `IPublicInquiryService` boundary; emits `InquiryAccepted` via leasing-pipeline; `InquirySubmitted` is the public-listings-side event reserved for Phase 5c Bridge route) | `tenant` + `listing_id` + `client_ip` |
| `InquiryRejected` | `InquiryFormDefense.EvaluateAsync` on Fail | `tenant` + `listing_id` + `rejected_at_layer` + `reason` |
| `CapabilityPromotedToProspect` | `MacaroonCapabilityPromoter.PromoteToProspectAsync` | `tenant` + `capability_id` + `accessible_listing_count` + `issued_at` + `expires_at` + `verified_email` |
| `CapabilityPromotedToApplicant` | (W#22 `ConfirmApplicationAndPromoteAsync` is the canonical promotion; W#28 reserves the event for Bridge-route-side mirror emission in Phase 5c) | `tenant` + `application_id` + `actor` |

`InquiryAccepted` + `InquiryRejected` are reused from W#22 ADR 0057 (no duplication) — the same lifecycle event at the same boundary.

## Wiring

Each service has an opt-in audit ctor. Typical composition:

```csharp
var emitter = new PublicListingAuditEmitter(auditTrail, signer, tenant);

// Repository: emits on status delta
var repo = new InMemoryListingRepository(emitter, time: null);

// Capability promoter: emits on every successful promotion
var promoter = new MacaroonCapabilityPromoter(
    issuer, tenant, accessibleListings, emitter);

// Inquiry-form defense: emits on every rejection
var defense = new InquiryFormDefense(captcha, rate, mx, emitter);
```

When the emitter is omitted (zero-arg or pre-Phase-7 ctor), services run without audit emission — useful for tests + simple kitchen-sink scenarios.

## Status-delta semantics for listings

`InMemoryListingRepository.UpsertAsync` emits at most one event per call, based on the **delta** between prior status and new status:

| Prior | New | Event |
|---|---|---|
| (none) | `Published` | `PublicListingPublished` |
| `Draft` | `Published` | `PublicListingPublished` |
| `Unlisted` | `Published` | `PublicListingPublished` |
| `Published` | `Unlisted` | `PublicListingUnlisted` |
| `Published` | `Published` | (no event) |
| `Draft` | `Unlisted` | (no event — not a Published-tier transition) |
| any | same | (no event) |

The intent is to capture **public-visibility** transitions, not internal status churn. Operators changing minor listing fields without affecting visibility don't generate noise.

## Cross-package wiring

`PublicListingAuditEmitter` is a thin helper bundling `IAuditTrail` + `IOperationSigner` + `TenantId`. Production hosts wire all three in the composition root + share the emitter across all 3 services.

## See also

- [Overview](./overview.md)
- [Inquiry Defense](./inquiry-defense.md)
- [ADR 0049](../../../docs/adrs/0049-audit-trail-substrate.md) — Audit trail substrate
- [ADR 0059](../../../docs/adrs/0059-public-listing-surface.md) — Public listing surface
