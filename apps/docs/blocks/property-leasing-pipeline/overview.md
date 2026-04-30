# Property Leasing Pipeline

`Sunfish.Blocks.PropertyLeasingPipeline` ships the end-to-end leasing flow: **Inquiry → Prospect → Application → Decision → LeaseOffer**. The package is built around the FHA-defense layout (per [ADR 0057](../../../docs/adrs/0057-leasing-pipeline-fair-housing.md)) — protected-class data is structurally quarantined from decisioning code, not just policy-restricted.

## Lifecycle

```text
Anonymous viewer browses listings (ADR 0059)
    ↓
Inquiry submitted via Bridge (5-layer defense in W#28 Phase 5a + IInquiryValidator)
    ↓ (email-verification flow)
Prospect (capability-tier-1 macaroon, 7-day TTL)
    ↓ (fee paid + signed application)
Application.Submitted
    ↓
Application.AwaitingBackgroundCheck
    ↓ (provider returns report)
Application.AwaitingDecision
    ↓ (operator decides)
Application.{Accepted | Declined | Withdrawn}
    ↓ (Accepted only)
LeaseOffer.Issued (boundary contract; ADR 0028 lease creation lives in `blocks-leases`)
    ↓
Lease.Draft  (W#27, EXTEND)
```

## Entity surface

| Entity | Purpose |
|---|---|
| `Inquiry` | Public-facing inquiry from an Anonymous viewer; pre-email-verification. |
| `Prospect` | Email-verified viewer; carries a macaroon-backed `ProspectCapability` from `blocks-public-listings`. |
| `Application` | Rental application; **FHA-defense quarantined** (Facts vs Demographics). |
| `BackgroundCheckResult` | FCRA-compliant report from `IBackgroundCheckProvider`. |
| `AdverseActionNotice` | FCRA §615(a) notice with mandatory statement + 60-day dispute window. |
| `LeaseOffer` | Pre-lease offer issued on Accept; precursor to `Lease` (per ADR 0028). |

## Service surface

| Interface | Owner | Purpose |
|---|---|---|
| `IPublicInquiryService` | this package | Boundary called by Bridge after the 5-layer abuse defense passes. |
| `ILeasingPipelineService` | this package | Operator-facing orchestrator (promote, submit, decide, withdraw). |
| `IInquiryValidator` | this package | Domain-side validation hooks (listing-existence + tenant-match + Published-state + email-format). |
| `IApplicationDecisioner` | this package | Decisioning surface — ACCEPTS ONLY `DecisioningFacts`. The structural quarantine is the FHA defense. |
| `IBackgroundCheckProvider` | this package | Pluggable consumer-report provider; `InMemoryBackgroundCheckProvider` for test/demo. |
| `IAdverseActionNoticeGenerator` | this package | FCRA §615 notice generator with verbatim mandatory statement. |
| `ICapabilityPromoter` | `blocks-public-listings` | Anonymous → Prospect promotion via macaroon (ADR 0032). |

## Cross-package wiring (Phase 7)

`InMemoryLeasingPipelineService` accepts these optional dependencies; each is null-disabled:

| Wiring | When invoked |
|---|---|
| `ICapabilityPromoter` (per ADR 0043 addendum) | `PromoteInquiryToProspectAsync` mints the Prospect macaroon. |
| `IInquiryValidator` (Phase 5) | `SubmitInquiryAsync` validates listing-existence + tenant-match + Published-state + email-format. |
| `IAuditTrail` + `IOperationSigner` (Phase 6) | Emits one of 12 AuditEventType constants per lifecycle event (FHA-defense at audit tier — demographic data NEVER leaks). |
| `IPaymentGateway` (per ADR 0051) | `SubmitApplicationAsync` authorizes the application fee; the auth handle is per-Application. |

## Audit emission (12 `AuditEventType`)

| Event | Emitted on |
|---|---|
| `InquiryAccepted` | `SubmitInquiryAsync` validation pass |
| `InquiryRejected` | `SubmitInquiryAsync` validation fail (before throw) |
| `ProspectPromoted` | `PromoteInquiryToProspectAsync` |
| `ApplicantPromoted` | `ConfirmApplicationAndPromoteAsync` |
| `ApplicationSubmitted` | `SubmitApplicationAsync` |
| `BackgroundCheckCompleted` | `RecordBackgroundCheckAsync` |
| `ApplicationAccepted` | `RecordDecisionAsync` (Accept) |
| `ApplicationDeclined` | `RecordDecisionAsync` (Decline) |
| `ApplicationWithdrawn` | `WithdrawApplicationAsync` |
| `BackgroundCheckRequested` | (forward-compat; service-level kickoff op deferred) |
| `AdverseActionNoticeIssued` | (forward-compat; service-level issuance op deferred) |
| `LeasingPipelineCapabilityRevoked` | (forward-compat; revocation flow deferred) |

## See also

- [FHA-defense layout](./fha-defense.md)
- [FCRA workflow](./fcra-workflow.md)
- [Jurisdiction rules](./jurisdiction-rules.md)
- [ADR 0057](../../../docs/adrs/0057-leasing-pipeline-fair-housing.md) — Leasing pipeline architecture
- [ADR 0059](../../../docs/adrs/0059-public-listing-surface.md) — Public listing surface (W#28)
- [ADR 0043](../../../docs/adrs/0043-capability-tiers.md) — Anonymous / Prospect / Applicant tiers
- [W#22 hand-off](../../../icm/_state/handoffs/property-leasing-pipeline-stage06-handoff.md)
