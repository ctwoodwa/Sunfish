# ADR 0058 — Vendor Onboarding Posture

**Status:** Proposed (2026-04-29; awaiting council review + acceptance)
**Date:** 2026-04-29
**Author:** XO (research session)
**Pipeline variant:** `sunfish-feature-change` (extends existing `blocks-maintenance`; cluster intake disposition reframed 2026-04-28 from NEW to EXTEND per [reconciliation review](../../icm/07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md))

**Resolves:** [property-vendors-intake-2026-04-28.md](../../icm/00_intake/output/property-vendors-intake-2026-04-28.md); cluster workstream #18 (Vendors EXTEND).

---

## Context

The property-operations cluster needs a vendor lifecycle that supports BDFL property-management workflows: hire a plumber, send them a job, get them paid, generate a 1099-NEC at year end. Existing `packages/blocks-maintenance/` already ships `Vendor` + `VendorSpecialty` + `VendorStatus` covering ~85% of the basic entity (per the cluster reconciliation review, disposition #18). What's missing is the **onboarding posture**: how does a brand-new vendor get from "BDFL just got their phone number" to "vendor is active, receiving work orders, getting paid, eligible for year-end 1099-NEC"?

Phase 2 commercial intake's six BDFL tenants average ~10–15 vendor relationships each. None of those vendors have Sunfish accounts; many are sole proprietors (TIN = SSN, sensitive PII); each has variable-quality contact info and inconsistent administrative discipline. The onboarding mechanism must work for vendors who don't read instructions, don't sign up for accounts, and don't reliably check email — but who DO respond to magic-links sent to their phone via SMS or email.

Cross-cutting constraints:

- **PII protection.** TIN (W-9) is SSN-class for sole proprietors. Field-level encryption at rest under per-tenant key; access requires audit-logged capability; only the owner (BDFL) + tax-advisor delegate can read.
- **No Sunfish account required.** A vendor receiving a magic-link work order is an *anonymous* capability holder — they prove possession of the link, not identity to a Sunfish IdP. ADR 0043 trust-model captures this T-tier.
- **1099-NEC eligibility.** The IRS rule fires at $600/year per vendor; the substrate must aggregate annual payments per TIN and produce 1099-NEC-ready data. (Aggregation logic itself is `blocks-tax-reporting` in Phase 2.3; this ADR ships the data substrate.)
- **Vendor-facing UI lives on Bridge** (Zone C hybrid SaaS per ADR 0031). Anchor (BDFL's local-first node) is operator-facing; Bridge's hosted-node-as-SaaS surface serves the public-input boundary (vendor's secure form, leaseholder's application form, public listing inquiry).

---

## Decision drivers

- **Existing block coverage is high but incomplete.** `blocks-maintenance.Vendor` has identity + contact + status; no W-9 capture, no `VendorContact` (multi-contact), no `VendorPerformanceRecord` (event log), no magic-link onboarding flow.
- **TIN PII discipline is non-negotiable.** Compliance + reputational risk; mishandled TIN is a real-world disaster (identity theft + civil suits).
- **Onboarding friction is the dominant adoption barrier.** Each additional click-to-complete step reduces vendor follow-through ~30% (industry heuristic; cited in marketing-funnel literature, not load-bearing). Magic-link single-step is the target; the system must work even when the vendor never opens a follow-up email.
- **Capability gradient maps to ADR 0043's trust catalog.** Three vendor-facing tiers: Anonymous (no W-9, no work orders) → vendor (W-9 returned, can receive magic-link work orders) → vendor-with-portal (optional Bridge account; future Phase 4+).
- **Existing cluster decisions hold.** ADR 0049 (audit substrate), ADR 0051 (payments — Money type for vendor 1099-NEC aggregation), ADR 0052 (messaging — magic-link delivery), ADR 0054 (signatures — vendor signs W-9 acknowledgment), ADR 0056 (taxonomy — VendorSpecialty migration to taxonomy ref). All compose; no new substrates required.

---

## Considered options

### Option A — Extend `blocks-maintenance` with onboarding-posture types [RECOMMENDED]

Add to existing `blocks-maintenance/`:
- `VendorContact` child entity (multi-contact per vendor)
- `VendorPerformanceRecord` event log entity (event-sourced via ADR 0049 audit substrate)
- `VendorOnboardingState` lifecycle enum: `Pending → W9Requested → W9Received → Active → Suspended → Retired`
- `W9Document` entity with field-level-encrypted TIN per ADR 0008 + ADR 0046 per-tenant key
- `VendorMagicLink` value type (one-time-use token per ADR 0052 ThreadToken pattern; 14-day TTL by default)

Bridge-hosted secure form: vendor receives `https://bridge.sunfish.dev/vendor/onboard/{magic-link-token}` → fills W-9 + ACH info → form posts to kernel via authenticated HTTPS + audit emission.

- **Pro:** Zero new packages; aligns with cluster reframing (#18 EXTEND not NEW); reuses ADR 0052 magic-link pattern + ADR 0046 per-tenant keys; lowest-churn path
- **Pro:** Existing `Vendor` callers (e.g., work-order assignment) keep working; new fields are additive
- **Con:** `blocks-maintenance` continues to grow; eventually warrants a `blocks-vendors` split if the vendor surface dominates the maintenance surface (probably Phase 3+)

**Verdict:** Recommended.

### Option B — Create new `blocks-vendors` package

Move vendor types out of `blocks-maintenance` into dedicated `blocks-vendors`. Onboarding lives there.

- **Pro:** Cleaner package separation; vendor concerns don't bleed into maintenance
- **Con:** Cluster reframing already settled `blocks-maintenance` is the home (per UPF Rule 4 reconciliation); reverting that adds churn without changing functionality
- **Con:** `Vendor` in `blocks-maintenance` already has live consumers (work-order assignment); moving forces api-change pipeline + version bumps

**Verdict:** Rejected. The "split when growth warrants" trigger is real but premature.

### Option C — Treat vendor onboarding as a `dynamic-forms` substrate consumer (per ADR 0055)

Don't define `W9Document` + `VendorOnboardingState` as concrete types. Use ADR 0055's dynamic-forms substrate so admins can configure their own W-9 form shapes per jurisdiction.

- **Pro:** Maximum flexibility; matches ADR 0055's framing for admin-defined types
- **Con:** ADR 0055 Phase 1 substrate not yet shipped; gating an ADR on a future substrate is a layering inversion
- **Con:** TIN handling discipline requires concrete types — JSONB dynamic-form data resists field-level encryption + capability-audited read patterns
- **Con:** W-9 form is a federal IRS form with a pinned shape; jurisdictional variability isn't real for this artifact (FATCA Form W-8 series for foreign vendors is a separate concrete type, not a dynamic-form variant)

**Verdict:** Rejected. Dynamic-forms is the right pattern for forms that genuinely vary per-tenant; W-9 isn't one.

---

## Decision

**Adopt Option A.** Extend `blocks-maintenance` with vendor onboarding-posture types. Concrete schemas; field-level encryption for TIN; magic-link onboarding via ADR 0052 messaging substrate; event-sourced performance log per ADR 0049; capability gradient per ADR 0043.

### Initial contract surface (additions to `blocks-maintenance`)

```csharp
namespace Sunfish.Blocks.Maintenance.Models;

// Extends existing Vendor record (additive; new init-only fields)
public sealed record Vendor
{
    // ... existing fields ...
    public required VendorOnboardingState OnboardingState { get; init; } // NEW per this ADR
    public W9DocumentId? W9 { get; init; }                                // NEW; null if W9 not yet received
    public PaymentPreference? PaymentPreference { get; init; }            // NEW; ACH | Check | Zelle | Other (per ADR 0051)
    public IReadOnlyList<TaxonomyClassification> Specialties { get; init; } = []; // NEW per ADR 0056 — replaces VendorSpecialty enum
    public IReadOnlyList<VendorContactId> Contacts { get; init; } = [];   // NEW; multi-contact support
}

public enum VendorOnboardingState
{
    Pending,        // BDFL just added vendor; nothing requested yet
    W9Requested,    // Magic-link sent; vendor hasn't responded
    W9Received,     // W-9 returned; not yet activated
    Active,         // Receiving work orders + getting paid
    Suspended,      // Temporary hold (insurance lapse, performance issue)
    Retired,        // Decommissioned (no new work orders; historical records preserved for IRS retention)
}

public sealed record VendorContact
{
    public required VendorContactId Id { get; init; }
    public required VendorId Vendor { get; init; }
    public required string Name { get; init; }
    public required string RoleLabel { get; init; }     // "Owner", "Dispatcher", "Field Tech"
    public string? Email { get; init; }
    public string? SmsNumber { get; init; }
    public bool IsPrimaryForVendor { get; init; }
    public IReadOnlyDictionary<PropertyId, bool> PrimaryForProperty { get; init; } // per-property override
}

public sealed record VendorPerformanceRecord
{
    public required VendorPerformanceRecordId Id { get; init; }
    public required VendorId Vendor { get; init; }
    public required VendorPerformanceEvent Event { get; init; }   // see enum below
    public required DateTimeOffset OccurredAt { get; init; }
    public required ActorId RecordedBy { get; init; }
    public WorkOrderId? RelatedWorkOrder { get; init; }
    public string? Notes { get; init; }
}

public enum VendorPerformanceEvent
{
    Hired, JobCompleted, JobNoShow, JobLate, JobCancelled, RatingAdjusted, InsuranceLapse, Suspended, Retired,
}

public sealed record W9Document
{
    public required W9DocumentId Id { get; init; }
    public required VendorId Vendor { get; init; }
    public required string LegalName { get; init; }
    public required string? DbaName { get; init; }
    public required W9TaxClassification TaxClassification { get; init; }  // Individual | LLC | SCorp | CCorp | Partnership | etc.
    public required EncryptedField TinEncrypted { get; init; }            // SSN or EIN; encrypted under tenant DEK per ADR 0046
    public required PostalAddress Address { get; init; }
    public required SignatureEventId SignatureRef { get; init; }          // per ADR 0054 — vendor signed acknowledgment
    public required DateTimeOffset ReceivedAt { get; init; }
    public DateTimeOffset? VerifiedAt { get; init; }
    public ActorId? VerifiedBy { get; init; }
}

public enum W9TaxClassification { Individual, LLC, SCorp, CCorp, Partnership, Trust, Other }

public sealed record VendorMagicLink
{
    public required VendorMagicLinkId Id { get; init; }
    public required VendorId Vendor { get; init; }
    public required string TokenHash { get; init; }   // HMAC-SHA256 over secret + vendor_id + issued_at; hash stored, plaintext is single-use only
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }   // default IssuedAt + 14 days
    public required MagicLinkPurpose Purpose { get; init; }   // OnboardForm | WorkOrderResponse | ReinviteW9
    public DateTimeOffset? ConsumedAt { get; init; }
    public string? ConsumedFromIp { get; init; }
}

public enum MagicLinkPurpose { OnboardForm, WorkOrderResponse, ReinviteW9 }

public readonly record struct VendorContactId(Guid Value);
public readonly record struct VendorPerformanceRecordId(Guid Value);
public readonly record struct W9DocumentId(Guid Value);
public readonly record struct VendorMagicLinkId(Guid Value);
```

`EncryptedField` is provided by `Foundation.Recovery` per-tenant-key wrapping (ADR 0046). Read access goes through `IFieldDecryptor` which checks the caller's capability (per ADR 0032) and emits an audit record per ADR 0049 every time the TIN is decrypted.

### Onboarding flow

1. **BDFL creates `Vendor`** in Anchor with name + email/SMS + initial specialties. `OnboardingState = Pending`. Audit emit `VendorCreated`.
2. **BDFL clicks "Send W-9 Request"** in Anchor UI. Backend mints `VendorMagicLink` with `Purpose = OnboardForm` + 14-day TTL; sends magic-link email/SMS via ADR 0052 messaging substrate. `OnboardingState = W9Requested`. Audit emit `VendorMagicLinkIssued` + `VendorOnboardingStateChanged`.
3. **Vendor receives magic-link** → clicks → lands on Bridge form `https://bridge.<tenant>.sunfish.dev/vendor/onboard/{token}`.
4. **Bridge form validates token** (HMAC + TTL + not-yet-consumed) → vendor fills W-9 fields + ACH details + signs acknowledgment via ADR 0054 `SignatureEvent` capture (`SignatureScope = Sunfish.Signature.Scopes/vendor-w9-acknowledgment` per ADR 0056) → form submits.
5. **Bridge posts to kernel** → kernel encrypts TIN under tenant DEK → creates `W9Document` → links to `Vendor` → flips `OnboardingState = W9Received`. Audit emit `W9DocumentReceived` + `SignatureCaptured`.
6. **BDFL reviews W-9 in Anchor** → clicks "Activate Vendor" → `OnboardingState = Active`. Vendor can now receive work orders. Audit emit `VendorActivated`.

### Capability gradient (ADR 0043 trust-model addendum)

| Tier | Identity | Capabilities | Trust source |
|---|---|---|---|
| **Anonymous** | None | Receive magic-link OnboardForm; submit W-9 + ACH | Token possession (HMAC verify) |
| **Vendor** (Active) | `VendorId` | Receive magic-link WorkOrderResponse; respond to work orders; receive payments | Token possession + W-9 on file |
| **Vendor-with-portal** (future) | Bridge account bound to `VendorId` | Browse historical work orders; download 1099-NEC; update contact info | Account auth (OIDC) + W-9 on file |

Phase 2.1 ships Anonymous + Vendor tiers; Vendor-with-portal is deferred to Phase 4+ when vendor self-service demand justifies the auth surface.

### Cross-package wiring

- **`Sunfish.Foundation.Integrations.Messaging` (ADR 0052):** magic-link delivery via `IMessagingGateway.SendAsync` with `MagicLinkBody` template
- **`Sunfish.Foundation.Recovery.IFieldDecryptor` (ADR 0046):** TIN decryption — capability-checked + audit-emitting
- **`Sunfish.Kernel.Signatures.ISignatureCapture` (ADR 0054):** vendor W-9 acknowledgment signature
- **`Sunfish.Foundation.Taxonomy.ITaxonomyResolver` (ADR 0056):** validate `Vendor.Specialties` against `Sunfish.Vendor.Specialties@1.0.0` (charter to be authored per ADR 0056 starter taxonomies)

---

## Consequences

### Positive

- Existing `blocks-maintenance.Vendor` callers keep working; field additions are additive
- TIN PII discipline structural: `EncryptedField` + `IFieldDecryptor` capability check is mechanically enforced, not policy-enforced
- Magic-link onboarding works for vendors with no Sunfish account
- 1099-NEC aggregation enabled (Phase 2.3 `blocks-tax-reporting` consumes this substrate)
- ADR 0043 trust-model gets a concrete vendor-tier specification

### Negative

- `blocks-maintenance` continues to grow; future split into `blocks-vendors` is plausible Phase 3+
- `EncryptedField` + `IFieldDecryptor` must be implemented in Foundation.Recovery; if not yet there at Stage 06 build, halt-condition fires
- Bridge-hosted form is a public-input boundary; carries ADR 0043 T2 ingress risk (covered by ADR 0052 amendment A1's 5-layer defense)
- `VendorPerformanceRecord` event log size could grow large for high-churn tenants; pagination + retention policy needed Phase 2.2

### Trust impact / Security & privacy

- **TIN encryption:** field-level under per-tenant DEK; never stored plaintext at rest
- **TIN read access:** capability-gated (only owner + tax-advisor delegates per ADR 0032); every read emits audit record
- **Magic-link tokens:** HMAC-SHA256 (mirrors ADR 0052 ThreadToken pattern); 14-day TTL; single-use; revocable
- **Bridge-hosted form:** TLS-only; provider signature verify on inbound webhook; ADR 0043 T2 boundary
- **W-9 signature:** ADR 0054 SignatureEvent — content-bound (TIN can't be retroactively altered without invalidating signature)
- **Audit trail:** every onboarding-state transition + every TIN read + every magic-link issue/consume emits audit record per ADR 0049

---

## Compatibility plan

### Existing callers

`blocks-maintenance.Vendor` already in use by `WorkOrder.Vendor` FK (per ADR 0053). New fields are additive (init-only with defaults); existing constructors continue to work via record-shape evolution. `VendorSpecialty` enum migration to `IReadOnlyList<TaxonomyClassification>` per ADR 0056 — needs migration plan; deferred to Stage 06 hand-off.

### Affected packages

| Package | Change |
|---|---|
| `packages/blocks-maintenance` | **Modified** — adds onboarding-state types + child entities + W-9 + magic-link types |
| `packages/foundation-recovery` | **Modified** — adds `EncryptedField` + `IFieldDecryptor` if not already present |
| `packages/foundation-integrations` | **Consumed** — `IMessagingGateway` for magic-link delivery |
| `packages/kernel-signatures` | **Consumed** — `ISignatureCapture` for W-9 acknowledgment |
| `packages/foundation-taxonomy` | **Consumed** — `ITaxonomyResolver` for `Vendor.Specialties` validation |
| `accelerators/bridge` | **Modified** — adds `/vendor/onboard/{token}` form route |
| `apps/docs/blocks/maintenance/vendors.md` | **New** — onboarding posture documentation |

### Migration

`VendorSpecialty` enum → `IReadOnlyList<TaxonomyClassification>` is the only breaking shape change. Migration: emit one-time data conversion at Stage 06; existing enum values map to seeded `Sunfish.Vendor.Specialties@1.0.0` taxonomy nodes 1:1.

---

## Implementation checklist

- [ ] `blocks-maintenance.Vendor` extended with new init-only fields
- [ ] `VendorContact` + `VendorPerformanceRecord` + `W9Document` + `VendorMagicLink` entity types ship with full XML doc + nullability + `required`
- [ ] `EncryptedField` + `IFieldDecryptor` in `foundation-recovery` (or verified existing)
- [ ] Magic-link issuance + consumption + audit emission wired through `IMessagingGateway` (ADR 0052)
- [ ] Bridge route `/vendor/onboard/{token}` ships in `accelerators/bridge`
- [ ] W-9 form posts through ADR 0054 `SignatureEvent` capture with `vendor-w9-acknowledgment` scope
- [ ] `Sunfish.Vendor.Specialties@1.0.0` taxonomy charter (per ADR 0056)
- [ ] 6 new `AuditEventType` constants: `VendorCreated`, `VendorMagicLinkIssued`, `VendorMagicLinkConsumed`, `VendorOnboardingStateChanged`, `W9DocumentReceived`, `VendorActivated`
- [ ] `VendorAuditPayloadFactory` mirroring W#31 + W#19 + W#20 + W#21 patterns
- [ ] `VendorSpecialty` enum → taxonomy migration data-conversion script
- [ ] `apps/docs/blocks/maintenance/vendors.md` page covers onboarding flow + TIN handling + capability gradient
- [ ] Tests: full onboarding flow (Pending → W9Requested → W9Received → Active); magic-link expiry; TIN decryption capability check; performance-record append-only invariant

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-V1 | `EncryptedField` API surface — does Foundation.Recovery already expose this, or is it a new addition? | Verify in Stage 02; if new, scope into the Stage 06 hand-off as Phase 0. |
| OQ-V2 | Bridge route auth — does `accelerators/bridge` already have a token-only public-form pattern, or is this the first one? | Stage 02 — recommend reusing ADR 0052 ThreadToken pattern + Postmark inbound webhook signature verify (the only existing public-input route). |
| OQ-V3 | `Sunfish.Vendor.Specialties@1.0.0` charter — should it ship in this hand-off or as a separate ADR 0056 follow-up taxonomy charter? | Stage 02 — recommend bundling into Stage 06 hand-off Phase X as a charter file mirroring `Sunfish.Signature.Scopes@1.0.0`. |
| OQ-V4 | Vendor-with-portal Phase 4+ trigger — what specifically promotes the "optional Bridge account" tier from deferred to in-scope? | Revisit trigger: 5+ tenants ask for vendor self-service OR a single tenant has 50+ vendors. |
| OQ-V5 | TIN retention after vendor retired — IRS requires 4-year retention for 1099-NEC backup; how is this enforced? | Stage 02 — recommend `OnboardingState = Retired` keeps `W9Document` for 4 years post-retirement, then crypto-shredding (per ADR 0046 + GDPR Article 17 framing). |

---

## Revisit triggers

- **Foreign vendor onboarding** (W-8 instead of W-9) — currently out of scope; revisit when first international vendor relationship surfaces
- **Vendor marketplace / community-shared vendor lists** — Phase 4+ feature; revisit when 5+ tenants ask
- **`blocks-vendors` package split** — when vendor surface dominates `blocks-maintenance` (probably Phase 3+ when vendor count per tenant exceeds work-order surface complexity)
- **State + local 1099 reporting** (some states require parallel filings) — Phase 2.3 follow-up
- **Background check / insurance verification automation** — manual Phase 2; automation Phase 4+

---

## References

### Predecessor and sister ADRs

- [ADR 0008](./0008-foundation-multitenancy.md) — multi-tenancy + per-tenant DEK
- [ADR 0013](./0013-foundation-integrations.md) — provider-neutrality (magic-link delivery via Postmark)
- [ADR 0015](./0015-module-entity-registration.md) — entity-module registration
- [ADR 0028](./0028-per-record-class-consistency.md) — `Vendor` is CP-class (state-machine + lease-coordinated W-9 capture)
- [ADR 0032](./0032-capability-projection-and-attenuation.md) — TIN read capability check
- [ADR 0043](./0043-trust-model-and-threat-delegation.md) — vendor capability gradient + Bridge T2 boundary
- [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — `EncryptedField` + per-tenant DEK
- [ADR 0049](./0049-audit-trail-substrate.md) — audit emission for onboarding + TIN reads
- [ADR 0051](./0051-foundation-integrations-payments.md) — vendor payment processing + 1099-NEC aggregation
- [ADR 0052](./0052-bidirectional-messaging-substrate.md) — magic-link delivery substrate
- [ADR 0054](./0054-electronic-signature-capture-and-document-binding.md) — W-9 signature acknowledgment
- [ADR 0056](./0056-foundation-taxonomy-substrate.md) — `Vendor.Specialties` as `TaxonomyClassification` list

### Roadmap and intakes

- [Phase 2 commercial intake](../../icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md) — vendor 1099-NEC + multi-vendor coordination
- [Property-vendors intake](../../icm/00_intake/output/property-vendors-intake-2026-04-28.md) — original cluster scope
- [Cluster INDEX](../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — cluster sequencing
- [Cluster reconciliation review](../../icm/07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md) — disposition #18 (NEW → EXTEND)

### External

- [IRS Form W-9 Instructions](https://www.irs.gov/forms-pubs/about-form-w-9) — canonical TIN-collection form
- [IRS 1099-NEC Filing Requirements](https://www.irs.gov/forms-pubs/about-form-1099-nec) — $600 threshold + filing dates
- [FATCA Form W-8 Series](https://www.irs.gov/forms-pubs/about-form-w-8) — foreign-vendor variant (out of scope this ADR)

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options: extend existing block, new package, dynamic-forms substrate. Option A (extend) chosen with explicit rejection rationale for B (cluster reframing already settled `blocks-maintenance`) and C (dynamic-forms substrate not yet shipped + W-9 isn't a per-tenant variable form).
- [x] **FAILED conditions / kill triggers.** Foreign vendor onboarding, marketplace, package split, state-1099 reporting, background-check automation — each tied to externally-observable signal (international relationship surfaces; 5+ tenants ask).
- [x] **Rollback strategy.** No production data exists yet (no live tenants on `blocks-maintenance.Vendor`). Rollback = revert this ADR + revert the new field additions; existing `Vendor` reverts to its current shape.
- [x] **Confidence level.** **MEDIUM-HIGH.** Substrate composition is well-understood (consumes ADR 0046 + 0049 + 0051 + 0052 + 0054 + 0056 — all already accepted/built). TIN encryption discipline is the highest-risk surface; mitigated by `EncryptedField` capability + audit-on-read pattern. Magic-link mechanics directly mirror ADR 0052 ThreadToken pattern.
- [x] **Anti-pattern scan.** None of AP-1 (unvalidated assumptions), AP-3 (vague success), AP-9 (skipping Stage 0), AP-12 (timeline fantasy), AP-21 (assumed facts) apply. Cluster reframing settled; no novel primitives.
- [x] **Revisit triggers.** Five named with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 11 specific tasks. Stage 02 contributor reading this ADR + ADR 0046 + ADR 0052 + ADR 0054 + ADR 0056 should be able to scaffold without asking for clarification on substrate semantics.
- [x] **Sources cited.** ADR 0008, 0013, 0015, 0028, 0032, 0043, 0046, 0049, 0051, 0052, 0054, 0056 referenced. IRS Forms W-9 + 1099-NEC + W-8 cited. Cluster intake + reconciliation review cited.
