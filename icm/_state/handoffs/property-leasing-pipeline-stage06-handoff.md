# Workstream #22 — Leasing Pipeline + Fair Housing — Stage 06 hand-off

**Workstream:** #22 (Leasing Pipeline cross-cutting; Inquiry → Application → BG-check → Lease)
**Spec:** [ADR 0057](../../docs/adrs/0057-leasing-pipeline-fair-housing.md) (Accepted 2026-04-29 — see PR #247)
**Pipeline variant:** `sunfish-feature-change` (new block `blocks-property-leasing-pipeline` + ADR 0043 addendum)
**Estimated effort:** 16–22 hours focused sunfish-PM time
**Decomposition:** 8 phases shipping as ~6 PRs
**Prerequisites:** W#31 Foundation.Taxonomy ✓ (PR #263); W#21 Signatures (in flight; needed for application+lease signatures); ADR 0059 Public Listing (Proposed; provides `Inquiry` boundary contract); ADR 0060 Right-of-Entry (Proposed; provides showing-notice compliance check)

---

## Scope summary

Build the leasing pipeline end-to-end:

1. **`blocks-property-leasing-pipeline` package** — `Inquiry`, `Prospect`, `Application`, `BackgroundCheckResult`, `AdverseActionNotice` entities + state machine + FHA-defense audit emission
2. **Public-input boundary** consuming ADR 0059 inquiry surface — receives `InquirySubmission` from Bridge listing form
3. **Capability promotion** Anonymous → Prospect → Applicant per ADR 0043 addendum (macaroons via ADR 0032)
4. **FHA structural defense** — protected-class fields stored separately from decisioning fields; structural impossibility of consulting protected-class data during Accept/Decline
5. **FCRA workflow** — application fee collection (ADR 0051), background check kick-off, adverse-action notice with FCRA-mandated language + dispute window
6. **Jurisdiction policy taxonomy** — `Sunfish.Leasing.JurisdictionRules@1.0.0` charter consuming W#31 + ADR 0060 patterns
7. **Showing-notice compliance** — leasing showings call `IEntryComplianceChecker` from ADR 0060
8. **Audit emission** — 12 new `AuditEventType` constants per ADR 0049

**NOT in scope:** payment-gateway implementation (consumes ADR 0051's `IPaymentGateway` stub from W#19 Phase 0); background-check provider adapter (consumed via `IBackgroundCheckProvider` interface; first concrete adapter is a follow-up); iOS application UX (deferred to W#23).

---

## Phases

### Phase 1 — Package scaffold + entity types (~3–4h)

Audit-first: confirm `packages/blocks-property-leasing-pipeline/` doesn't already exist (per `feedback_audit_existing_blocks_before_handoff`).

New package; entity types per ADR 0057's contract surface:

- `Inquiry` (consumed from ADR 0059's `InquirySubmission` via boundary)
- `Prospect` (capability-tier-1 promotion)
- `Application` (capability-tier-2 promotion; FHA-defense layout)
- `BackgroundCheckResult` (FCRA-compliant)
- `AdverseActionNotice` (FCRA-mandated content)
- `LeaseOffer` (precursor to ADR 0028 Lease)

Key shape (ADR 0057 §"Initial contract surface"):

```csharp
// FHA-defense layout: protected-class fields ONLY in DemographicProfile;
// DecisioningFacts contains the only fields visible to Accept/Decline logic
public sealed record Application
{
    public required ApplicationId Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required ProspectId Prospect { get; init; }
    public required PublicListingId Listing { get; init; }
    public required DecisioningFacts Facts { get; init; }            // income, credit, eviction history, references
    public required DemographicProfile Demographics { get; init; }    // protected-class data; encrypted; NEVER read by decisioning
    public required ApplicationStatus Status { get; init; }
    public required SignatureEventId ApplicationSignature { get; init; } // ADR 0054
    public required Money ApplicationFee { get; init; }                // ADR 0051; collected via IPaymentGateway
    public required DateTimeOffset SubmittedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
    public ActorId? DecidedBy { get; init; }
}

public enum ApplicationStatus
{
    Submitted, AwaitingBackgroundCheck, AwaitingDecision, Accepted, Declined, Withdrawn
}
```

`DemographicProfile` is per-tenant-key encrypted (ADR 0046 `EncryptedField`); `IFieldDecryptor` access requires a separate capability (e.g., for HUD reporting only) and emits an audit record per read.

Decisioning interfaces (`IApplicationDecisioner`) accept ONLY `DecisioningFacts` — `DemographicProfile` is structurally inaccessible.

**Gate:** package builds; entity types ship with full XML doc + nullability + `required`; `dotnet build` clean.

**PR title:** `feat(blocks-property-leasing-pipeline): Phase 1 substrate scaffold + FHA-defense layout (ADR 0057)`

### Phase 2 — State machine + capability promotion (~3–4h)

`ILeasingPipelineService` orchestrates state transitions:

```text
Inquiry → Prospect (on email-verification + listing-criteria-acknowledgment)
Prospect → Applicant (on application-fee paid + application-signature captured)
Application → AwaitingBackgroundCheck (on fee + sig confirmation)
AwaitingBackgroundCheck → AwaitingDecision (on bg-check provider response)
AwaitingDecision → Accepted | Declined (on operator decision)
Accepted → LeaseOffer (kick-off ADR 0028 lease creation; out of scope this hand-off but interface boundary defined)
Declined → AdverseActionNotice issuance + 60-day dispute window
```

Each transition gates on per-state capability check (ADR 0032 macaroon).

Capability promotion (ADR 0043 addendum): Anonymous (browse listings via ADR 0059) → Prospect (inquiry submitted, email verified, criteria acknowledged) → Applicant (fee paid, application signed).

**Gate:** state machine refuses invalid transitions; capability checks gate every transition; tests cover happy path + each invalid-transition scenario.

**PR title:** `feat(blocks-property-leasing-pipeline): state machine + capability promotion (ADR 0057 + 0043)`

### Phase 3 — FCRA workflow + AdverseActionNotice (~3–4h)

Background check kick-off via `IBackgroundCheckProvider` interface (Phase 1 InMemory provider; real provider adapter deferred to follow-up):

```csharp
public interface IBackgroundCheckProvider
{
    Task<BackgroundCheckResult> KickOffAsync(BackgroundCheckRequest request, CancellationToken ct);
    Task<BackgroundCheckResult> GetStatusAsync(string vendorRef, CancellationToken ct);
}

public sealed record BackgroundCheckResult
{
    public required string VendorRef { get; init; }                  // opaque token from BG-check provider
    public required BackgroundCheckOutcome Outcome { get; init; }
    public required IReadOnlyList<AdverseFinding> Findings { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
}

public sealed record AdverseActionNotice
{
    public required AdverseActionNoticeId Id { get; init; }
    public required ApplicationId Application { get; init; }
    public required IReadOnlyList<AdverseFinding> CitedFindings { get; init; }
    public required string FcraStatement { get; init; }              // FCRA §615 mandatory language
    public required DateTimeOffset DisputeWindowExpiresAt { get; init; } // 60 days per FCRA
    public required string ConsumerReportingAgency { get; init; }    // who to dispute with
    public required string Address { get; init; }                    // CRA's address per FCRA
    public required SignatureEventId NoticeIssuanceSignature { get; init; } // operator-signed per ADR 0054
    public required DateTimeOffset IssuedAt { get; init; }
}
```

**Gate:** AdverseActionNotice generation includes FCRA mandatory language; 60-day dispute window enforced; tests cover decline-with-bg-finding flow.

**PR title:** `feat(blocks-property-leasing-pipeline): FCRA workflow + AdverseActionNotice (ADR 0057)`

### Phase 4 — Jurisdiction policy taxonomy (~1–2h)

Author `Sunfish.Leasing.JurisdictionRules@1.0.0` charter at `icm/00_intake/output/starter-taxonomies-v1-leasing-2026-04-29.md` (mirrors `Sunfish.Signature.Scopes` charter).

Initial nodes:

```text
us-fed.fha-protected-classes
  - race
  - color
  - religion
  - sex
  - familial-status
  - national-origin
  - disability
us-state.ca.unruh-civil-rights-act
  - additional-protected-classes (sexual-orientation, gender-identity, marital-status, etc.)
us-state.ny.tenant-protection-act
us-fed.fcra
  - 60-day-dispute-window
  - mandatory-§615-language
us-fed.fair-housing-act
  - prohibited-questions-list
  - source-of-income-rules
```

Seed loaded into `Foundation.Taxonomy` per W#31's pattern; `IJurisdictionPolicyResolver` (ADR 0060) consumes for jurisdiction-specific rules.

**Gate:** charter file authored; seed loadable; resolver returns expected rules per jurisdiction.

**PR title:** `feat(foundation-taxonomy): Sunfish.Leasing.JurisdictionRules@1.0.0 charter + seed`

### Phase 5 — Public-input boundary integration (~2–3h)

Consumes ADR 0059's `InquirySubmission` post target. Bridge route from ADR 0059 calls into `ILeasingPipelineService.AcceptInquiryAsync(InquirySubmission, CancellationToken)`.

Inquiry validation:
- CAPTCHA already verified by ADR 0059's 5-layer defense
- Email format + MX check (mirror ADR 0052 amendment A1)
- Listing exists + Published + tenant matches
- No-pre-screening of FHA-protected fields (Demographics not collected at inquiry tier)

On accept: emit audit, mint Prospect-tier macaroon (7-day TTL), email verification link.

**Gate:** end-to-end test from `InquirySubmission` (ADR 0059) → Inquiry entity → Prospect promotion + macaroon issuance.

**PR title:** `feat(blocks-property-leasing-pipeline): public-input boundary + Prospect promotion`

### Phase 6 — Showing-notice compliance + Audit emission (~2–3h)

Showings call `IEntryComplianceChecker` from ADR 0060 (when shipped — halt-condition #4).

12 new `AuditEventType` per ADR 0057:
- `InquiryAccepted`, `InquiryRejected`
- `ProspectPromoted`, `ApplicantPromoted`
- `ApplicationSubmitted`, `ApplicationAccepted`, `ApplicationDeclined`, `ApplicationWithdrawn`
- `BackgroundCheckRequested`, `BackgroundCheckCompleted`
- `AdverseActionNoticeIssued`
- `LeasingPipelineCapabilityRevoked`

`LeasingPipelineAuditPayloadFactory` mirroring established W#31/W#19/W#20/W#21/W#18 patterns. Demographic data NEVER appears in audit payloads (FHA-defense at audit tier).

**Gate:** 12 event types ship; factory works; demographics never leak into audit; showing-compliance check fires correctly.

**PR title:** `feat(blocks-property-leasing-pipeline): showing-compliance + 12 AuditEventType`

### Phase 7 — Cross-package wiring + apps/docs (~2h)

- ADR 0051 `IPaymentGateway` for application-fee collection (uses W#19 Phase 0 stub if Stage 06 not shipped)
- ADR 0054 `ISignatureCapture` for application + adverse-action signatures
- ADR 0028 lease creation interface boundary (LeaseOffer → Lease handoff defined; lease creation deferred to W#27 Leases EXTEND)
- `apps/docs/blocks/property-leasing-pipeline/` overview + FHA-defense + FCRA-workflow + jurisdiction-rules pages

**Gate:** cross-package wiring works in InMemory mode; apps/docs builds.

**PR title:** `feat(blocks-property-leasing-pipeline): cross-package wiring + apps/docs`

### Phase 8 — Ledger flip (~0.5h)

Update `icm/_state/active-workstreams.md` row #22 → `built`. Append last-updated footer entry.

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | Package scaffold + FHA-defense entity types | 3–4 |
| 2 | State machine + capability promotion | 3–4 |
| 3 | FCRA workflow + AdverseActionNotice | 3–4 |
| 4 | Jurisdiction policy taxonomy charter | 1–2 |
| 5 | Public-input boundary + Inquiry → Prospect | 2–3 |
| 6 | Showing-compliance + 12 AuditEventType | 2–3 |
| 7 | Cross-package wiring + apps/docs | 2 |
| 8 | Ledger flip | 0.5 |
| **Total** | | **16.5–22.5h** |

---

## Halt conditions

- **ADR 0060 not yet shipped** when Phase 6 needs `IEntryComplianceChecker` → write `cob-question-*`; XO may stub the interface ahead of full ADR 0060 Stage 06
- **ADR 0059 (`Inquiry` boundary contract) not shipped** when Phase 5 runs → halt; ADR 0059 hand-off needs to ship first
- **`IPaymentGateway` real implementation needed** at Phase 7 → use W#19 Phase 0 stub; real gateway is post-Stage-06 per ADR 0051
- **`IBackgroundCheckProvider` real provider** — beyond scope; InMemory stub suffices for Phase 1
- **FCRA legal-review pass** — content of `AdverseActionNotice.FcraStatement` should attorney-pass before production; Phase 1 ships with best-effort language + revisit trigger
- **Demographic-data-leak in audit** — ANY test that surfaces demographic data in `AuditPayload.Body` is a halt-condition + design failure; fix immediately

---

## Acceptance criteria

- [ ] `blocks-property-leasing-pipeline` package builds with full XML doc + nullability + `required`
- [ ] `Application.Demographics` is encrypted-only; `IApplicationDecisioner` interface NEVER receives demographic data (compile-time enforced via type system)
- [ ] State machine refuses invalid transitions
- [ ] Capability promotion gate enforces Anonymous → Prospect → Applicant
- [ ] FCRA-mandated language in `AdverseActionNotice.FcraStatement`
- [ ] 60-day dispute window enforced (`DisputeWindowExpiresAt`)
- [ ] `Sunfish.Leasing.JurisdictionRules@1.0.0` charter loaded into Foundation.Taxonomy
- [ ] Showing-compliance check fires on showing schedule (per ADR 0060)
- [ ] 12 new `AuditEventType` constants
- [ ] No demographic data appears in any audit payload (FHA-defense)
- [ ] `apps/docs/blocks/property-leasing-pipeline/` pages exist
- [ ] Ledger row #22 → `built`

---

## References

- [ADR 0057](../../docs/adrs/0057-leasing-pipeline-fair-housing.md) — full substrate spec
- [ADR 0059](../../docs/adrs/0059-public-listing-surface.md) — `InquirySubmission` boundary contract
- [ADR 0060](../../docs/adrs/0060-right-of-entry-compliance-framework.md) — `IEntryComplianceChecker` for showings
- [ADR 0046](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) — `EncryptedField` for demographics
- [ADR 0049](../../docs/adrs/0049-audit-trail-substrate.md) — audit emission (with FHA-defense at audit tier)
- [ADR 0051](../../docs/adrs/0051-foundation-integrations-payments.md) — `IPaymentGateway` for application fees
- [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — application + adverse-action signatures
- [ADR 0056](../../docs/adrs/0056-foundation-taxonomy-substrate.md) — `Sunfish.Leasing.JurisdictionRules` charter
- [ADR 0043](../../docs/adrs/0043-trust-model-and-threat-delegation.md) — capability gradient
- [W#19 Work Orders hand-off](./property-work-orders-stage06-handoff.md) — Phase 0 Money/ThreadId stubs reused
- [W#31 Foundation.Taxonomy hand-off](./foundation-taxonomy-phase1-stage06-handoff.md) — taxonomy charter pattern
