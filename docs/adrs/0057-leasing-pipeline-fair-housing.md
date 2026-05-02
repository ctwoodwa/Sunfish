---
id: 57
title: Leasing Pipeline + Fair Housing Compliance Posture
status: Accepted
date: 2026-04-29
tier: block
concern:
  - regulatory
  - audit
  - multi-tenancy
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0057 — Leasing Pipeline + Fair Housing Compliance Posture

**Status:** Accepted (2026-04-29 — ratified post-build per ledger row #22; **A1 amendment landed 2026-04-30** — see §"Amendments (post-acceptance)"). Note: the original Proposed → Accepted transition for this ADR was implicit (W#22 was authored + shipped under the assumption ADR 0057 was Accepted; the Status line not flipping at acceptance time was a process-bug). A1 ratifies + adds the structural-encryption amendment.
**Date:** 2026-04-29 (Accepted) / 2026-04-30 (A1 structural-encryption amendment)
**Resolves:** Property-ops cluster intake [`property-leasing-pipeline-intake-2026-04-28.md`](../../icm/00_intake/output/property-leasing-pipeline-intake-2026-04-28.md); cluster workstream #22. Specifies the leasing-pipeline domain block (Inquiry → Application → Approval) and the Fair Housing / FCRA compliance posture under which it ships. Composes ADR 0054 (Signatures), ADR 0055 (Dynamic Forms), ADR 0056 (Foundation.Taxonomy), ADR 0052 (Messaging), ADR 0049 (Audit), ADR 0043 (Threat Model — addended).

---

## Context

When a property is vacant, the leasing pipeline gets it filled. BDFL's property business runs this manually + via Rentler today; Phase 2 commercial intake defers Rentler-portal replacement to Phase 3, but the **non-portal pipeline pieces** — inquiry intake, criteria sending, showings, application receipt, accept/decline — are operational gaps the field-app conversation surfaced as Phase 2.1d in-scope.

This ADR is structurally distinct from prior cluster ADRs. Three reasons:

### 1. First public-input boundary

Every Sunfish workflow before this one accepts input from **known actors** (BDFL, spouse, bookkeeper, contractor, leaseholder, vendor with W-9). A prospect filling out a public inquiry form is **anonymous, untrusted, and capability-bounded**. Different trust posture; explicit threat-model treatment.

The capability progression:

```
Anonymous (filled inquiry form)
    ↓ (criteria sent + acknowledged + email verified)
Prospect (criteria-bound, email-verified, abuse-rate-limited)
    ↓ (started application; partial-save active)
Applicant (full screening data + FCRA-consent + adverse-action eligible)
```

Each promotion crosses a trust boundary that ADR 0043 must explicitly model. This ADR amends ADR 0043 (addendum) for the public-input boundary + capability promotion semantics.

### 2. Fair Housing Act exposure is binary

The FHA (and state equivalents — California FEHA, New York HRL, etc.) require **uniform application of pre-screening criteria across protected classes** (race, color, religion, sex, familial status, national origin, disability; plus state-protected classes). Selectively screening applicants by protected class is the legal exposure that ends a property-management business. Civil penalties + private rights of action + class-action exposure.

The structural defense is documentation:
- The **exact criteria document version** sent to **each prospect** on **what date**
- **Uniform application** of those criteria
- **Audit trail** showing each application was evaluated against the criteria as documented

This is a **content-hash binding mechanic identical to ADR 0054 SignatureEvent**: criteria document is versioned, content-hash-bound, prospect acknowledges via lightweight signature, evaluation references the bound version. **No selective application is structurally possible** — the substrate enforces uniformity by binding evaluation to a specific document version.

### 3. FCRA + jurisdiction-aware policy is non-negotiable

The Fair Credit Reporting Act (15 USC § 1681) requires:
- **Pre-screening consent** for credit/background checks (`CriteriaAcknowledgement` includes consent)
- **Adverse-action letters** with specific FCRA-required content when declining based on consumer-report data
- **Adverse-action delivery** within mandated timeframes (usually 60 days post-decision)

State law adds jurisdiction-specific requirements:
- **Application fee caps** (e.g., California ~$58.34 cap; some states require itemized fee disclosure)
- **Required disclosures in criteria** (lead-paint per HUD, asbestos per state, smoke-detector per state, etc.)
- **Tenant-screening law specifics** (NY rent-stabilized; CA AB 1482; WA RCW 59.18 — varies)
- **Adverse-action letter requirements** (some states require adverse-action even for non-FCRA-based declines)

Sunfish ships **jurisdiction-policy** as a pluggable taxonomy (per ADR 0056) so per-state policy compositions don't fork the substrate.

---

## Decision drivers

- **FHA exposure ends businesses.** Substrate must structurally prevent non-uniform application of criteria.
- **FCRA non-compliance creates federal liability.** Substrate must structurally support FCRA workflows (consent + adverse-action letters + delivery timelines).
- **Jurisdiction-aware policy.** Per-state compliance rules vary; substrate must compose them as a Foundation.Taxonomy product, not hardcoded enums.
- **Public-input boundary.** Anonymous prospects need explicit threat modeling (rate-limit, CAPTCHA, abuse posture).
- **Capability promotion.** Anonymous → prospect → applicant transition must be modeled as a state machine with audit trail per stage.
- **Composability.** ADR 0054 (Signatures) for criteria acknowledgement; ADR 0055 (Dynamic Forms) for application forms; ADR 0056 (Foundation.Taxonomy) for jurisdiction policy; ADR 0052 (Messaging) for adverse-action letter delivery; ADR 0049 (Audit) for full lifecycle audit trail. **No new substrate primitives required** — composes existing ADRs.
- **BDFL-property-business first.** Phase 2.1d ships BDFL's-property-states only (Utah, Colorado per intake context); broader US-state coverage in Phase 2.3.
- **Local-first.** Prospect/applicant data syncs via ADR 0028 CRDT; offline application receipt works (kitchen-sink demo).
- **Provider-neutrality (ADR 0013).** Credit/background-check providers land as `providers-screening-*` adapters (separate from this ADR's substrate scope).

---

## Considered options

### Option A — Block-tier `blocks-property-leasing-pipeline` substrate composing existing ADRs [RECOMMENDED]

Place the substrate in `packages/blocks-property-leasing-pipeline/` (cluster sibling convention per `blocks-property-*` prefix). Substrate composes:

- **ADR 0054 SignatureEvent** for `CriteriaAcknowledgement` (signature scope = `Sunfish.Signature.Scopes.consent-disclosure` + `Sunfish.Signature.Scopes.consent-background-check` + `Sunfish.Signature.Scopes.consent-credit-check`)
- **ADR 0055 Dynamic Forms** for `Application` form definition (admin-defined form per tenant; per-jurisdiction overlays via Pattern E)
- **ADR 0056 Foundation.Taxonomy** for `Sunfish.Jurisdiction.US-States.Leasing@1.0.0` starter taxonomy (per-state policy bundle)
- **ADR 0052 Messaging Substrate** for inquiry → criteria-document delivery + adverse-action letter delivery
- **ADR 0049 Audit** for full pipeline-lifecycle audit trail (inquiry-received, criteria-sent, criteria-acknowledged, application-started, application-submitted, application-reviewed, approved/declined, adverse-action-sent, etc.)
- **ADR 0028 CRDT** for prospect/applicant data sync (AP for inquiry; CP for application acceptance — see §"Per-record-class CP/AP" below)

- **Pro:** No new kernel-tier substrate. Composes 6 existing ADRs cleanly.
- **Pro:** Block-tier matches the layer of the consumer set (one cluster, one block).
- **Pro:** Pluggable jurisdiction policy via Foundation.Taxonomy enables per-state compliance without forking the block.
- **Pro:** FHA defense is structural (bind evaluation to criteria document version; substrate cannot bypass).
- **Con:** Block-tier means pipeline state machine lives in domain code, not kernel — same as Work Orders (ADR 0053), so consistent.

**Verdict:** Recommended.

### Option B — Kernel-tier `kernel-leasing-pipeline` substrate

Place in `packages/kernel-leasing-pipeline/`, sibling to kernel-audit/security/signatures.

- **Pro:** Kernel-tier weight matches FHA-exposure severity.
- **Con:** **Mismatched layer.** Leasing pipeline is a domain workflow specific to property management; not a cross-cutting primitive like signatures or audit. Future verticals (highway management, healthcare) won't have a leasing pipeline. Kernel-tier is for cross-cutting primitives that every accelerator needs.
- **Con:** Kernel-tier creates a foot-gun — non-property accelerators inherit the package whether they need it or not.

**Verdict:** Rejected. Domain workflows belong in block-tier; kernel-tier is for cross-cutting primitives.

### Option C — Foundation-tier `foundation-leasing-pipeline` (split substrate from domain)

Split into foundation-tier substrate (state machine + capability promotion + jurisdiction-policy resolver) + block-tier domain (Inquiry/Application/Showing entities).

- **Pro:** Separation of concerns; substrate can be reused for non-leasing capability-promotion flows.
- **Con:** **Premature abstraction.** No second consumer of "capability-promotion + state-machine substrate" exists. YAGNI; refactor later if a real second consumer emerges (e.g., vendor onboarding might be a candidate, but its capability surface is different enough that abstraction would over-fit to leasing).
- **Con:** Doubles the package count for one workflow.

**Verdict:** Rejected pending a second consumer. Single block-tier package per Option A.

---

## Decision

**Adopt Option A.** Place the leasing-pipeline substrate in `packages/blocks-property-leasing-pipeline/` (cluster sibling). Composes 6 existing ADRs (0054 / 0055 / 0056 / 0052 / 0049 / 0028) without introducing new kernel- or foundation-tier substrates. Jurisdiction policy ships as a Foundation.Taxonomy starter (`Sunfish.Jurisdiction.US-States.Leasing@1.0.0`).

### Substrate scope (this ADR)

- **`Inquiry` entity** — public form submission; anonymous; rate-limited
- **`CriteriaDocument` entity** — versioned, content-hash-bound (ADR 0054 ContentHash mechanic); per-LLC-tenant or per-property defaults
- **`CriteriaAcknowledgement` entity** — prospect's signed acknowledgment; references CriteriaDocument version + signature event
- **`Application` entity** — dynamic-forms-shaped (ADR 0055); per-jurisdiction overlays
- **`Showing` entity** — scheduled time slot; iCal export; right-of-entry coordination if occupied
- **`AdverseActionLetter` entity** — FCRA-compliant; messaging substrate delivery
- **`LeasingPipelineState` state machine** — Inquiry → CriteriaSent → CriteriaAcknowledged → ApplicationStarted → ApplicationSubmitted → ApplicationReviewed → Approved → LeaseDrafted → LeaseSigned → MoveInScheduled → TenancyStart (with side-branches: Declined+AdverseAction / Withdrawn / Stalled)
- **Per-record-class CP/AP positioning**: Inquiry + Showing are AP (eventually consistent, multi-leg, late-arriving offline OK); Application + CriteriaAcknowledgement + AdverseActionLetter are CP (single-source-of-truth required for FCRA + FHA audit)
- **Jurisdiction-policy resolver** — reads `Sunfish.Jurisdiction.US-States.Leasing@1.0.0` taxonomy; applies per-state policy at criteria-document generation + application-screening + adverse-action-letter generation
- **Public inquiry-form Bridge surface** — anonymous-accessible form per public listing; rate-limited; CAPTCHA-gated; abuse posture per ADR 0043 addendum

### Initial contract surface (foundation-tier types referenced; block-tier types defined)

```csharp
namespace Sunfish.Blocks.PropertyLeasingPipeline;

// Public-input entry — anonymous capability
public sealed record Inquiry
{
    public required InquiryId Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required PropertyId? PropertyOfInterest { get; init; }   // null if generic LLC inquiry (no specific property)
    public required ContactPoint ContactInfo { get; init; }          // FHIR-aligned per Pattern G; email/phone/SMS
    public PersonName? ProspectName { get; init; }                   // optional (FHA: don't require name to inquire)
    public required string Message { get; init; }                    // free-text; rate-limited
    public required InquirySource Source { get; init; }              // listing-page / direct-email / phone-logged / walk-in
    public required DateTimeOffset ReceivedAt { get; init; }
    public required InquiryStatus Status { get; init; }              // new / criteria-sent / applied / declined / abandoned
    public required ContentHash AbuseFingerprint { get; init; }      // anti-spam content-hash; rate-limit key
}

public readonly record struct InquiryId(Guid Value);

public enum InquirySource
{
    PublicListingPage,    // Bridge SSR-rendered listing
    DirectEmail,          // operator manually-entered after inquiry email
    PhoneLogged,          // operator manually-entered after phone call
    WalkIn,               // operator manually-entered for in-person
    Other                 // catch-all; rare
}

public enum InquiryStatus
{
    New,
    CriteriaSent,
    Applied,
    Declined,
    Abandoned          // 30+ days without progression; auto-archived
}

// Versioned criteria document (FHA-defense mechanism)
public sealed record CriteriaDocument
{
    public required CriteriaDocumentId Id { get; init; }
    public required CriteriaDocumentVersionId VersionId { get; init; }
    public required TenantId Tenant { get; init; }
    public required PropertyId? ScopedToProperty { get; init; }      // null = LLC-default; set = property-specific
    public required ContentHash DocumentHash { get; init; }           // ADR 0054 ContentHash (Rule 2 — UTF-8 NFC + LF)
    public required CriteriaDocumentBody Body { get; init; }          // structured criteria
    public required DateTimeOffset PublishedAt { get; init; }
    public required IdentityRef PublishedBy { get; init; }
    public required IReadOnlyList<TaxonomyClassification> ApplicableJurisdictions { get; init; } // Sunfish.Jurisdiction.US-States.Leasing@1.0.0 nodes
    public DateTimeOffset? RetiredAt { get; init; }                   // when version replaced; null = active
}

public readonly record struct CriteriaDocumentId(Guid Value);
public readonly record struct CriteriaDocumentVersionId(Guid Value);

// Structured criteria body — uniform-application-enforceable
public sealed record CriteriaDocumentBody
{
    public required IncomeMultiple MinIncomeMultiple { get; init; }   // e.g., 3.0× rent
    public required CreditScoreFloor MinCreditScore { get; init; }    // e.g., 650
    public required EvictionHistoryPolicy EvictionPolicy { get; init; }
    public required RentalHistoryRequirement RentalHistory { get; init; }
    public required SmokingPolicy SmokingPolicy { get; init; }
    public required PetPolicy PetPolicy { get; init; }
    public required OccupancyLimit OccupancyLimit { get; init; }
    public required Money ApplicationFee { get; init; }
    public required IReadOnlyList<JurisdictionDisclosure> RequiredDisclosures { get; init; }
}

// Prospect's acknowledgment (ADR 0054 lightweight signature)
public sealed record CriteriaAcknowledgement
{
    public required CriteriaAcknowledgementId Id { get; init; }
    public required InquiryId InquiryRef { get; init; }
    public required CriteriaDocumentVersionId AcknowledgedVersion { get; init; }
    public required SignatureEventId SignatureEventRef { get; init; }     // ADR 0054 signature; scope includes consent-disclosure + consent-background-check + consent-credit-check
    public required DateTimeOffset AcknowledgedAt { get; init; }
    public required ContactPoint VerifiedContactInfo { get; init; }       // contact verified pre-acknowledgment
}

// Full application (ADR 0055 dynamic-forms-shaped)
public sealed record Application
{
    public required ApplicationId Id { get; init; }
    public required InquiryId InquiryRef { get; init; }
    public required CriteriaAcknowledgementId AcknowledgementRef { get; init; }
    public required FormDefinitionRef ApplicationFormDef { get; init; }   // ADR 0055 dynamic-form definition pinned
    public required JsonbDocument FormData { get; init; }                  // ADR 0055 JSONB-stored form data
    public required ApplicationStatus Status { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public IdentityRef? ReviewedBy { get; init; }
    public ApplicationDecision? Decision { get; init; }
    public AdverseActionLetterId? AdverseActionRef { get; init; }
}

public readonly record struct ApplicationId(Guid Value);

public enum ApplicationStatus
{
    Started,                     // prospect saved partial; not yet submitted
    Submitted,                   // application complete + fee paid
    UnderReview,                 // operator actively reviewing
    Approved,
    Declined,
    Withdrawn,                   // prospect withdrew before decision
    Stalled                      // 14+ days of no operator action; flagged
}

// Application decision (precise reason — FCRA-driven if adverse)
public sealed record ApplicationDecision
{
    public required ApplicationDecisionKind Kind { get; init; }
    public required IReadOnlyList<TaxonomyClassification> DeclineReasons { get; init; }   // Sunfish.LeasingPipeline.DeclineReasons@1.0.0 nodes (future starter taxonomy)
    public required string Notes { get; init; }                            // operator-facing only; not in adverse-action letter
    public required IdentityRef DecidedBy { get; init; }
    public required DateTimeOffset DecidedAt { get; init; }
    public bool ConsumerReportBased { get; init; }                         // if true, FCRA adverse-action letter REQUIRED
}

public enum ApplicationDecisionKind { Approved, Declined, Withdrawn }

// Showing — schedule + iCal
public sealed record Showing
{
    public required ShowingId Id { get; init; }
    public required InquiryId? InquiryRef { get; init; }                  // null if walk-in showing
    public required PropertyId Property { get; init; }
    public required IReadOnlyList<DateTimeOffset> ProposedSlots { get; init; }
    public required DateTimeOffset? ConfirmedSlot { get; init; }
    public required ShowingStatus Status { get; init; }
    public required ContactPoint ProspectContact { get; init; }
    public bool RequiresRightOfEntryNotice { get; init; }                  // if property is occupied
    public RightOfEntryNoticeId? RightOfEntryNoticeRef { get; init; }      // forward-compat per Right-of-Entry ADR (queued)
}

public readonly record struct ShowingId(Guid Value);

public enum ShowingStatus
{
    Proposed,
    Confirmed,
    Completed,
    NoShow,
    Rescheduled,
    Cancelled
}

// Adverse action letter (FCRA-required when ConsumerReportBased decline)
public sealed record AdverseActionLetter
{
    public required AdverseActionLetterId Id { get; init; }
    public required ApplicationId ApplicationRef { get; init; }
    public required ContentHash LetterContentHash { get; init; }            // immutable; FCRA-defense
    public required IReadOnlyList<AdverseActionReason> Reasons { get; init; }
    public required string ConsumerReportProvider { get; init; }           // e.g., "Equifax / Experian / TransUnion / TenantData"
    public required ContactPoint ConsumerReportProviderContact { get; init; } // per FCRA: applicant must be told who to dispute with
    public required DateTimeOffset GeneratedAt { get; init; }
    public required DateTimeOffset DeliveredAt { get; init; }              // via messaging substrate (ADR 0052)
    public required MessageId DeliveryMessageRef { get; init; }            // ADR 0052
}

public readonly record struct AdverseActionLetterId(Guid Value);

// Public Bridge inquiry-form surface — anonymous capability
public interface IPublicInquiryService
{
    Task<InquirySubmissionResult> SubmitInquiryAsync(
        PublicInquiryRequest request,
        AnonymousCapability capability,
        CancellationToken ct);
}

public sealed record PublicInquiryRequest
{
    public required PropertyId? PropertyOfInterest { get; init; }
    public required ContactPoint ContactInfo { get; init; }
    public PersonName? ProspectName { get; init; }
    public required string Message { get; init; }
    public required CaptchaToken Captcha { get; init; }
    public required ClientFingerprint ClientFingerprint { get; init; }    // for rate-limiting
}

public enum InquirySubmissionResult
{
    Accepted,
    RateLimited,
    CaptchaFailed,
    AbuseDetected
}
```

(Schema sketch only; XML doc + nullability + `required` enforced at Stage 06.)

### Substrate / layering notes

```
blocks-property-leasing-pipeline   (this ADR)
        ↓ depends on
    ADR 0054 SignatureEvent (CriteriaAcknowledgement)
    ADR 0055 Dynamic Forms (Application form definition + JSONB storage)
    ADR 0056 Foundation.Taxonomy (Jurisdiction policy + Decline reasons)
    ADR 0052 Messaging Substrate (inquiry / criteria delivery / adverse-action delivery)
    ADR 0049 Audit (12 lifecycle audit record types)
    ADR 0028 CRDT (Inquiry/Showing AP; Application/CriteriaAcknowledgement/AdverseActionLetter CP)
    blocks-properties (PropertyId references)
    blocks-leases (LeaseDocument link post-Approval; eventual consumer)
```

### Public-input boundary + capability promotion (ADR 0043 addendum)

This ADR introduces three capability tiers for the leasing pipeline:

| Capability | Identity proof | Allowed actions | ADR 0043 zone |
|---|---|---|---|
| **Anonymous** | None (rate-limit + CAPTCHA + abuse-fingerprint only) | Submit inquiry; view public listings | Public ingress (new boundary) |
| **Prospect** | ContactPoint verified (email-confirm-link or SMS-OTP) + criteria-acknowledgment signature | Receive criteria; acknowledge; schedule showings | Trusted-input boundary |
| **Applicant** | Prospect + completed application + ID verification + FCRA consent | Submit application; partial-save; receive adverse-action notice if declined | Trusted-data-supplier boundary |

ADR 0043 addendum required: §"Public ingress trust posture" + capability-promotion flow + abuse-mitigation requirements (rate-limit, CAPTCHA, abuse-fingerprint, IP-reputation list, application-fee-friction). Addendum authoring is a **prerequisite to Stage 06** but ships as a separate ADR amendment.

### Fair Housing structural defense

The substrate enforces FHA compliance via four mechanisms:

1. **Versioned criteria documents.** Every criteria sent to a prospect references a specific `CriteriaDocumentVersionId`. Once acknowledged, the prospect's CriteriaAcknowledgement permanently binds to that version.

2. **Uniform criteria evaluation.** The `IApplicationReviewService.ScreenAsync` API takes the `Application` and the `CriteriaDocumentVersionId` and applies the criteria as documented. **There is no mechanism to apply different criteria per-applicant.** Operators cannot bypass.

3. **Audit-record per evaluation.** Every screening produces a `ScreeningEvaluation` audit record (per ADR 0049) that records: applicant ID, criteria-document version, each criterion's pass/fail, decision, decision-time, decided-by. Forensic reconstruction is deterministic.

4. **Adverse-action letter content-binding.** Decline letters reference the `CriteriaDocumentVersionId` + the failed criteria + the consumer-report data (if applicable per FCRA). Letter content is content-hash-bound (immutable post-generation).

These four mechanisms make non-uniform-application **structurally impossible**, not policy-prohibited. An operator cannot "screen out a prospect because of [protected class]" through the substrate — the only path to decline is criteria evaluation against the bound criteria document version.

### FCRA workflow

When a decline references consumer-report data (credit + background + tenant-data screening):

1. `ApplicationDecision.ConsumerReportBased = true`
2. `AdverseActionLetter` is **automatically generated** by the substrate
3. Letter content composes from a Foundation-shipped FCRA-compliant template + `ApplicationDecision.DeclineReasons` + consumer-report-provider info
4. Letter content-hash-bound + immutable
5. Delivery via messaging substrate (ADR 0052) to applicant's verified ContactPoint
6. Delivery confirmation logged in audit substrate
7. SLA timer (60-day FCRA window) tracked; substrate emits `AdverseActionLetterOverdue` audit event if undelivered within window

Substrate **prevents shipping a `Declined` decision with `ConsumerReportBased = true` without a corresponding `AdverseActionLetter`** via analyzer SUNFISH_FCRA_001.

### Jurisdiction policy (Foundation.Taxonomy starter)

Sunfish ships `Sunfish.Jurisdiction.US-States.Leasing@1.0.0` as a starter taxonomy (queued; charter authoring in follow-up to this ADR). Per-state nodes carry policy attributes:

- Maximum application fee (Money cap)
- Required disclosures (`JurisdictionDisclosure[]`)
- Tenant-screening law specifics (e.g., NY rent-stabilized; CA AB 1482; WA RCW 59.18)
- Adverse-action letter requirements beyond FCRA baseline
- Permissible criteria attributes (some states limit credit-score floors; some prohibit eviction-history beyond N years)

Resolver: `IJurisdictionPolicyResolver.Resolve(jurisdictionTaxonomyClassification)` returns a `JurisdictionPolicy` value object that the criteria-document generator + application-screener + adverse-action-letter generator consult.

Phase 2.1d ships **Utah + Colorado** policies (BDFL's-property-states); Phase 2.3 broadens to top-20 US states; broader coverage in v1.x.

### Audit-substrate integration (ADR 0049)

12 typed audit record types added:

| Audit record type | Emitted on |
|---|---|
| `InquiryReceived` | `SubmitInquiryAsync` accepts |
| `CriteriaSent` | criteria document delivered to prospect's contact |
| `CriteriaAcknowledged` | CriteriaAcknowledgement signature event captured |
| `ApplicationStarted` | application creation (partial-save initiated) |
| `ApplicationSubmitted` | application completed + fee paid |
| `ApplicationReviewStarted` | operator opens for review |
| `ApplicationApproved` | decision = Approved |
| `ApplicationDeclined` | decision = Declined |
| `AdverseActionGenerated` | FCRA letter generated |
| `AdverseActionDelivered` | letter delivered via messaging |
| `AdverseActionLetterOverdue` | 60-day FCRA window approaching without delivery |
| `ScreeningEvaluation` | per-criterion pass/fail decision (forensic reconstruction) |

These join the existing audit vocabulary (signature lifecycle per ADR 0054, work-order lifecycle per ADR 0053, etc.).

### Per-record-class CP/AP positioning (paper §13)

| Entity class | Mode | Rationale |
|---|---|---|
| `Inquiry` | AP | Multi-leg public ingress; offline iPad showing-coordination → online sync; eventual consistency OK |
| `CriteriaDocument` | CP | Single source of truth; versions are immutable; consensus-required for publication |
| `CriteriaAcknowledgement` | CP | FHA-defense audit artifact; cannot have divergent versions |
| `Application` | CP | FCRA + FHA require single canonical version; no concurrent-edit semantics |
| `Showing` | AP | Schedule coordination; multi-leg confirmation OK; eventual consistency |
| `AdverseActionLetter` | CP | FCRA letter is immutable; single canonical version; consensus-required |

CP records use ADR 0028 single-coordinator model; AP records use ADR 0028 CRDT G-Set/G-Counter as appropriate.

### Showing scheduling

The owner publishes 3 candidate slots; the prospect picks one; the owner confirms. iCal export. If property is currently occupied (rare; usually only between leases), the showing requires a right-of-entry notice — references the queued Right-of-Entry Compliance ADR.

For Phase 2.1d, right-of-entry coordination is captured as a **placeholder field** (`RequiresRightOfEntryNotice: bool` + `RightOfEntryNoticeRef: RightOfEntryNoticeId?`); typed ref resolves once Right-of-Entry ADR Accepts.

### What this ADR does NOT do

- **Does not define public listing surface** — handled by Public Listings ADR (cluster INDEX-queued)
- **Does not define lease document storage / execution** — handled by Leases EXTEND (workstream #27)
- **Does not define move-in checklist execution** — handled by Inspections EXTEND (workstream #25)
- **Does not define credit/background-check provider integrations** — `providers-screening-*` adapter family (separate ADR per provider)
- **Does not define property-management portal replacement** — Rentler-portal replacement is Phase 3 explicit
- **Does not define dispute-resolution workflows** — applicant disputing decline is Phase 2.3+
- **Does not define multi-tenant federation** — cross-tenant inquiry routing (e.g., applicant submits to one LLC; LLC refers to another) is post-MVP
- **Does not define notarized criteria documents** — notarization is ADR 0054 Phase 4+
- **Does not define jurisdiction-arbitrage detection** — operator forum-shopping per-jurisdiction rules is out-of-scope

---

## Consequences

### Positive

- **FHA structural defense.** Substrate cannot produce non-uniformly-applied criteria; uniformity is enforced at the API contract level
- **FCRA workflow automation.** Adverse-action letters generated automatically from decision; delivery + SLA tracking built in
- **Jurisdiction policy via taxonomy.** Per-state compliance is pluggable; new states ship as taxonomy version-bumps, not code changes
- **Public-input boundary modeled.** Anonymous → prospect → applicant capability promotion is explicit + audit-trailed
- **Composes 6 existing ADRs cleanly.** No new kernel-tier substrate; no new foundation-tier substrate
- **Property-management business unblocks.** BDFL's leasing workflow ships in Phase 2.1d (Utah + Colorado)

### Negative

- **Block-tier weight.** ~12 entity types + 12 audit subtypes + state machine + jurisdiction resolver + public Bridge surface = substantive package
- **ADR 0043 addendum prerequisite.** Stage 06 build cannot start until ADR 0043 addendum (public-input boundary + capability promotion) Accepted; sequencing constraint
- **Jurisdiction-policy scope.** Phase 2.1d ships 2 states (Utah + Colorado); broader US coverage requires per-state policy authoring (legal expertise + budget)
- **Adverse-action template.** FCRA-compliant template requires legal review for v1.0 ship; Foundation ships baseline + tenants override per-jurisdiction

### Trust impact / Security & privacy

- **PII in inquiry.** Prospect contact info + name + message are PII; tenant-key-encrypted at rest; never in audit projections
- **PII in application.** Full applicant data (income, employment, references, prior addresses, SSN-fragment for FCRA, etc.) is highly sensitive PII; tenant-key-encrypted; access-controlled per macaroon scope
- **Consumer-report data is FCRA-regulated PII.** Stored only as long as adverse-action SLA requires (60 days post-decision); auto-purged thereafter per FCRA + state law
- **Public-form abuse surface.** Rate-limit + CAPTCHA + abuse-fingerprint + IP-reputation list mitigate; ADR 0043 addendum specifies thresholds
- **Adverse-action letter delivery.** Messaging substrate (ADR 0052) delivery; same encryption + audit posture as other tenant communications
- **Jurisdiction policy provenance.** Sunfish-shipped policies pin to legal-counsel review; tenant overrides require explicit acknowledgment of legal-responsibility shift

---

## Compatibility plan

### Existing callers / consumers

No production code references the leasing pipeline today. ADR 0054 (Signatures) reserves a `criteria-acknowledgment` SignatureScope node in Sunfish.Signature.Scopes@1.0.0 (per starter taxonomy charter). ADR 0053 (Work Orders) does not reference leasing.

### Affected packages

| Package | Change |
|---|---|
| `packages/blocks-property-leasing-pipeline` (new) | **Created** — primary deliverable |
| `packages/kernel-audit` | **Modified** — adds 12 typed audit record subtypes |
| `packages/foundation-taxonomy` (planned per ADR 0056) | **Eventual consumer** — `Sunfish.Jurisdiction.US-States.Leasing@1.0.0` starter taxonomy charter authored as follow-up |
| `packages/blocks-leases` | **Eventual consumer** — Approval transitions to LeaseDrafted state which creates a Lease |
| `accelerators/bridge` | **Modified** — public inquiry-form Bridge surface (anonymous capability) |
| `accelerators/anchor` | **Modified** — operator-facing leasing-pipeline UI (Inquiry list, Application review, Decision capture) |

### ADR amendments triggered

1. **ADR 0043 addendum (Major).** Public-input boundary + capability promotion (Anonymous → Prospect → Applicant) + abuse posture. **Authored separately as ADR-0043-A1.** Stage 06 build is gated on this addendum's Acceptance.
2. **ADR 0049 confirmation.** 12 new audit record subtypes; no structural change.
3. **ADR 0054 confirmation.** No edits; SignatureScope `criteria-acknowledgment` node is already in starter taxonomy charter (PR #242).
4. **ADR 0055 consumer.** Application form definition uses ADR 0055 dynamic-forms substrate; no edits to ADR 0055.
5. **ADR 0056 consumer.** Jurisdiction policy uses ADR 0056 Foundation.Taxonomy substrate; new starter taxonomy charter `Sunfish.Jurisdiction.US-States.Leasing@1.0.0` authored as follow-up to this ADR.

---

## Implementation checklist

- [ ] `packages/blocks-property-leasing-pipeline/` scaffolded; references blocks-properties + foundation-taxonomy + foundation-recovery + kernel-audit + kernel-signatures
- [ ] Entity types defined: `Inquiry`, `CriteriaDocument`, `CriteriaDocumentBody`, `CriteriaAcknowledgement`, `Application`, `ApplicationDecision`, `Showing`, `AdverseActionLetter` + IDs + enums; full XML doc; nullability + `required` enforced
- [ ] State machine: `LeasingPipelineState` + transitions + state-machine validator (no invalid transitions)
- [ ] `IPublicInquiryService` + `IApplicationReviewService` + `IJurisdictionPolicyResolver` + `IAdverseActionLetterService` interfaces
- [ ] In-memory reference implementations for each service
- [ ] 12 audit record types added to `Sunfish.Kernel.Audit` per ADR 0049
- [ ] **ADR-0043-A1 (public-input boundary + capability promotion) Accepted** — gating Stage 06 build
- [ ] **`Sunfish.Jurisdiction.US-States.Leasing@1.0.0` starter taxonomy charter authored** (follow-up intake)
- [ ] **`Sunfish.LeasingPipeline.DeclineReasons@1.0.0` starter taxonomy charter authored** (follow-up intake)
- [ ] Phase 2.1d Utah + Colorado jurisdiction policies authored
- [ ] FCRA-compliant adverse-action letter template authored (legal review before v1.0)
- [ ] Analyzer SUNFISH_FCRA_001 — `Declined + ConsumerReportBased without AdverseActionLetter` is compile-time error
- [ ] Analyzer SUNFISH_FHA_001 — `IApplicationReviewService.ScreenAsync` cannot bypass criteria-document-version binding
- [ ] CP/AP positioning: Inquiry/Showing = AP CRDT G-Set; Application/CriteriaAcknowledgement/AdverseActionLetter = CP single-coordinator
- [ ] Bridge public inquiry-form surface (anonymous capability + CAPTCHA + rate-limit + abuse-fingerprint)
- [ ] Anchor operator UI (Inquiry list + Criteria send + Application review + Decision capture + Showing scheduler with iCal export)
- [ ] kitchen-sink demo: full Inquiry → Approved or Declined-with-Adverse-Action flow
- [ ] FHA-defense fixture test: attempted non-uniform-criteria-application produces compile-time error (analyzer test)
- [ ] FCRA-defense fixture test: Declined + ConsumerReportBased automatically produces AdverseActionLetter; SLA-overdue produces audit event
- [ ] apps/docs entry covering pipeline state machine + FHA posture + FCRA workflow + jurisdiction-policy resolution + public-input trust boundary

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-L1 | Application fee payment — does this ADR specify capture mechanism, or defer to ADR 0051 (Payments)? | Stage 02 — recommend defer to ADR 0051. Application fee is a Payment with `PaymentReason.LeasingApplicationFee`. Substrate references `PaymentId`. |
| OQ-L2 | Income verification — bank-link (Plaid) vs paystub upload vs employer-letter? | Stage 02 — recommend dynamic-form-overlay per jurisdiction; substrate doesn't pin. Provider integration (Plaid + others) is `providers-financial-*` adapter family. |
| OQ-L3 | ID verification (e.g., driver's license capture + verification) — substrate-managed or out-of-band? | Stage 02 — recommend out-of-band for v1; capture as document-attachment on Application; verification is operator-judgment. KYC-class verification is Phase 2.3+. |
| OQ-L4 | Multi-applicant (co-tenant) applications — single Application with multiple applicants or N Applications linked? | Stage 02 — recommend single Application with `IReadOnlyList<ApplicantInfo>` for primary + co-applicants; FCRA evaluation per-applicant; FHA criteria applied to household composition. |
| OQ-L5 | Application withdrawal vs decline — can applicants withdraw post-submit but pre-decision? | Recommend yes; `ApplicationStatus.Withdrawn` is a terminal non-decline state; no adverse-action letter required. |
| OQ-L6 | Stalled applications — auto-archive after N days or operator-action-required? | Recommend 14-day operator-no-action triggers `ApplicationStatus.Stalled` flag (audit event); 30-day auto-archives to `Withdrawn` state with operator-notify. |
| OQ-L7 | Showing no-shows — operator-marks vs auto-detect via iCal-not-confirmed? | Recommend operator-marks; iCal can't reliably detect attendance. |
| OQ-L8 | Public listing → inquiry attribution — how does Bridge attribute an inquiry to a specific listing surface? | Recommend `InquirySource.PublicListingPage` + `PropertyId` reference; listing-specific tracking (UTM-style) is post-MVP analytics. |
| OQ-L9 | Operator-rejection-reason taxonomy — separate `Sunfish.LeasingPipeline.DeclineReasons@1.0.0`, or shared with future taxonomy? | Recommend dedicated starter taxonomy. Sample nodes: insufficient-income / failed-credit-check / failed-background-check / failed-eviction-policy / failed-rental-history / non-FCRA-policy-failure (e.g., smoking, occupancy). |
| OQ-L10 | Right-of-entry coordination for showings during occupancy — substrate-handled or refer-to-Right-of-Entry-ADR? | Refer; placeholder fields in `Showing`; typed-ref resolves post-Right-of-Entry-ADR Acceptance. |
| OQ-L11 | Anti-discrimination training / certification for operators — does substrate enforce? | Out of scope; operational concern; document in apps/docs as recommended practice. |

---

## Revisit triggers

This ADR should be re-evaluated when any of the following fire:

- **FHA enforcement action** — first complaint or DOJ inquiry alleging non-uniform application of criteria; review substrate's structural defense + audit-trail completeness
- **FCRA enforcement action** — first complaint or class-action alleging adverse-action notice failure; review SLA tracking + delivery confirmation
- **State-law change** — any jurisdiction adds tenant-screening or fair-housing law that current substrate cannot accommodate via taxonomy version-bump alone
- **Public-input abuse** — first DDoS / spam wave on public inquiry form exceeding rate-limit + CAPTCHA defenses; review ADR-0043-A1 thresholds
- **Multi-tenant federation request** — first request to route inquiries across LLC tenants in same operator group
- **Application-fee change** — Phase 2.3+ adopts payment-capture per ADR 0051; review composition with ADR 0051 PaymentReason
- **Forensic FHA dispute** — substrate cannot reproduce screening evaluation deterministically from audit records
- **Civilian / vertical extension** — non-property-management consumer wants leasing-pipeline-shaped capability promotion (e.g., government-services-application flow) — promote substrate to foundation-tier
- **Rentler-replacement Phase 3** — full portal-class workflow lands; review whether substrate accommodates or needs Phase 3 amendment

---

## References

### Predecessor + sister ADRs

- [ADR 0028](./0028-crdt-engine-selection.md) — CRDT substrate; Inquiry/Showing AP positioning
- [ADR 0043](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md) — Threat model; addendum (ADR-0043-A1) drives Stage 06 gate
- [ADR 0049](./0049-audit-trail-substrate.md) — Audit substrate; 12 new event types
- [ADR 0052](./0052-bidirectional-messaging-substrate.md) — Messaging; criteria-document delivery + adverse-action letter delivery
- [ADR 0053](./0053-work-order-domain-model.md) — Sibling cluster ADR
- [ADR 0054](./0054-electronic-signature-capture-and-document-binding.md) — Signatures; CriteriaAcknowledgement uses SignatureEvent
- [ADR 0055](./0055-dynamic-forms-substrate.md) — Dynamic forms; Application uses dynamic-form-shaped definition
- [ADR 0056](./0056-foundation-taxonomy-substrate.md) — Foundation.Taxonomy; jurisdiction policy + decline reasons
- [ADR 0046-A1](./0046-a1-historical-keys-projection.md) — Historical-keys projection (signature survival under operator-key rotation)
- [ADR 0051](./0051-foundation-integrations-payments.md) — Application fee capture (OQ-L1)

### Roadmap and specifications

- [Property-ops cluster INDEX](../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — pins ADR drafting order; leasing-pipeline is #4
- [Leasing-pipeline cluster intake](../../icm/00_intake/output/property-leasing-pipeline-intake-2026-04-28.md) — Stage 00 spec source (160 lines; 14 in-scope items)
- [Phase 2 commercial intake](../../icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md) — pre-screening criteria + leasing pipeline mentioned
- [Starter taxonomy charters](../../icm/00_intake/output/starter-taxonomies-v1-charters-2026-04-29.md) — Sunfish.Signature.Scopes referenced

### Existing code / substrates

- `packages/blocks-properties/` — PropertyId references
- `packages/blocks-leases/` — eventual consumer (Approval → LeaseDrafted)
- `packages/kernel-audit/` — audit substrate consumer
- `packages/kernel-signatures/` (planned per ADR 0054) — CriteriaAcknowledgement consumer

### External

- Fair Housing Act (42 U.S.C. § 3601 et seq.) — federal anti-discrimination baseline
- California Fair Employment and Housing Act (FEHA) — state-protected-class additions
- Fair Credit Reporting Act (15 U.S.C. § 1681 et seq.) — adverse-action requirements
- HUD-issued guidance on tenant screening — non-binding but persuasive; criminal-history screening guidance 2016
- iCalendar (RFC 5545) — showing schedule export format

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options considered: block-tier (A), kernel-tier (B), foundation-tier (C). Option A chosen with explicit rejection rationale per option (kernel mismatched layer; foundation premature abstraction).
- [x] **FAILED conditions / kill triggers.** 9 revisit triggers tied to externally-observable signals (FHA enforcement, FCRA enforcement, state-law change, public-input abuse, multi-tenant federation, etc.).
- [x] **Rollback strategy.** No production code consumes leasing-pipeline today. Rollback = revert this ADR + revert blocks-property-leasing-pipeline package + revert 12 audit subtype additions.
- [x] **Confidence level.** **HIGH for substrate shape; MEDIUM for jurisdiction-policy depth.** Substrate composes 6 well-understood ADRs cleanly. Jurisdiction policy depth is bounded by Phase 2.1d (Utah + Colorado) — broader coverage gates on legal expertise + budget.
- [x] **Anti-pattern scan.** None of AP-1 (unvalidated assumptions — flow specified), AP-3 (vague success — 12 audit types + analyzers), AP-9 (skipping Stage 0 — 3 options sparred), AP-21 (assumed facts — FHA + FCRA + iCal cited; HUD guidance cited).
- [x] **Revisit triggers.** 9 conditions with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 18 specific tasks. Fresh contributor reading this ADR + cluster intake + ADR 0049/0052/0054/0055/0056/0028 should be able to scaffold blocks-property-leasing-pipeline without ambiguity.
- [x] **Sources cited.** 9 ADRs referenced. FHA + FCRA + HUD guidance + iCalendar (RFC 5545) cited. Cluster intake + INDEX + Phase 2 commercial intake referenced.

---

## Amendments (post-acceptance)

### A1 (2026-04-30) — `DemographicProfile` structural encryption (post-W#32 substrate)

**Driver:** W#22 Phase 9 (PR #390) replaced `DemographicProfile`'s per-field plaintext storage (`string?`, `int?`) with `Sunfish.Foundation.Recovery.EncryptedField?` per the W#32 substrate (ADR 0046-A2/A3/A4/A5). The 11 protected-class + protected-adjacent fields (Race / Color / NationalOrigin / Religion / Sex / FamilialStatus / Disability / Age / MaritalStatus / SourceOfIncome / VeteranStatus) now require an `IFieldDecryptor` capability to access plaintext.

This amendment **ratifies the structural-enforcement claim** that the original ADR's §"FHA-defense layout" made: protected-class fields are now structurally inaccessible to decisioning, not merely test-enforced.

#### A1.1 — What changed (consumer side)

- `DemographicProfileSubmission` plaintext record introduced at the `SubmitApplicationRequest` boundary; `LeasingPipelineService.SubmitApplicationAsync` encrypts at the service boundary; plaintext never persists.
- `DemographicProfile.Race` (and 10 sibling fields) typed as `EncryptedField?`; reading the `Value` requires `IFieldDecryptor.DecryptAsync(field, capability, tenant, ct)` which is itself audit-emitting (`FieldDecrypted` per W#32 audit pattern).
- Decisioning code paths (`DecisioningFactsBuilder`, future `BackgroundCheckOrchestrator`, future `AdverseActionNoticeGenerator`) do NOT receive `DemographicProfile` references and do NOT hold an `IFieldDecryptor` capability; the type system enforces this.

#### A1.2 — Permitted decryption sites

Only two consumer classes are permitted to decrypt demographic fields:

1. **Compliance reporting (HUD aggregated statistics, FHA filings, etc.)** — holds a `ComplianceReportingDecryptCapability` scoped to compliance-reporting work. Per-field decrypts emit `FieldDecrypted` audit per W#32; aggregate statistics emit no per-row identification.
2. **Subject Access Request (SAR) handler** (per FCRA §609 + GDPR Article 15 + CCPA §1798.110) — holds a `SubjectAccessRequestDecryptCapability` scoped to a specific Prospect's own data; per-field decrypts emit `FieldDecrypted` audit identifying the Prospect-as-requestor.

No other readers. Decisioning, AdverseActionNotice generation, BackgroundCheck orchestration, application-status views, etc. all consume only the non-protected `DecisioningFacts` per the ADR's original §"FHA-defense layout."

#### A1.3 — Audit-payload field-name absence test (downgraded role)

The original Phase 6 audit-emission-invariant test in `LeasingPipelineServiceTests` used reflection to assert that no `AuditPayload.Body` contains a key matching a demographic field name (`race`, `color`, etc.). This was the **primary structural defense** in the pre-A1 layout.

Post-A1, the structural defense is the type system (decisioning code can't access demographic fields without a capability they don't hold). The audit-payload test is **downgraded to "belt-and-braces tripwire"** — useful as a regression detector if future code accidentally introduces decisioning-side demographic-field reflection. Severity drops from "primary structural enforcement" to "useful sanity check."

#### A1.4 — Cited-symbol verification (Decision Discipline Rule 6)

All cited symbols verified existing on `origin/main` post-PR #390 merge:

- `Sunfish.Foundation.Recovery.EncryptedField` — verified existing per W#32 PR #370
- `Sunfish.Foundation.Recovery.IFieldDecryptor` — verified existing per W#32 PRs #371 + #372
- `Sunfish.Kernel.Audit.AuditEventType.FieldDecrypted` — verified existing per W#32 audit emission shipping
- `Sunfish.Blocks.PropertyLeasingPipeline.Models.DemographicProfile` — verified existing post-#390 in revised `EncryptedField`-bearing shape
- `Sunfish.Blocks.PropertyLeasingPipeline.Models.DemographicProfileSubmission` — verified existing post-#390 (introduced as plaintext-boundary type)

`ComplianceReportingDecryptCapability` + `SubjectAccessRequestDecryptCapability` are not yet implemented (Phase 9 ships the consumer-side surface; the actual capability-issuing tools are out of W#22 scope and TBD per Phase 2.2+ compliance-tooling work).

#### A1.5 — Migration semantics

- Production callers: zero (W#22 v1.0 just shipped 2026-04-30). No data migration needed.
- In-flight test data: existing tests updated to use `DemographicProfileSubmission` at the input boundary; no persistent-store schema migration (in-memory provider).
- Future production callers (Phase 2.2+ when LLC ships): flow through `EncryptedField`-bearing shape; no backfill since v1.0 ships post-encryption-design.

#### A1.6 — Compliance posture

This amendment **strengthens** the original ADR's FHA-defense claim from "test-enforced" to "structurally enforced":

> **Original ADR §"FHA-defense layout":** "DemographicProfile is structurally inaccessible to decisioning."
> **Pre-A1 reality:** test-enforced via reflection-based audit-payload absence check.
> **Post-A1 reality:** type-system-enforced via `EncryptedField` + capability gate; reflection check retained as belt-and-braces.

HUD/FHA/FCRA compliance posture is unchanged (the data still exists, with stricter access controls). SAR/GDPR access rights are unchanged (Prospects can still read their own demographics via the SAR handler).

#### A1.7 — Decision-class

Session-class per `feedback_decision_discipline` Rule 1. Compliance hardening (HUD/FHA/FCRA-aligned good practice); zero production callers; closes the W#32 substrate→W#22 consumer chain. Authority: XO; ratifies the structural claim that W#22 Phase 9 (PR #390) implemented.

#### A1.8 — References

- W#22 Phase 9 build PR: #390
- W#22 Phase 9 hand-off addendum: `icm/_state/handoffs/property-leasing-pipeline-stage06-demographic-encryption-addendum.md` (PR #389)
- W#32 substrate: ADR 0046-A2/A3/A4/A5 + `EncryptedField` + `IFieldDecryptor`
- ADR 0049 (Audit Trail Substrate) — `FieldDecrypted` AuditEventType consumer
- HUD §100.20-100.24 (FHA protected classes); FCRA §609 (Subject Access Right); GDPR Article 15; CCPA §1798.110
