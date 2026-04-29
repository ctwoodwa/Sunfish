# ADR 0060 — Right-of-Entry Compliance Framework

**Status:** Proposed (2026-04-29; awaiting council review + acceptance)
**Date:** 2026-04-29
**Author:** XO (research session)
**Pipeline variant:** `sunfish-feature-change` (new substrate `Foundation.JurisdictionPolicy` + extension to `blocks-maintenance.WorkOrderEntryNotice`)

**Resolves:** No standalone intake (right-of-entry framework was scoped inside [`property-work-orders-intake-2026-04-28.md`](../../icm/00_intake/output/property-work-orders-intake-2026-04-28.md) §"Entry compliance & notice"). This ADR ships the jurisdiction-policy framework that ADR 0053's `WorkOrderEntryNotice` entity consumes.

---

## Context

US tenancy law gives the operator (BDFL / property manager / vendor) a right to enter a tenant-occupied unit only under specific conditions: with proper advance notice (typically 24–48h), within reasonable hours, for a permitted purpose (repair, inspection, showing, emergency). The exact rules vary by jurisdiction:

- **California:** 24h written notice; 8a–5p only; specific permitted purposes (CCP §1954)
- **New York City:** "reasonable advance notice" (no fixed hours); narrower permitted purposes
- **Utah:** 24h notice for non-emergency; no time-of-day restriction
- **Federal HUD subsidized housing:** layered on top of state rules; additional disclosure requirements

ADR 0053 defines `WorkOrderEntryNotice` as the entity that records a notice was given. This ADR specifies **the policy framework** that determines whether a given entry attempt is compliant — what notice was required, when it was given, whether the timing is legal, what the permitted purposes are, what audit trail is required for a defensible compliance posture.

Phase 2 commercial intake's six BDFL tenants operate in 1–2 jurisdictions today; the framework must extensibly accommodate per-jurisdiction policy without code changes when a new jurisdiction surfaces (Phase 3+).

---

## Decision drivers

- **Jurisdiction policy is data, not code.** Hard-coding California rules into `blocks-maintenance` blocks New York onboarding. Policy lives in a registry.
- **Compliance is provable.** Every entry attempt has an audit trail tying notice → entry → purpose; a tenant lawsuit must be answerable from the audit log.
- **Default posture is conservative.** When in doubt, require more notice / narrower hours / clearer purpose. Operators can relax via per-tenant policy override; can't relax via "we forgot to set the policy."
- **Reuses ADR 0049 audit substrate.** Every notice + every entry + every override emits an audit record per the existing pattern.
- **Reuses W#31 Foundation.Taxonomy.** Entry purposes are taxonomy-classified per `Sunfish.Entry.Purposes@1.0.0` (charter to be authored under ADR 0056 starter taxonomies).

---

## Considered options

### Option A — `Foundation.JurisdictionPolicy` substrate + per-jurisdiction policy registry [RECOMMENDED]

New foundation-tier substrate `packages/foundation-jurisdiction-policy/` containing:
- `JurisdictionId` (NAICS-style code; e.g., `US-CA`, `US-NY-NYC`, `US-UT-SLC`)
- `EntryPolicy` value object (`MinimumNoticeHours`, `PermittedHoursOfDay`, `PermittedPurposes : IReadOnlyList<TaxonomyClassification>`, `EmergencyExceptions`)
- `IJurisdictionPolicyRegistry` interface
- Default seed: `Sunfish.JurisdictionPolicy.Defaults@1.0.0` for ~8 common US jurisdictions
- Override mechanism per tenant (e.g., a more-restrictive policy for a specific unit)

`WorkOrderEntryNotice` (per ADR 0053) consumes this via `IJurisdictionPolicyResolver`.

