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

Storage-side: each field is per-tenant-key encrypted (per ADR 0046 `EncryptedField` from W#32 substrate; wiring lands in a follow-up phase). Reading any field requires a separate capability + emits an audit record per read.

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
