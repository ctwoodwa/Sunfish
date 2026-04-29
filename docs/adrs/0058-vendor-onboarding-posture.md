# ADR 0058 — Vendor Onboarding Posture

**Status:** Accepted (2026-04-29 by CO; council-reviewed C-borderline-fail-grade; amendments A1–A6 (required) + A7–A8 (encouraged) **landed 2026-04-29** — see §"Amendments (post-acceptance, 2026-04-29 council)")
**Date:** 2026-04-29 (Proposed) / 2026-04-29 (Accepted) / 2026-04-29 (A1–A8 landed)
**Author:** XO (research session)
**Pipeline variant:** `sunfish-api-change` for the `Vendor` record shape change (positional → init-only + `Specialty` → `Specialties`); `sunfish-feature-change` for the rest (per Amendment A2)
**Council review:** [`0058-council-review-2026-04-29.md`](../../icm/07_review/output/adr-audits/0058-council-review-2026-04-29.md) — Accept-with-amendments. Six required + two encouraged; all addressed below:
1. **A1** — Reckon with `EncryptedField` + `IFieldDecryptor` as design dependency, not a Stage-02 lookup. Resolves AP-1 (unvalidated assumption) + AP-19 (discovery amnesia).
2. **A2** — Re-pipeline Vendor record shape change as `sunfish-api-change` (positional → init-only is breaking). Resolves AP-1 (Major).
3. **A3** — Correct cross-ADR type references: `IMessagingGateway` → `IOutboundMessageGateway`; `MagicLinkBody` → introduced-here; `PaymentPreference` → introduced-here (NOT from ADR 0051 — that's `PaymentMethodReference`); `SignatureScope` string → `IReadOnlyList<TaxonomyClassification>`. Resolves AP-19 + AP-21.
4. **A4** — Fix ADR 0043 framing — its T1–T5 catalog is CI/CD threats, not vendor trust gradient. Frame the gradient stand-alone in this ADR. Resolves the category error.
5. **A5** — Define magic-link 14-day TTL divergence from ADR 0052's 90-day default + specify rate limiting. Resolves the unjustified divergence.
6. **A6** — Add three FAILED conditions / kill triggers + state-machine completeness (Suspended → Active, Retired → Active, expired-link reissue). Resolves AP-18.
7. **A7** (encouraged) — Promote OQ-V5 4-year TIN retention from deferred to in-scope (the ADR ships `Retired` state).
8. **A8** (encouraged) — Add `PostalAddress` (lives in `blocks-properties`) + `ActorId` (lives in `foundation`) cross-package dependency notes.

**Resolves:** [property-vendors-intake-2026-04-28.md](../../icm/00_intake/output/property-vendors-intake-2026-04-28.md); cluster workstream #18 (Vendors EXTEND).

> **⚠ Read amendments first.** Inline contract sketches in this ADR show the **pre-amendment** types where amendments replaced earlier shapes (e.g., `PaymentPreference` was attributed to ADR 0051 in the original; A3 reclassifies it as a NEW type introduced by this ADR; `SignatureScope` was a slash-string; A3 replaces with `IReadOnlyList<TaxonomyClassification>`). Implementation must follow the amended types, not the original sketch. The original-text is preserved for audit.

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

## Amendments (post-acceptance, 2026-04-29 council)

The council review ([`0058-council-review-2026-04-29.md`](../../icm/07_review/output/adr-audits/0058-council-review-2026-04-29.md)) graded the ADR **C (Viable, borderline-fail)** on the UPF rubric and identified two Critical APs (AP-1 phantom-types + AP-19/21 cross-ADR type-name miss) and two Major (AP-1 Vendor record breaking change + AP-13 confidence-without-evidence). The CO accepted with amendments; this section authors them. After A1–A6 land, the rubric grade lifts to **A** on re-review (per the council's "to reach A: all required + recalibrate confidence to LOW-MEDIUM" rubric).

This is the same failure mode that fired on ADRs 0051 / 0053 / 0054 council reviews — Stage 1.5 is catching cross-ADR type-name misses that the pre-acceptance audit missed because it didn't grep the sister ADRs. Process recommendation in §7 of the council review names a "cited-symbol verification pass" as a pre-acceptance audit checkbox for ADRs 0059+.

### A1 (REQUIRED) — `EncryptedField` + `IFieldDecryptor` are design dependencies, not Stage-02 lookups (resolves AP-1 + AP-19)

The original ADR states "`EncryptedField` is provided by `Foundation.Recovery` per-tenant-key wrapping (ADR 0046)" and OQ-V1 defers verification to Stage 02. **Verified by grep:** ADR 0046 has zero matches for either `EncryptedField` or `IFieldDecryptor`. `Foundation.Recovery` covers per-tenant DEK derivation in service of the **key-loss recovery scheme** (the historical-keys projection for SignatureEvent verification), not field-level encryption of structured records. The `EncryptedField` value type, the `IFieldDecryptor` capability-checked decryption interface, and the audit-emitting decrypt-on-read pattern are **not designed**.

**Resolution:** This ADR formally takes a hard dependency on substrate types that don't yet exist. The dependency is named explicitly as a phantom; resolution path:

- **Option α (RECOMMENDED): Promote to a follow-up ADR 0046-A2 amendment** authored before Stage 06 starts. The new ADR amendment specifies `EncryptedField` (value type wrapping ciphertext + nonce + key-version) and `IFieldDecryptor` (capability-checked, audit-emitting decrypt-on-read) as net-new types in `Sunfish.Foundation.Recovery`.
- **Option β: Ship the design inline in the Stage 06 hand-off as Phase 0** with a dedicated review.

**Halt condition (FAIL-1 below):** Stage 06 build does not start until the substrate types ship as Accepted (in either ADR 0046-A2 or Stage 06 Phase 0).

**Affected-packages table update:**

```
- | `packages/foundation-recovery` | **Modified** — adds `EncryptedField` + `IFieldDecryptor` if not already present |
+ | `packages/foundation-recovery` | **Modified** — adds `EncryptedField` + `IFieldDecryptor` (NEW; not yet in ADR 0046; substrate dependency per Amendment A1; **halt-condition for Stage 06**) |
```

**Cross-package wiring update:**

```
- - **`Sunfish.Foundation.Recovery.IFieldDecryptor` (ADR 0046):** TIN decryption — capability-checked + audit-emitting
+ - **`Sunfish.Foundation.Recovery.IFieldDecryptor` (NEW; not yet in ADR 0046; introduced by Amendment A1):** TIN decryption — capability-checked + audit-emitting. **Stage 06 build halts until this type exists.**
+ - **`Sunfish.Foundation.Recovery.EncryptedField` (NEW; not yet in ADR 0046; introduced by Amendment A1):** opaque value type wrapping per-tenant-DEK-encrypted byte payload + nonce + key-version. Compile-time impossible to access plaintext without `IFieldDecryptor` capability check.
```

### A2 (REQUIRED) — Re-pipeline Vendor record shape change as `sunfish-api-change` (resolves AP-1 Major)

Existing `Vendor` (verified at `packages/blocks-maintenance/Models/Vendor.cs:13-20`) is a 7-parameter **positional record**:

```csharp
public sealed record Vendor(
    VendorId Id,
    string DisplayName,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    VendorSpecialty Specialty,    // singular enum
    VendorStatus Status);
```

The ADR's target shape uses init-only properties with `required` modifiers + a list-typed `Specialties` (taxonomy refs replacing the singular enum). This is **two breaking shape changes**:

1. **Positional → init-only conversion** breaks every existing constructor invocation (`new Vendor(id, name, …, specialty, status)`).
2. **`VendorSpecialty Specialty` (singular enum) → `IReadOnlyList<TaxonomyClassification> Specialties` (collection)** breaks every accessor (`vendor.Specialty`).

The original ADR labeled this as `sunfish-feature-change` ("new fields are additive") which is wrong — positional → init-only is the canonical api-change-shape signature. **Mirror to ADR 0053 amendment A6** which forced the same relabel for the `WorkOrder` record migration.

**Pipeline variant declaration update** (in header + Compatibility plan):

```
- **Pipeline variant:** `sunfish-feature-change` (extends existing `blocks-maintenance`; …)
+ **Pipeline variant:** `sunfish-api-change` for the `Vendor` record shape change (positional → init-only + `Specialty` → `Specialties`); `sunfish-feature-change` for the rest of the surface (new entity types, magic-link flow, Bridge route).
```

**Migration section update:**

```
- `VendorSpecialty` enum → `IReadOnlyList<TaxonomyClassification>` is the only breaking shape change. …
+ Two breaking shape changes:
+   1. `Vendor` record migrates from positional (7 ctor params) to init-only with `required` fields. All existing call sites must be updated to use object-initializer syntax (`new Vendor { Id = …, DisplayName = …, … }`).
+   2. `VendorSpecialty Specialty` (single enum) → `IReadOnlyList<TaxonomyClassification> Specialties` (collection of taxonomy refs). All existing accessors (`vendor.Specialty`) break.
+ Migration path: Stage 06 hand-off updates `InMemoryMaintenanceService`, `WorkOrder.Vendor` accessors per ADR 0053, all tests, and Anchor wiring as part of the shape change. One-time data conversion at Stage 06; existing enum values map to seeded `Sunfish.Vendor.Specialties@1.0.0` taxonomy nodes 1:1.
```

### A3 (REQUIRED) — Correct cross-ADR type references (resolves AP-19 + AP-21)

The ADR uses several names that don't match the source ADRs. **Each fix verified by grep:**

#### A3.1 — `IMessagingGateway` → `IOutboundMessageGateway`

ADR 0052 line 95 declares `public interface IOutboundMessageGateway` (and a separate `IInboundMessageReceiver`). There is no `IMessagingGateway` interface anywhere in the repo or in ADR 0052.

**All three references corrected**:

```
- - **`Sunfish.Foundation.Integrations.Messaging` (ADR 0052):** magic-link delivery via `IMessagingGateway.SendAsync` with `MagicLinkBody` template
+ - **`Sunfish.Foundation.Integrations.Messaging` (ADR 0052):** magic-link delivery via `IOutboundMessageGateway.SendAsync` (per ADR 0052) with `VendorMagicLinkOutboundBody` template (NEW; introduced by this ADR — see A3.2)

- | `packages/foundation-integrations` | **Consumed** — `IMessagingGateway` for magic-link delivery |
+ | `packages/foundation-integrations` | **Consumed** — `IOutboundMessageGateway` (per ADR 0052) for magic-link delivery |

- - [ ] Magic-link issuance + consumption + audit emission wired through `IMessagingGateway` (ADR 0052)
+ - [ ] Magic-link issuance + consumption + audit emission wired through `IOutboundMessageGateway` (ADR 0052)
```

#### A3.2 — `MagicLinkBody` is introduced by THIS ADR (not by ADR 0052)

ADR 0052 does not name a `MagicLinkBody` template. This ADR introduces `VendorMagicLinkOutboundBody` as a NEW message-body type local to this substrate; ADR 0052's `IOutboundMessageGateway.SendAsync` accepts arbitrary body payloads, so this type composes without changing ADR 0052.

**Implementation checklist addition** (append):

- [ ] `VendorMagicLinkOutboundBody` record (NEW; introduced by ADR 0058 A3.2) — body shape consumed by `IOutboundMessageGateway.SendAsync`. Fields: `MagicLinkUrl: string` (constructed from token), `Purpose: MagicLinkPurpose`, `VendorDisplayName: string`, `ExpiresAt: DateTimeOffset`. NO TIN / NO PII beyond display name.

#### A3.3 — `PaymentPreference` is introduced by THIS ADR (not by ADR 0051)

ADR 0051 defines `PaymentMethodReference` (an opaque provider-tokenized handle for inbound payment cards) at line 105 — see ADR 0051 §"Initial contract surface". It does NOT define a `PaymentPreference` type. The four-value enum `ACH | Check | Zelle | Other` in this ADR is a **vendor-outbound-payout-method preference** which is conceptually different from inbound `PaymentMethodReference` (it expresses "how this vendor wants to be paid by us at year-end").

**Resolution:** Declare `PaymentPreference` as a NEW type introduced by this ADR. The original `(per ADR 0051)` annotation is dropped.

**Decision section update** (Vendor record):

```
- public PaymentPreference? PaymentPreference { get; init; }            // NEW; ACH | Check | Zelle | Other (per ADR 0051)
+ public PaymentPreference? PaymentPreference { get; init; }            // NEW (introduced by this ADR; NOT from ADR 0051 — that's PaymentMethodReference for inbound payments)
```

**New enum specification** (append to Decision section):

```csharp
/// <summary>
/// Vendor-outbound-payout method preference. Distinct from
/// <see cref="Sunfish.Foundation.Integrations.Payments.PaymentMethodReference"/>
/// (which models an INBOUND tokenized provider handle for charging tenants).
/// PaymentPreference expresses "how this vendor wants to be paid AT YEAR-END
/// for 1099-NEC purposes" — not a tokenized handle, just a preference flag.
/// </summary>
public enum PaymentPreference { Ach, Check, Zelle, Other }
```

Implementation checklist addition: `[ ] PaymentPreference enum (4 values; introduced by this ADR; NOT from ADR 0051)`.

#### A3.4 — `SignatureScope` is `IReadOnlyList<TaxonomyClassification>`, not a slash-separated string (per ADR 0054 A7)

ADR 0054 Amendment A7 (already Accepted) says `SignatureEvent.Scopes` is `IReadOnlyList<TaxonomyClassification>` referencing nodes in `Sunfish.Signature.Scopes@1.0.0`. The original ADR's `SignatureScope = "Sunfish.Signature.Scopes/vendor-w9-acknowledgment"` slash-string is wrong post-amendment.

**Onboarding flow step 4 update:**

```
- (`SignatureScope = Sunfish.Signature.Scopes/vendor-w9-acknowledgment` per ADR 0056)
+ (`SignatureEvent.Scopes` (per ADR 0054 A7) contains a single `TaxonomyClassification` referencing the `vendor-w9-acknowledgment` node within `Sunfish.Signature.Scopes@1.0.0`)
```

**Implementation checklist update:**

```
- - [ ] W-9 form posts through ADR 0054 `SignatureEvent` capture with `vendor-w9-acknowledgment` scope
+ - [ ] W-9 form posts through ADR 0054 `SignatureEvent` capture with `Scopes = [TaxonomyClassification(Sunfish.Signature.Scopes@1.0.0/vendor-w9-acknowledgment)]` per ADR 0054 A7
+ - [ ] **Charter editor change**: add `vendor-w9-acknowledgment` node to `Sunfish.Signature.Scopes@1.0.0` (currently NOT seeded — verified by grep of `icm/00_intake/output/starter-taxonomies-v1-charters-2026-04-29.md`); coordinated with ADR 0056 starter-taxonomies-v1 charter as a follow-up addition before Stage 06 ships W-9 capture.
```

### A4 (REQUIRED) — Fix the ADR 0043 trust-model framing (resolves the category error)

ADR 0043's T1–T5 catalog is the **CI/CD chain-of-permissiveness threat model** (T1=compromised maintainer credentials, T2=compromised dependency supply chain, T3=subagent regression, T4=CI-action compromise, T5=insider threat). Verified at ADR 0043 lines 113–151. It is **not** a public-facing identity/auth tier system; "Bridge T2 ingress boundary" is a category error — pulling ADR 0043 to understand "T2 boundary" yields content about npm typosquats.

**Resolution (preferred):** Frame the capability gradient as a **stand-alone vendor-trust gradient** in this ADR. Drop the ADR 0043 references in §Capability gradient and §Trust impact.

**Capability gradient section update:**

```
- ### Capability gradient (ADR 0043 trust-model addendum)
+ ### Capability gradient (vendor-trust gradient — stand-alone in this ADR)
```

The "Trust source" column already correctly cites token-possession + W-9-on-file + account-auth; no further ADR 0043 reference needed.

**Trust impact section update:**

```
- - **Bridge-hosted form:** TLS-only; provider signature verify on inbound webhook; ADR 0043 T2 boundary
+ - **Bridge-hosted form:** TLS-only; provider signature verify on inbound webhook; public-input-boundary (Bridge as Zone-C per ADR 0031). NOTE: this is NOT an ADR 0043 T-tier — ADR 0043 catalogs CI/CD threats (compromised maintainer / dependency / subagent / CI / insider), not vendor-facing public ingress.
```

**Negative-consequences section update:**

```
- - Bridge-hosted form is a public-input boundary; carries ADR 0043 T2 ingress risk (covered by ADR 0052 amendment A1's 5-layer defense)
+ - Bridge-hosted form is a public-input boundary; mitigations follow the 5-layer defense pattern from ADR 0052 amendment A1 (TLS + provider signature verify + token possession + rate limit + audit emission)
```

**Decision-drivers section update:**

```
- - **Capability gradient maps to ADR 0043's trust catalog.** Three vendor-facing tiers: …
+ - **Capability gradient is a stand-alone vendor-trust gradient** (NOT mapped to ADR 0043; that catalog is CI/CD threats). Three vendor-facing tiers: Anonymous (no W-9, no work orders) → vendor (W-9 returned, can receive magic-link work orders) → vendor-with-portal (optional Bridge account; future Phase 4+).
```

The reference to ADR 0043 in the References section is removed (it was always a category error; no remaining citation is correct).

### A5 (REQUIRED) — Define magic-link 14-day TTL divergence + rate limiting

ADR 0052's ThreadToken pattern uses **90-day TTL** (verified at ADR 0052 line 478: "TTL: 90 days by default … matches the longest reasonable conversational thread"). This ADR's 14-day `VendorMagicLink` TTL diverges with no original justification.

**Justification (now articulated):**

> Vendor onboarding magic-links carry a tighter window than ThreadTokens because (a) onboarding is a single-step transaction (vendor returns W-9 within days, not months), (b) unconsumed onboarding tokens have an attack surface (TIN-collection-form discoverable by URL guess + token), and (c) the `ReinviteW9` purpose covers the legitimate "vendor lost the link / never opened it" reissue case. 14 days reflects "two sprints' worth of follow-up time before the BDFL would naturally re-prompt the vendor anyway."

**Decision section update** (`VendorMagicLink` record annotation):

```
- public required DateTimeOffset ExpiresAt { get; init; }   // default IssuedAt + 14 days
+ public required DateTimeOffset ExpiresAt { get; init; }   // default IssuedAt + 14 days. Diverges from ADR 0052 ThreadToken 90-day default; per A5 rationale, onboarding tokens have a tighter attack surface than conversational thread continuity tokens.
```

**Rate limiting (anti-spray) specification** — append to Decision section:

```
### Magic-link rate limiting (anti-spray)

- **Per-token ceiling:** the `ConsumedFromIp` field is annotated as a list (audit-write-only); a `VendorMagicLink` accumulating attempts from >5 distinct IPs within 24h triggers token revocation + emits a `VendorMagicLinkSuspectedAbuse` audit record (FAIL-2 below). Mirrors ADR 0052 amendment A1's defense pattern.
- **Per-IP ceiling:** Bridge route applies `429 Too Many Requests` after 20 attempts/IP/hour against the `/vendor/onboard/{token}` route (regardless of token validity).
- **Audit emission:** every consumption attempt emits `VendorMagicLinkConsumed` (success) or `VendorMagicLinkConsumedRejected` (failure) with reason code (`TokenExpired`, `TokenAlreadyConsumed`, `TokenHashMismatch`, `RateLimitExceeded`).
```

Implementation checklist additions:

- [ ] `VendorMagicLinkSuspectedAbuse` + `VendorMagicLinkConsumedRejected` audit event types
- [ ] Bridge route `429` rate limit per-IP per-hour
- [ ] Token revocation on >5-distinct-IPs threshold within 24h

### A6 (REQUIRED) — FAILED conditions / kill triggers + state-machine completeness (resolves AP-18)

The original ADR's revisit triggers cover follow-on features but no **FAILED condition for the substrate itself**. Add three named:

**FAIL-1 (Stage 06 halt-condition).** TIN decryption observed in any code path that does not run through `IFieldDecryptor` capability check + audit emission → **halt Stage 06 build; emergency ADR amendment**. This is the structural-vs-policy claim from §Consequences made enforceable. Detection: code review at Stage 06 + a Roslyn analyzer (`SUNFISH_TINDISCIPLINE_001`) if A1's substrate ADR specifies one.

**FAIL-2.** `VendorMagicLink` consumed-from-IP audit field shows token consumption from **>5 distinct IPs within 24h** → revoke token + emit `VendorMagicLinkSuspectedAbuse` security alert; rotate underlying HMAC secret. Mirrors ADR 0052 amendment A1's 5-layer defense.

**FAIL-3.** `OnboardingState = Active` reached with `W9 = null` → **invariant violation**; halt build, fix path. Asserted as a unit test invariant in the Stage 06 test suite (`Vendor_CannotBeActive_WithoutW9Document` test).

**State-machine completeness (additional flows now specified):**

| Transition | Trigger | Audit emission |
|---|---|---|
| `Suspended → Active` | BDFL clicks "Reinstate Vendor" after suspension cause cleared (e.g., insurance lapse cured) | `VendorReinstated` |
| `Retired → Active` | NOT ALLOWED; retirement is terminal. Re-engagement requires creating a new `Vendor` record (preserves IRS audit trail per OQ-V5 4-year retention) | n/a |
| `W9Requested → W9Requested` (reissue) | BDFL clicks "Resend W-9 Request" — old `VendorMagicLink` revoked, new one minted with `Purpose = ReinviteW9` | `VendorMagicLinkRevoked` + `VendorMagicLinkIssued` |
| Expired-link auto-cleanup | Daily job revokes expired `VendorMagicLink` records (TTL exceeded with `ConsumedAt = null`) | `VendorMagicLinkExpired` (batch) |

Implementation checklist additions:

- [ ] FAIL-1, FAIL-2, FAIL-3 named in `Vendor` substrate XML doc as invariants
- [ ] `Vendor_CannotBeActive_WithoutW9Document` invariant unit test
- [ ] `Suspended → Active` reinstate flow + `VendorReinstated` audit event
- [ ] `Retired → Active` rejection (state-machine guard); error message documents the "create new Vendor" pattern
- [ ] `Purpose = ReinviteW9` reissue flow with old-token revocation
- [ ] `VendorMagicLinkRevoked`, `VendorMagicLinkExpired`, `VendorReinstated` audit event types
- [ ] Daily expired-link cleanup job

### A7 (ENCOURAGED) — TIN retention 4-year policy promoted from deferred to in-scope

OQ-V5's recommendation (4-year IRS retention after Retired, then crypto-shred per ADR 0046 + GDPR Article 17 framing) **is in scope** because this ADR ships the `Retired` state. Without policy enforcement, Stage 06 ships `Retired` with no retention discipline, and the gap surfaces in audit later.

**Decision section addition** (TIN retention policy):

```
### TIN retention policy

- **Active vendor:** TIN-encrypted-at-rest indefinitely (decrypt-on-read with capability + audit).
- **Retired vendor:** `W9Document` retained for **4 years from `OnboardingState = Retired` transition** (mirrors IRS 1099-NEC 4-year backup withholding documentation requirement).
- **Post-retention crypto-shred:** at 4-year + 1 day, `W9Document.TinEncrypted` is replaced with a `EncryptedField.CryptoShredded` sentinel; the per-vendor key version is rotated out of the historical-keys projection per ADR 0046; remaining `W9Document` non-TIN fields (LegalName, DbaName, address) are retained for audit.
- **Audit emission:** `W9DocumentCryptoShredded` event when shred completes; emits the `vendor_id`, `retired_at`, `shredded_at` triple.
- **Phase 2.2 implementation hand-off note:** the daily/weekly retention-sweep job ships in Phase 2.2; until then, manual operator action is the gap, named in the implementation hand-off as a known deferral.
```

OQ-V5 marked `RESOLVED` (moved to in-scope; concrete policy specified above).

### A8 (ENCOURAGED) — `PostalAddress` + `ActorId` cross-package dependency notes

The original §Affected packages does not flag two cross-package consumers:

- **`PostalAddress`** lives in `packages/blocks-properties/Models/` (verified — block-level domain type, not foundation). `W9Document.Address: PostalAddress` creates a NEW cross-package dep from `blocks-maintenance` to `blocks-properties`. This dep is unusual since `blocks-maintenance` and `blocks-properties` are sibling blocks, not consumer/substrate.
- **`ActorId`** lives in `packages/foundation/Assets/Common/` (verified — foundation-level identity primitive). `VendorPerformanceRecord.RecordedBy: ActorId` and `W9Document.VerifiedBy: ActorId?` are consumers; this is a normal foundation→block dependency.

**Affected-packages table additions:**

```
+ | `packages/blocks-properties` | **Consumed** — `PostalAddress` for `W9Document.Address` (cross-block dep; consider lifting `PostalAddress` to a shared `blocks-property-common` or `foundation-domain-types` module if the cross-block dep proliferates) |
+ | `packages/foundation` | **Consumed** — `ActorId` for `VendorPerformanceRecord.RecordedBy` and `W9Document.VerifiedBy` |
```

**Revisit-trigger addition** (one new):

- **`PostalAddress` proliferation:** if 3+ blocks (currently `blocks-maintenance` + `blocks-properties` + a third) take a dependency on `PostalAddress`, lift it to a shared module. Tracked as a follow-up; not blocking Phase 2.

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