- **Pro:** Policy is data; new jurisdictions add via seed update, no code change
- **Pro:** Foundation-tier means every consumer (W#19 work orders, W#22 leasing-pipeline showings, W#25 inspection visits) gets the same compliance check
- **Pro:** Reuses W#31 taxonomy for permitted-purpose classification (admin-defined extensibility)
- **Con:** New foundation package; package count grows (mild)

**Verdict:** Recommended. The policy registry pattern is the right shape; foundation tier is the right level.

### Option B — Inline per-jurisdiction policy into `blocks-maintenance.WorkOrder`

Each `WorkOrderEntryNotice` carries its policy inline (notice-hours field + permitted-hours field + purpose enum).

- **Pro:** No new package
- **Con:** Every consumer (work orders, leasing showings, inspections) duplicates the policy fields → code duplication, drift risk
- **Con:** Adding a new jurisdiction requires changes across multiple consumer blocks

**Verdict:** Rejected. Duplication risk + can't share defaults.

### Option C — External jurisdiction-policy service (third-party API)

Outsource jurisdiction-policy lookup to a third-party legal-tech API (e.g., legal-research vendors).

- **Pro:** Always-current rules
- **Con:** Network dependency; legal-tech APIs are expensive + low-volume-tenant-unfriendly
- **Con:** Vendor neutrality (ADR 0013) becomes an issue
- **Con:** Local-first principle: BDFL needs to operate offline; jurisdiction policy must work in Anchor without network

**Verdict:** Rejected. Local-first + offline operation requires policy lives in the substrate, not behind a network call.

---

## Decision

**Adopt Option A.** New `packages/foundation-jurisdiction-policy/` substrate. Per-jurisdiction `EntryPolicy` value objects; `IJurisdictionPolicyRegistry` resolves policy by `JurisdictionId`; `Sunfish.JurisdictionPolicy.Defaults@1.0.0` seeds 8 common US jurisdictions; tenant overrides supported.

### Initial contract surface

```csharp
namespace Sunfish.Foundation.JurisdictionPolicy;

public readonly record struct JurisdictionId(string Code)
{
    // ISO-style; "US-CA", "US-NY", "US-NY-NYC", "US-UT-SLC", "US-FED-HUD" etc.
    public static JurisdictionId Parse(string code) => /* validate format */;
}

public sealed record EntryPolicy
{
    public required JurisdictionId Jurisdiction { get; init; }
    public required int MinimumNoticeHours { get; init; }                     // typically 24 or 48
    public required PermittedHoursOfDay PermittedHours { get; init; }         // see below
    public required IReadOnlyList<TaxonomyClassification> PermittedPurposes { get; init; } // ADR 0056; refs Sunfish.Entry.Purposes
    public required EmergencyExceptionPolicy EmergencyExceptions { get; init; }
    public required string CitationReference { get; init; }                   // legal cite ("CCP §1954" / "NYC Admin Code §27-2008")
    public required DateTimeOffset PolicyEffectiveAt { get; init; }
    public DateTimeOffset? PolicyRetiredAt { get; init; }                     // null while active
}

public readonly record struct PermittedHoursOfDay(TimeOnly EarliestStart, TimeOnly LatestEnd)
{
    public static PermittedHoursOfDay Daytime => new(new TimeOnly(8, 0), new TimeOnly(17, 0));   // 8a–5p
    public static PermittedHoursOfDay BusinessHours => new(new TimeOnly(9, 0), new TimeOnly(18, 0)); // 9a–6p
    public static PermittedHoursOfDay Anytime => new(TimeOnly.MinValue, TimeOnly.MaxValue);     // 24/7 — emergency / vacant
}

public sealed record EmergencyExceptionPolicy
{
    public required bool AllowEntryWithoutNotice { get; init; }               // typically true for genuine emergency
    public required string EmergencyDefinition { get; init; }                 // jurisdiction-specific definition
    public required bool RequirePostHocNotice { get; init; }                  // notice within X hours after entry
    public int? PostHocNoticeWithinHours { get; init; }
}

public interface IJurisdictionPolicyRegistry
{
    Task<EntryPolicy> ResolveForAsync(JurisdictionId jurisdiction, CancellationToken ct);

    /// <summary>Resolve with tenant-override stack — per-property &gt; per-tenant &gt; jurisdiction default.</summary>
    Task<EntryPolicy> ResolveForAsync(JurisdictionId jurisdiction, TenantId tenant, PropertyId? property, CancellationToken ct);
}

public interface IJurisdictionPolicySeedSource
{
    /// <summary>Returns Sunfish.JurisdictionPolicy.Defaults@1.0.0 seed.</summary>
    IReadOnlyList<EntryPolicy> Defaults();
}

// Compliance-check API — consumed by ADR 0053 WorkOrderEntryNotice + ADR 0057 showing-notice + ADR 0025 inspection-notice
public interface IEntryComplianceChecker
{
    /// <summary>Returns Compliant or a structured non-compliance reason.</summary>
    Task<EntryComplianceResult> CheckAsync(EntryAttempt attempt, EntryPolicy policy, CancellationToken ct);
}

public sealed record EntryAttempt
{
    public required DateTimeOffset PlannedEntryAt { get; init; }
    public required DateTimeOffset NoticeGivenAt { get; init; }
    public required TaxonomyClassification Purpose { get; init; }             // ADR 0056
    public required bool IsEmergency { get; init; }
    public required IdentityRef NotifiedParty { get; init; }                  // tenant
    public required IdentityRef NotifyingParty { get; init; }                 // operator (BDFL/spouse/contractor)
}

public abstract record EntryComplianceResult
{
    public sealed record Compliant : EntryComplianceResult;
    public sealed record NonCompliant(string Reason, IReadOnlyList<string> CitedRules) : EntryComplianceResult;
    public sealed record EmergencyOverride(string EmergencyJustification, bool RequiresPostHocNotice) : EntryComplianceResult;
}
```

### Default seed (`Sunfish.JurisdictionPolicy.Defaults@1.0.0`)

Phase 2.1 ships 8 default policies:

| JurisdictionId | Notice | Hours | Citation |
|---|---|---|---|
| `US-CA` | 24h written | 8a–5p | CCP §1954 |
| `US-NY` | "reasonable advance" (24h policy default) | 9a–6p | RPL §235-f (default; NYC adds rules) |
| `US-NY-NYC` | 24h written | 9a–6p | NYC Admin Code §27-2008 |
| `US-UT` | 24h | Anytime (no statutory hours; policy default 8a–7p) | Utah Code §57-22-4(2) |
| `US-TX` | 24h notice | 8a–6p (reasonable hours doctrine) | TPC §92.0081 |
| `US-WA` | 48h written notice | 8a–6p | RCW §59.18.150 |
| `US-FED-HUD` | 48h (overlays state rules; additional HUD disclosure) | per state | 24 CFR §966.4 |
| `US-DEFAULT` | 48h conservative | 9a–5p | Conservative fallback for unknown jurisdiction; tenant must override |

`US-DEFAULT` is the safety-net — when the BDFL hasn't explicitly set a jurisdiction for a property, this conservative policy applies. Operators can't accidentally enter under-noticed by failing to configure jurisdiction.

Permitted purposes per default seed (initial set; admin can extend per ADR 0056):

```text
- inspection.routine
- inspection.move-in
- inspection.move-out
- inspection.emergency
- repair.requested-by-tenant
- repair.preventative
- repair.emergency
- showing.with-prospect
- showing.with-applicant
- vacant-unit-access
```

### Tenant override mechanism

Operators can configure more-restrictive policies (cannot loosen below jurisdiction floor):

- **Per-tenant override** (`OverridePolicy` on the tenant configuration): e.g., "always require 48h regardless of jurisdiction"
- **Per-property override** (`OverridePolicy` on the property record): e.g., "this single-family rental is a senior-living arrangement; require 72h notice"
- **Per-tenant-leaseholder override** (per leaseholder configuration in ADR 0028 lease): e.g., "this leaseholder has medical reasons requiring 7 days notice"

Override stack resolution: `per-leaseholder > per-property > per-tenant > jurisdiction default`. Most-restrictive wins; can never loosen below jurisdiction floor.

### Compliance-check flow (ADR 0053 WorkOrderEntryNotice integration)

When `IMaintenanceService.RecordEntryNoticeAsync` is called per ADR 0053:

1. Resolve `EntryPolicy` via `IJurisdictionPolicyRegistry` for the property's jurisdiction
2. Construct `EntryAttempt` from the `WorkOrderEntryNotice` fields
3. Call `IEntryComplianceChecker.CheckAsync`
4. If `NonCompliant`: throw `EntryNoticeComplianceException` with `CitedRules`; record audit emit `EntryNoticeRejected` (per ADR 0049); BDFL must adjust notice (more advance time / different hours / different purpose)
5. If `EmergencyOverride`: record `EntryEmergencyOverride` audit + flag for required post-hoc notice
6. If `Compliant`: proceed; emit `EntryNoticeCompliant` audit

### Audit emission (ADR 0049 substrate)

5 new `AuditEventType` constants:

```csharp
public static readonly AuditEventType EntryNoticeCompliant = new("EntryNoticeCompliant");
public static readonly AuditEventType EntryNoticeRejected = new("EntryNoticeRejected");
public static readonly AuditEventType EntryEmergencyOverride = new("EntryEmergencyOverride");
public static readonly AuditEventType EntryPostHocNoticeProvided = new("EntryPostHocNoticeProvided");
public static readonly AuditEventType JurisdictionPolicyOverrideApplied = new("JurisdictionPolicyOverrideApplied");
```

`JurisdictionAuditPayloadFactory` per the established W#31 + W#19 + W#20 + W#21 pattern.

---

## Consequences

### Positive

- Jurisdiction policy is structurally extensible — new jurisdiction ships as taxonomy seed update + EntryPolicy data, no code change
- Default conservative posture (`US-DEFAULT` 48h) prevents accidental under-notice on misconfigured properties
- Override stack supports BDFL operational discretion within compliance bounds
- Audit trail makes compliance defensible — every notice + every override + every rejection logged
- Substrate is foundation-tier — consumed by W#19 (work orders), W#22 (leasing showings), W#25 (inspections) without duplication
- Reuses W#31 Foundation.Taxonomy for permitted-purpose classification

### Negative

- New foundation package (`foundation-jurisdiction-policy`) — package count grows
- Default seed is approximate; legal-grade accuracy requires per-jurisdiction attorney review (revisit trigger named)
- Override-stack resolution adds complexity; consumers must understand precedence

### Trust impact / Security & privacy

- **Compliance is enforced at substrate boundary**, not by reviewer discipline — `IEntryComplianceChecker` returns rejection that callers can't ignore (throws if attempted to bypass)
- **Audit trail is mandatory** — every entry-related action emits via ADR 0049
- **No tenant PII** in jurisdiction policies — only operator-side identifiers in audit
- **Emergency-override requires explicit emergency definition** — can't claim "emergency" without naming the condition (recorded in audit)

---

## Compatibility plan

### Existing callers

`WorkOrderEntryNotice` (ADR 0053) consumes this substrate via `IEntryComplianceChecker`. Since W#19 ships before W#60, the compliance check is added in W#19's Phase 6 cross-package wiring (or as a Phase 6.5 if needed). No retrofit required for ADR 0053; the compliance check fits naturally into the entry-notice recording flow.

### Affected packages

| Package | Change |
|---|---|
| `packages/foundation-jurisdiction-policy` | **New** — substrate types + interfaces + default seed + InMemory implementations |
| `packages/foundation-taxonomy` (per ADR 0056) | **Consumed** — `Sunfish.Entry.Purposes@1.0.0` taxonomy charter (charter file authored alongside this ADR's hand-off) |
| `packages/blocks-maintenance` (W#19) | **Modified** — `IMaintenanceService.RecordEntryNoticeAsync` calls `IEntryComplianceChecker` |
| `packages/blocks-leases` (W#27 future) | **Consumed** — leasing showings consume same compliance check |
| `packages/blocks-inspections` (W#25 done) | **Modified (future)** — inspection visits consume same compliance check |
| `apps/docs/foundation/jurisdiction-policy/` | **New** — substrate + default-seed + override-mechanism documentation |

### Migration

No existing data to migrate (new substrate). On adoption, all existing tenants default to `US-DEFAULT` policy; BDFL configures actual jurisdictions via per-tenant override at onboarding.

---

## Implementation checklist

- [ ] `packages/foundation-jurisdiction-policy` package with `JurisdictionId` + `EntryPolicy` + `PermittedHoursOfDay` + `EmergencyExceptionPolicy` + interfaces (full XML doc + nullability + `required`)
- [ ] `IJurisdictionPolicyRegistry` + `InMemoryJurisdictionPolicyRegistry` implementing override stack
- [ ] `IEntryComplianceChecker` + `DefaultEntryComplianceChecker` implementation
- [ ] `Sunfish.JurisdictionPolicy.Defaults@1.0.0` seed shipping 8 jurisdictions
- [ ] `Sunfish.Entry.Purposes@1.0.0` taxonomy charter (for ADR 0056 starter taxonomies; lives in `icm/00_intake/output/`)
- [ ] 5 new `AuditEventType` constants in kernel-audit
- [ ] `JurisdictionAuditPayloadFactory` mirroring established patterns
- [ ] W#19 Phase 6 (cross-package wiring) updated to call `IEntryComplianceChecker` on `RecordEntryNoticeAsync`
- [ ] `apps/docs/foundation/jurisdiction-policy/` overview + override-mechanism + adding-new-jurisdiction docs
- [ ] Tests: 8 default-jurisdiction policies parse correctly; override stack resolution; compliance check rejects under-notice + wrong-hours + impermissible-purpose; emergency override path; post-hoc notice flow

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-J1 | `JurisdictionId` format — ISO-style codes vs. flat strings vs. hierarchical (`/US/CA/SF`)? | Stage 02 — recommend `US-CA`-style flat with optional sub-codes (`US-NY-NYC`); avoids parsing complexity while supporting state+city granularity |
| OQ-J2 | Default seed accuracy — these are paralegal-grade citations, not attorney-reviewed. Phase 2.1 ship as-is or attorney-pass first? | Phase 2.1 ship as best-effort + revisit trigger for attorney pass; defaults are conservative enough that "wrong" means "more-restrictive than required" which is safe |
| OQ-J3 | Federal HUD policy stack — does HUD layer on top of state, or replace it? | Stage 02 — recommend HUD layers (more-restrictive of state-or-HUD); existing legal pattern; verify with attorney pass |
| OQ-J4 | Tenant-leaseholder override — recorded in lease document or separate? | Stage 02 — separate `TenantPolicyOverride` entity; lease document referenced but not source-of-truth (override changes possible mid-lease) |
| OQ-J5 | Effective-at / retired-at dating — does each policy version need versioning? | Stage 02 — recommend yes; ADR 0056 taxonomy substrate already does this; mirror the pattern |

---

## Revisit triggers

- **First non-US jurisdiction** (international tenant) — substrate is US-shaped; revisit `JurisdictionId` semantics + emergency-exception model
- **Attorney-reviewed default seed** — Phase 2.2 milestone; defaults are best-effort until attorney-passed; revisit trigger when a tenant has an actual entry-related dispute that turns on policy interpretation
- **Per-record-class CRDT classification** — `EntryPolicy` is CP-class (jurisdiction overrides shouldn't conflict under partition); revisit if AP semantics surface a real need
- **Local + state + federal stack** — first time HUD overlay produces a contradiction with state law; need policy-stack semantics
- **Tenant-side notice acknowledgment** — currently operator-asserts notice was given; tenant-side acknowledgment (signature per ADR 0054?) might become a Phase 3+ requirement

---

## References

### Predecessor and sister ADRs

- [ADR 0008](./0008-foundation-multitenancy.md) — multi-tenancy + per-tenant overrides
- [ADR 0013](./0013-foundation-integrations.md) — provider-neutrality (no third-party API; substrate owns policy)
- [ADR 0028](./0028-per-record-class-consistency.md) — `EntryPolicy` is CP-class
- [ADR 0049](./0049-audit-trail-substrate.md) — audit emission for compliance trail
- [ADR 0053](./0053-work-order-domain-model.md) — `WorkOrderEntryNotice` consumes this substrate
- [ADR 0056](./0056-foundation-taxonomy-substrate.md) — `Sunfish.Entry.Purposes` taxonomy charter
- [ADR 0057](./0057-leasing-pipeline-fair-housing.md) — leasing showings consume this same compliance check (Phase 2.2 wiring)

### Roadmap and intakes

- [Property-work-orders intake](../../icm/00_intake/output/property-work-orders-intake-2026-04-28.md) — original entry-compliance scope
- [Cluster INDEX](../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — sequencing

### External

- California CCP §1954 — landlord entry rules
- New York City Admin Code §27-2008 — multiple dwelling entry rules
- Utah Code §57-22-4(2) — Utah landlord-tenant entry
- Texas Property Code §92.0081 — Texas rental entry
- Washington RCW §59.18.150 — Washington landlord entry
- 24 CFR §966.4 — HUD subsidized housing entry rules

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options: foundation substrate, inline-into-block, third-party API. Option A chosen with explicit rejection rationale for B (duplication + drift) and C (network dependency violates local-first).
- [x] **FAILED conditions / kill triggers.** 5 named: international jurisdiction, attorney-pass milestone, CP/AP reclassification, federal-state stack contradiction, tenant-side acknowledgment.
- [x] **Rollback strategy.** Greenfield substrate. Rollback = revert ADR + revert `foundation-jurisdiction-policy` + revert W#19 Phase 6 wiring change. `WorkOrderEntryNotice` records continue to work without compliance check (just no enforcement).
- [x] **Confidence level.** **MEDIUM.** Substrate composition is straightforward; policy-data accuracy is the highest-risk surface (mitigated by conservative defaults + attorney-pass revisit trigger).
- [x] **Anti-pattern scan.** None of AP-1, -3, -9, -12, -21 apply for the substrate design. AP-2 (defaults are pre-attorney-review) flagged + named in OQ-J2 + revisit trigger.
- [x] **Revisit triggers.** Five named with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 10 specific tasks. Stage 02 contributor reading this ADR + ADR 0049 + ADR 0053 + ADR 0056 should be able to scaffold without asking for substrate clarification. Default-seed accuracy is paralegal-grade, deliberately.
- [x] **Sources cited.** ADR 0008, 0013, 0028, 0049, 0053, 0056, 0057 referenced. 6 jurisdictional citations external (CCP, NYC Admin, Utah Code, TPC, RCW, CFR).
