# FHA-Defense Layout

The Fair Housing Act (Title VIII, 1968) prohibits discrimination in housing decisions on the basis of seven protected classes: race, color, religion, sex, familial status, national origin, and disability. State laws (e.g., California Unruh, NY TPA) extend the list further (sexual orientation, gender identity, marital status, ancestry, source of income, etc.). See the [Jurisdiction Rules](./jurisdiction-rules.md) page for the full taxonomy.

`Sunfish.Blocks.PropertyLeasingPipeline` enforces FHA compliance **structurally**, not just policy-wise.

## The structural-quarantine pattern

`Application` carries two distinct shapes:

```csharp
public sealed record Application
{
    public required DecisioningFacts Facts { get; init; }            // income, credit, eviction, references — visible to decisioning
    public required DemographicProfile Demographics { get; init; }    // protected-class data — quarantined
    // ... other fields ...
}
```

`IApplicationDecisioner` is the only operator-facing decisioning contract. **It accepts only `DecisioningFacts`:**

```csharp
public interface IApplicationDecisioner
{
    Task<ApplicationDecision> DecideAsync(
        ApplicationId applicationId,
        DecisioningFacts facts,                  // ← the ONLY shape with applicant data
        BackgroundCheckResult? backgroundCheck,
        CancellationToken ct);
}
```

A decisioning implementation cannot read `DemographicProfile` because it never receives one. To do so, an operator would have to take a *separate* dependency — visible to a code reviewer at the type signature alone.

## Reflection-based invariant

Phase 1 ships a unit test (`EntityShapeTests.IApplicationDecisioner_DoesNotAccept_DemographicProfile`) that reflects over `IApplicationDecisioner` + asserts no method parameter is typed `DemographicProfile`. **A failure here is a halt-condition**: the FHA-defense layer has been breached.

## At the audit tier

Phase 6's `LeasingPipelineAuditPayloadFactory` NEVER includes `DemographicProfile` fields in any audit body. `Audit_NeverLeaks_DemographicProfile` is a reflection-based invariant that exercises the full lifecycle with sentinel demographic values + asserts no field name OR value appears in any emitted record's `AuditPayload.Body` — a catch for the case where someone serializes the entire `Application` into audit by mistake.

## What's in `DecisioningFacts`

Non-protected fields — these are where decisioning happens:

- `GrossMonthlyIncome` + `IncomeSource` (employer, self-employment) + `YearsAtIncomeSource`
- `SelfReportedCreditRange` (the actual report comes via `BackgroundCheckResult` from the BG-check provider)
- `PriorEvictionDisclosed`
- `ReferenceCount` + `PriorLandlordNames`
- `DependentCount` (the operational fact, not "familial status" — courts distinguish)
- `AccommodationRequested` (the request alone is not a protected-class indicator; the underlying basis is — and the basis lives in `DemographicProfile`)

## What's in `DemographicProfile`

Protected fields — for HUD reporting + civil-rights compliance only:

- `RaceOrEthnicity`
- `NationalOrigin`
- `Religion`
- `Sex`
- `DisabilityStatus`
- `FamilialStatus`
- `MaritalStatus`
- `IncomeSourceType` (e.g., Section 8 voucher — protected under some state laws)

Storage-side: every field is per-tenant-key encrypted (per ADR 0046 `EncryptedField` from W#32 substrate). Reading any field requires an `IFieldDecryptor` capability + emits a `FieldDecrypted` audit record per read. **Wiring shipped in Phase 9** — see "Phase 9 — structural encryption" below for details.

## Phase 9 — structural encryption (post-W#32)

The reflection-based `IApplicationDecisioner_DoesNotAccept_DemographicProfile` test was the original FHA-defense layer. **Phase 9 (post-W#32 substrate) makes the same claim type-system-enforced rather than test-enforced.**

### Two records, two postures

```csharp
public sealed record DemographicProfileSubmission       // wire form: plaintext, transient
{
    public string? RaceOrEthnicity { get; init; }
    // ... 7 more nullable string fields ...
}

public sealed record DemographicProfile                  // persisted form: encrypted
{
    public EncryptedField? RaceOrEthnicity { get; init; }
    // ... 7 more nullable EncryptedField fields ...
}
```

`SubmitApplicationRequest.Demographics` is typed as `DemographicProfileSubmission` (plaintext); `Application.Demographics` is `DemographicProfile` (encrypted). The `LeasingPipelineService.SubmitApplicationAsync` boundary encrypts every non-null field via `IFieldEncryptor.EncryptAsync` (purpose label `encrypted-field-aes`; per-tenant DEK) and discards plaintext.

### What changes for decisioning code

Nothing — `IApplicationDecisioner` still receives only `DecisioningFacts`. But the FHA-defense claim is now *also* enforced by the type system: even if a decisioning implementation tried to reach into `Application.Demographics`, it would receive `EncryptedField?` values that are opaque without an `IFieldDecryptor` capability the decisioning surface does not hold.

### What changes for legitimate readers

- **HUD reporting tooling** holds an `IDecryptCapability` scoped to compliance reporting; per-field decrypts emit `FieldDecrypted` audits per W#32.
- **Subject Access Request (SAR) handler** (FCRA §609 / GDPR / CCPA) holds an SAR-scoped capability; same audit pattern.
- **Decisioning, scoring, screening, ranking, sorting code** holds no decrypt capability — they cannot reach plaintext even if they tried.

### Defense-in-depth

The original reflection-based audit-leak invariant test (`Audit_NeverLeaks_DemographicProfile`) is retained as a **belt-and-braces tripwire**. Type-system enforcement is now primary; the reflection test is a regression detector against accidental serialization paths that bypass the canonical encrypt-on-write boundary.

### Defense-in-depth: graceful fall-back

When `IFieldEncryptor` is not registered (e.g., test bootstrap, dev-only host), `LeasingPipelineService.SubmitApplicationAsync` drops every demographic field rather than silently retaining plaintext. The application is persisted with an all-null `DemographicProfile`. Plaintext never crosses the persistence boundary in any configuration.

## Legitimate read paths for `DemographicProfile`

Only these:

1. **HUD reporting export** — operator-driven export with audit emission per access
2. **Court-ordered audit** — emergency-access flow per ADR 0046
3. **Self-service** — the prospect viewing their own data via a capability bound to their own `ProspectId`

Decisioning, scoring, screening, recommendation, ranking, sorting — none of these have a legitimate read path. The structural quarantine is the proof.

## Why this matters

Many FHA cases turn on whether decisioning code "could have" consulted protected-class data. With Sunfish's structural quarantine, the answer is provably no — the decisioning interface cannot reach the data without an explicit refactor that would be visible in version control. This is the strongest form of compliance: not "we promise we don't," but "we structurally cannot."

## See also

- [ADR 0057](../../../docs/adrs/0057-leasing-pipeline-fair-housing.md) — Leasing pipeline architecture
- [Jurisdiction Rules](./jurisdiction-rules.md) — FHA + state-law protected-class taxonomy
- [FCRA Workflow](./fcra-workflow.md) — Adjacent regulatory regime
