# Sunfish.Blocks.PropertyLeasingPipeline

Block for the rental-application lifecycle: Inquiry → Prospect → Application → BackgroundCheck → AdverseAction | LeaseOffer.

Implements [ADR 0057 — Leasing pipeline + Fair Housing](../../docs/adrs/0057-leasing-pipeline-fair-housing.md) (Accepted with A1 amendment).

## What this ships

### Models

- **`Inquiry`** — anonymous-tier submission from the public-listings surface.
- **`Prospect`** — email-verified inquirer; carries the `MacaroonToken` for the W#28 capability-tier surface.
- **`Application`** — Prospect-submitted application; carries `Facts` (decisioning) + `Demographics` (FHA-quarantined).
- **`DecisioningFacts`** — non-protected-class fields visible to decisioning (income, references, eviction-disclosed, dependent-count, etc.).
- **`DemographicProfile`** — protected-class fields (race, national origin, religion, sex, disability, familial status, marital status, source-of-income); per-field `EncryptedField?` (W#22 Phase 9 / W#32 substrate).
- **`DemographicProfileSubmission`** — plaintext wire form; consumed at the `SubmitApplicationAsync` boundary; never persisted.
- **`BackgroundCheckRequest`** + `BackgroundCheckResult` — FCRA workflow envelopes.
- **`AdverseActionNotice`** — FCRA §615 mandatory-language record (60-day dispute window + CRA address per FCRA).
- **`LeaseOffer`** — the precursor to ADR 0028 `Lease`.

### Services

- **`ILeasingPipelineService`** + `InMemoryLeasingPipelineService` — full lifecycle state machine with capability promotion + audit emission + payment-gateway hookup + `GetProspectByEmailAsync` lookup.
- **`IPublicInquiryService`** — boundary contract for the W#28 Bridge inquiry POST surface.
- **`IInquiryValidator`** — public-input boundary check (Phase 5).
- **`IApplicationDecisioner`** — operator-facing decisioning contract; **takes only `DecisioningFacts`**, never `DemographicProfile` (FHA-defense type-system enforcement).
- **`IBackgroundCheckProvider`** + `InMemoryBackgroundCheckProvider` — pluggable BG-check provider seam.
- **`AdverseActionNoticeGenerator`** — FCRA §615-compliant adverse-action notice generator with 60-day dispute window.

### Capabilities

- **`AnonymousCapability`** — minted by the inquiry POST route; 30-min TTL; carries to the email-verification flow.
- **`ApplicantCapability`** — minted by `ConfirmApplicationAndPromoteAsync` after payment + signature confirmation.

### Audit

- 12 `AuditEventType` constants per ADR 0057 (`InquiryAccepted`, `InquiryRejected`, `ProspectPromoted`, `ApplicantPromoted`, `ApplicationSubmitted`, `ApplicationAccepted`, `ApplicationDeclined`, `ApplicationWithdrawn`, `BackgroundCheckRequested`, `BackgroundCheckCompleted`, `AdverseActionNoticeIssued`, `LeasingPipelineCapabilityRevoked`).
- `LeasingPipelineAuditPayloadFactory` mirrors the W#31 / W#19 / W#21 / W#18 conventions.
- **Demographic data NEVER appears in audit payloads** (FHA-defense at audit tier; reflection-based regression test retained as belt-and-braces tripwire post Phase 9 type-system enforcement).

## FHA-defense layout

The cornerstone of this block. `IApplicationDecisioner` accepts only `DecisioningFacts`, never `DemographicProfile`. Phase 1 enforced this with a reflection-based unit test; Phase 9 (post-W#32) makes it type-system-enforced — `DemographicProfile.<protected>` is `EncryptedField?` and decisioning code holds no `IFieldDecryptor` capability.

See `apps/docs/blocks/property-leasing-pipeline/fha-defense.md` for the full walkthrough.

## DI

```csharp
services.AddSingleton<ILeasingPipelineService, InMemoryLeasingPipelineService>();
// Wires Prospect promoter, inquiry validator, audit emission, payment
// gateway, and field encryptor through factory parameters.
```

The `Bridge` SaaS posture's factory delegate auto-pulls `IFieldEncryptor` via W#32's `AddSunfishRecoveryCoordinator()`; when not registered, demographic submissions drop to all-null `DemographicProfile` (defense-in-depth).

## ADR map

- [ADR 0057](../../docs/adrs/0057-leasing-pipeline-fair-housing.md) — leasing-pipeline architecture
- [ADR 0046](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) — `EncryptedField` / `IFieldDecryptor` substrate (W#22 Phase 9 consumer)
- [ADR 0051](../../docs/adrs/0051-foundation-integrations-payments.md) — payment-gateway seam
- [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — application-signature seam

## See also

- [apps/docs Overview](../../apps/docs/blocks/property-leasing-pipeline/overview.md)
- [FHA-Defense Layout](../../apps/docs/blocks/property-leasing-pipeline/fha-defense.md)
- [FCRA Workflow](../../apps/docs/blocks/property-leasing-pipeline/fcra-workflow.md)
- [Jurisdiction Rules](../../apps/docs/blocks/property-leasing-pipeline/jurisdiction-rules.md)
