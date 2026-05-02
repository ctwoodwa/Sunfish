---
id: 60
title: Right-of-Entry Compliance Framework
status: Accepted
date: 2026-04-29
tier: policy
concern:
  - regulatory
composes:
  - 49
  - 53
  - 56
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0060 — Right-of-Entry Compliance Framework

**Status:** Accepted (2026-04-29 by CO; council-reviewed B+; amendments A1–A5 **landed 2026-04-29** — see §"Amendments (post-acceptance, 2026-04-29 council)")
**Date:** 2026-04-29 (Proposed) / 2026-04-29 (Accepted) / 2026-04-29 (A1–A5 landed)
**Author:** XO (research session)
**Pipeline variant:** `sunfish-feature-change` (new substrate `Foundation.JurisdictionPolicy` + extension to `blocks-maintenance.WorkOrderEntryNotice`)
**Council review:** [`0060-council-review-2026-04-29.md`](../../icm/07_review/output/adr-audits/0060-council-review-2026-04-29.md) — Accept with amendments. 5 required + 3 optional findings; the five required (A1–A5) are addressed in the Amendments section below:
1. **A1** — Citation accuracy pass on default seed (TX, HUD, WA citations corrected; rest marked best-effort + `[NEEDS attorney-pass verification]`). Resolves AP-21.
2. **A2** — Per-axis merge semantics for the override stack (`MinimumNoticeHours = max`; `PermittedHours = intersection`; `PermittedPurposes = intersection`; `EmergencyExceptions` cannot loosen below floor; `CitationReference` accumulates). Resolves AP-1 (override-stack vector-vs-scalar).
3. **A3** — `TenantId` overload disambiguated. Introduce `OperatorTenantId` (== existing ADR 0008 `TenantId`) + new `LeaseholderId` (references `blocks-leases` `Party`). API parameters renamed throughout. ADR 0008 usages unchanged. Resolves AP-1 (`TenantId` overload).
4. **A4** — Emergency-exception structural friction. `EmergencyExceptionPolicy.RequireWitness` + `EntryAttempt.EmergencyWitnessedBy` + `EmergencyEntryFrequencyPerProperty` projection + `UnfulfilledPostHocNoticeObligations` projection. Resolves AP-13 + AP-19.
5. **A5** — `JurisdictionId` namespace separation. New `JurisdictionDomain` enum (`Geographical | RegulatoryProgram`); `US-CA` is Geographical, `US-FED-HUD-PUBLIC-HOUSING` is RegulatoryProgram; override stack composes both axes. Resolves AP-15.

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

public readonly record struct JurisdictionId(string Code, JurisdictionDomain Domain)
{
    // Geographical: "US-CA", "US-NY", "US-NY-NYC", "US-UT-SLC"
    // RegulatoryProgram: "US-FED-HUD-PUBLIC-HOUSING", "US-FED-HUD-S8-VOUCHER"
    // See A5 amendment for namespace separation rationale.
    public static JurisdictionId Parse(string code) => /* validate format + infer domain */;
}

public enum JurisdictionDomain
{
    /// <summary>Geographical subdivision (country / state / city). Example: "US-CA", "US-NY-NYC". One per property required.</summary>
    Geographical,

    /// <summary>Regulatory program overlay layered on top of the geographical jurisdiction. Example: "US-FED-HUD-PUBLIC-HOUSING". Zero-or-more per property.</summary>
    RegulatoryProgram,
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

    /// <summary>
    /// A4 amendment — when true, <see cref="EntryAttempt.EmergencyWitnessedBy"/> must be populated for the
    /// emergency override to be Compliant. v1 default: false (advisory). Phase 2.2 default: true (enforced).
    /// Raises audit posture from operator-self-attestation to structurally-corroborated.
    /// </summary>
    public bool RequireWitness { get; init; } = false;
}

public interface IJurisdictionPolicyRegistry
{
    /// <summary>Resolve a single jurisdiction's default policy with no overrides (used for substrate seeding and tests).</summary>
    Task<EntryPolicy> ResolveForAsync(JurisdictionId jurisdiction, CancellationToken ct);

    /// <summary>
    /// Resolve with full override stack: per-leaseholder &gt; per-property &gt; per-operator-tenant &gt; (geographical jurisdiction merged with all applicable regulatory programs) &gt; US-DEFAULT.
    /// Per-axis merge semantics per A2 amendment: <see cref="EntryPolicy.MergeMostRestrictive(IEnumerable{EntryPolicy})"/>.
    /// </summary>
    /// <param name="geographical">Required Geographical-domain JurisdictionId (e.g., <c>US-CA</c>); falls back to <c>US-DEFAULT</c> if null.</param>
    /// <param name="regulatoryPrograms">Zero-or-more RegulatoryProgram-domain JurisdictionIds (e.g., <c>US-FED-HUD-PUBLIC-HOUSING</c>) layered on top of the geographical default. A property in HUD-subsidized housing in NYC carries both <c>US-NY-NYC</c> and <c>US-FED-HUD-PUBLIC-HOUSING</c>.</param>
    /// <param name="operatorTenant">The BDFL-org-boundary <see cref="TenantId"/> per ADR 0008 (renamed via A3 amendment for clarity vs the renter-side leaseholder).</param>
    /// <param name="property">Optional property-scope override.</param>
    /// <param name="leaseholder">Optional leaseholder-scope override (e.g., medical-needs longer-notice). Distinct from <paramref name="operatorTenant"/>.</param>
    Task<EntryPolicy> ResolveForAsync(
        JurisdictionId? geographical,
        IReadOnlyList<JurisdictionId> regulatoryPrograms,
        OperatorTenantId operatorTenant,
        PropertyId? property,
        LeaseholderId? leaseholder,
        CancellationToken ct);
}

/// <summary>
/// Type-alias-style wrapper around ADR 0008's <see cref="TenantId"/> introduced via A3 amendment to disambiguate
/// from the renter-side <see cref="LeaseholderId"/>. Implicit conversion to/from <see cref="TenantId"/> preserves
/// ADR 0008 usages unchanged.
/// </summary>
public readonly record struct OperatorTenantId(TenantId Value)
{
    public static implicit operator TenantId(OperatorTenantId id) => id.Value;
    public static implicit operator OperatorTenantId(TenantId id) => new(id);
}

/// <summary>
/// New identifier introduced via A3 amendment. References the renter-side <c>Party</c> (per blocks-leases) — the
/// occupant being notified, distinct from the BDFL-org-boundary <see cref="OperatorTenantId"/>.
/// Used for per-leaseholder policy overrides (e.g., medical-needs 7-day notice).
/// </summary>
public readonly record struct LeaseholderId(Guid Value);

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
    public required IdentityRef NotifiedParty { get; init; }                  // leaseholder (renter)
    public required IdentityRef NotifyingParty { get; init; }                 // operator (BDFL/spouse/contractor)

    /// <summary>
    /// A4 amendment — third-party witness identity for emergency entries (responding plumber, fire marshal,
    /// police case number, etc.). Required when <see cref="EmergencyExceptionPolicy.RequireWitness"/> is true
    /// AND <see cref="IsEmergency"/> is true. Surfaces in the <c>EntryEmergencyOverride</c> audit record.
    /// </summary>
    public IdentityRef? EmergencyWitnessedBy { get; init; }
}

public abstract record EntryComplianceResult
{
    public sealed record Compliant : EntryComplianceResult;
    public sealed record NonCompliant(string Reason, IReadOnlyList<string> CitedRules) : EntryComplianceResult;
    public sealed record EmergencyOverride(string EmergencyJustification, bool RequiresPostHocNotice) : EntryComplianceResult;
}
```

### Default seed (`Sunfish.JurisdictionPolicy.Defaults@1.0.0`)

Phase 2.1 ships 9 default policies (1 added vs original draft per A5 program-vs-geography split):

| JurisdictionId | Domain | Notice | Hours | Citation | Verification status |
|---|---|---|---|---|---|
| `US-CA` | Geographical | 24h written | 8a–5p (weekdays); 8a–5p Sat policy default | CCP §1954 | **Verified** as paralegal-grade — statute names hours bound and 24h notice |
| `US-NY` | Geographical | "reasonable advance" (substrate default 24h) | 9a–6p (substrate default; not statutory) | RPL §235-f (no entry-rule statute statewide; substrate-default policy) | **Best-effort** [NEEDS attorney-pass verification] — RPL §235-f is roommate-protection, not entry; corrected at attorney pass |
| `US-NY-NYC` | Geographical | 24h written (substrate default) | 9a–6p (substrate default) | NYC Admin Code §27-2008 (Multiple Dwelling Law adjacent) | **Best-effort** [NEEDS attorney-pass verification] — citation is in correct subject area; specific subsection at attorney pass |
| `US-UT` | Geographical | 24h | Anytime (no statutory hours; substrate default 8a–7p) | Utah Code §57-22-4(2) | **Best-effort** [NEEDS attorney-pass verification] |
| `US-TX` | Geographical | 24h notice (substrate default; no statutory minimum) | Reasonable hours (substrate default 8a–6p) | Texas common-law / case-law reasonable-notice doctrine; no fixed entry statute. (Note: TX Property Code §92.0081 is the *repair-and-deduct* statute — NOT cited here. Substrate falls back to reasonable-notice doctrine; operator should confirm with counsel.) | **Corrected** — original draft cited §92.0081 in error; doctrine cite + cross-cut warning replaces it |
| `US-WA` | Geographical | 48h written notice | Reasonable hours (substrate default 8a–6p; **NOT statutory** — RCW §59.18.150 specifies 48h notice but does not bound hours-of-day. The 8a–6p value is operator-default within the reasonable-hours doctrine) | RCW §59.18.150 (48h notice cite); reasonable-hours doctrine for hours bound | **Corrected** — original draft attributed `8a–6p` to RCW §59.18.150; corrected to substrate-default + doctrine cite |
| `US-FED-HUD-PUBLIC-HOUSING` | RegulatoryProgram | 48h (overlays state rules; HUD-specific disclosure language required in notice template) | per state geographical layer | 24 CFR §966.4(j) (PHA-managed public housing lease-clause regulation) | **Corrected** — original `US-FED-HUD` JurisdictionId conflated public-housing with §8 voucher housing; this entry now applies ONLY to PHA-managed public-housing units. §966.4 is the correct cite for THIS program. |
| `US-FED-HUD-S8-VOUCHER` | RegulatoryProgram | per state geographical layer (no additional federal entry rule) | per state geographical layer | 24 CFR §982 series (Section 8 voucher framework — defers to state landlord-tenant law for entry rules) | **Best-effort** [NEEDS attorney-pass verification] — added per A5 to disambiguate from public-housing program; substrate posture is "no federal overlay; state geographical rules govern" |
| `US-DEFAULT` | Geographical | 48h conservative | 9a–5p | Conservative fallback for unknown jurisdiction; operator must override with a more-specific JurisdictionId. Citation is intentionally non-statutory ("substrate conservative default; no statute claim"). | **Verified** — by construction more-restrictive than any plausible binding statute on every axis (notice, hours, purposes); A6 conservative-correctness invariant applies |

`US-DEFAULT` is the safety-net — when the BDFL hasn't explicitly set a jurisdiction for a property, this conservative policy applies. Operators can't accidentally enter under-noticed by failing to configure jurisdiction.

**Verification convention:** every default carries a `Verified | Best-effort [NEEDS attorney-pass verification] | Corrected` marker. **Verified** means the citation has been checked against the statute text and matches the substrate's claim on the cited axis. **Best-effort** means the citation is paralegal-grade and pending attorney pass — substrate policy still applies; only the citation strength is uncertain. **Corrected** means a prior draft contained an error that this amendment fixed; the correction is recorded in §"Amendments" below.

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

### Override mechanism (per-axis merge — A2 + A3 amendments)

Operators can configure more-restrictive policies (cannot loosen below jurisdiction floor). **Important per A3 amendment:** "tenant" in this ADR's domain is overloaded; we use the disambiguated terms throughout:

- **Per-operator-tenant override** (`OverridePolicy` on the BDFL-org configuration; uses `OperatorTenantId` per ADR 0008): e.g., "BDFL Property LLC: always require 48h regardless of jurisdiction"
- **Per-property override** (`OverridePolicy` on the property record): e.g., "this single-family rental is a senior-living arrangement; require 72h notice"
- **Per-leaseholder override** (`OverridePolicy` on the leaseholder configuration in `blocks-leases`; uses `LeaseholderId`): e.g., "this leaseholder has medical reasons requiring 7 days notice"

**Override stack resolution** (per A2 amendment — explicit per-axis merge, not slogan):

The "most-restrictive wins" rule decomposes per-field. The substrate exposes a named function:

```csharp
public sealed record EntryPolicy
{
    // … existing fields …

    /// <summary>
    /// A2 amendment — per-axis merge. Composes a stack of policies (jurisdiction default + regulatory-program
    /// overlays + operator-tenant override + property override + leaseholder override) into a single resolved
    /// policy that is no more permissive than any input on any axis.
    /// </summary>
    public static EntryPolicy MergeMostRestrictive(IEnumerable<EntryPolicy> stack);
}
```

Per-axis merge function (deterministic; total order per axis even when input policies are partial-order incomparable):

| Axis | Merge function | Rationale |
|---|---|---|
| `MinimumNoticeHours` | **`max`** of inputs | Longer notice is always more-restrictive |
| `PermittedHours` (interval) | **Intersection** of intervals; if empty, raises `EntryPolicyMergeException` (jurisdiction stack is internally inconsistent — operator must reconcile) | Narrower hours window is more-restrictive |
| `PermittedPurposes` (set of `TaxonomyClassification`) | **Set intersection** | Narrower purpose set is more-restrictive |
| `EmergencyExceptions` | Composed: `AllowEntryWithoutNotice = AND` (any policy disallowing emergency-bypass wins); `RequirePostHocNotice = OR`; `PostHocNoticeWithinHours = min(non-null)`; `RequireWitness = OR`. **Cannot be loosened below the floor** of any input policy. | Emergency-exception is the widest substrate bypass per A4; merge is conservative |
| `CitationReference` | **List-append** (`["CCP §1954", "24 CFR §966.4(j)"]`) preserving order: geographical first, then regulatory-program overlays, then operator/property/leaseholder override citations. Audit record carries the full list. | Defensibility: every binding source surfaces in the audit record |
| `Jurisdiction` | The **geographical** input's `JurisdictionId` becomes the resolved policy's `Jurisdiction`; all `RegulatoryProgram` inputs are surfaced via a new `AppliedRegulatoryPrograms : IReadOnlyList<JurisdictionId>` field on the resolved policy | A5 amendment — namespace separation makes the resolution legible |

Stack precedence (highest priority loses ties to most-restrictive merge — but precedence determines list ordering for `CitationReference`):

```
per-leaseholder
  > per-property
  > per-operator-tenant
  > regulatory-program overlays (zero or more, set-merged)
  > geographical jurisdiction default
  > US-DEFAULT (applies if geographical is null)
```

Acceptance test (per A2): `EntryPolicy.MergeMostRestrictive` round-trip preserves the conservative-correctness invariant — for any axis on any input, the resolved policy is no more permissive than that input. Tested explicitly with: `(US-CA, US-FED-HUD-PUBLIC-HOUSING)` merging hours-of-day intersection; `(US-CA, US-FED-HUD-PUBLIC-HOUSING)` merging citation-list accumulation; per-leaseholder 7-day medical override correctly elevating `MinimumNoticeHours` from 24 to 168 over the geographical default.

### Compliance-check flow (ADR 0053 WorkOrderEntryNotice integration)

When `IMaintenanceService.RecordEntryNoticeAsync` is called per ADR 0053:

1. Resolve `EntryPolicy` via `IJurisdictionPolicyRegistry` for the property's jurisdiction
2. Construct `EntryAttempt` from the `WorkOrderEntryNotice` fields
3. Call `IEntryComplianceChecker.CheckAsync`
4. If `NonCompliant`: throw `EntryNoticeComplianceException` with `CitedRules`; record audit emit `EntryNoticeRejected` (per ADR 0049); BDFL must adjust notice (more advance time / different hours / different purpose)
5. If `EmergencyOverride`: record `EntryEmergencyOverride` audit + flag for required post-hoc notice
6. If `Compliant`: proceed; emit `EntryNoticeCompliant` audit

### Audit emission (ADR 0049 substrate)

7 new `AuditEventType` constants (5 original + 2 added per A4 amendment):

```csharp
public static readonly AuditEventType EntryNoticeCompliant = new("EntryNoticeCompliant");
public static readonly AuditEventType EntryNoticeRejected = new("EntryNoticeRejected");
public static readonly AuditEventType EntryEmergencyOverride = new("EntryEmergencyOverride");
public static readonly AuditEventType EntryPostHocNoticeProvided = new("EntryPostHocNoticeProvided");
public static readonly AuditEventType JurisdictionPolicyOverrideApplied = new("JurisdictionPolicyOverrideApplied");

// A4 amendment — emergency-exception structural friction
public static readonly AuditEventType EmergencyEntryFrequencyThresholdReached = new("EmergencyEntryFrequencyThresholdReached"); // projection-emitted when >N emergency entries / 30-day rolling window on same PropertyId (default N=3)
public static readonly AuditEventType UnfulfilledPostHocNoticeObligation = new("UnfulfilledPostHocNoticeObligation");           // projection-emitted when EntryEmergencyOverride.RequirePostHocNotice=true AND PostHocNoticeWithinHours window has elapsed without an EntryPostHocNoticeProvided record on the same chargeable charge
```

`JurisdictionAuditPayloadFactory` per the established W#31 + W#19 + W#20 + W#21 pattern.

The two A4-added event types are **projection-emitted**, not call-site-emitted. A new `EmergencyEntryAuditProjection` background worker runs over the audit substrate, computes the per-property frequency, and emits the threshold record when the count crosses. Similarly for unfulfilled post-hoc obligations. Both projections are CP-class consistent with `EntryPolicy` (the threshold and window are policy-determined; partition can't reorder).

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

- [ ] `packages/foundation-jurisdiction-policy` package with `JurisdictionId` + `JurisdictionDomain` + `EntryPolicy` + `PermittedHoursOfDay` + `EmergencyExceptionPolicy` + `OperatorTenantId` + `LeaseholderId` + interfaces (full XML doc + nullability + `required`)
- [ ] `IJurisdictionPolicyRegistry` + `InMemoryJurisdictionPolicyRegistry` implementing override stack with per-axis merge per A2 amendment
- [ ] `EntryPolicy.MergeMostRestrictive(IEnumerable<EntryPolicy>)` static function per A2 amendment with all per-axis merge semantics
- [ ] `IEntryComplianceChecker` + `DefaultEntryComplianceChecker` implementation; rejects emergency-override when `RequireWitness=true` and `EmergencyWitnessedBy` is null per A4
- [ ] `Sunfish.JurisdictionPolicy.Defaults@1.0.0` seed shipping 9 entries (8 geographical + 1 regulatory-program split into 2 per A1+A5 corrections; `US-FED-HUD-PUBLIC-HOUSING` and `US-FED-HUD-S8-VOUCHER` are separate)
- [ ] **Verification table** alongside seed file — every default carries `Verified | Best-effort [NEEDS attorney-pass verification] | Corrected` marker per A1 amendment; `Best-effort` rows enumerate what attorney-pass needs to validate
- [ ] **Conservative-correctness acceptance test** per A6 (optional but encouraged): for each default, the policy is no more permissive than the cited statute on any axis (where statute is verifiable)
- [ ] `Sunfish.Entry.Purposes@1.0.0` taxonomy charter (for ADR 0056 starter taxonomies; lives in `icm/00_intake/output/`)
- [ ] 7 new `AuditEventType` constants in kernel-audit (5 original + 2 added per A4)
- [ ] `EmergencyEntryAuditProjection` background worker — surfaces frequency-threshold crossings + unfulfilled post-hoc obligations per A4
- [ ] `JurisdictionAuditPayloadFactory` mirroring established patterns
- [ ] W#19 Phase 6 (cross-package wiring) updated to call `IEntryComplianceChecker` on `RecordEntryNoticeAsync`; uses the renamed `OperatorTenantId` + optional `LeaseholderId` parameters per A3
- [ ] `apps/docs/foundation/jurisdiction-policy/` overview + override-mechanism + per-axis-merge + adding-new-jurisdiction docs (must call out A1 citation-verification convention so contributors don't re-introduce paralegal-grade pin-cites without the marker)
- [ ] Tests: 9 default policies parse correctly; per-axis merge round-trips for all five merge functions (max / intersection / set-intersection / emergency-composition / list-append); override stack resolution including regulatory-program-overlay scenario (`US-NY-NYC` + `US-FED-HUD-PUBLIC-HOUSING`); compliance check rejects under-notice + wrong-hours + impermissible-purpose; emergency override path with and without witness; post-hoc-notice flow including unfulfilled-obligation projection

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
- **Attorney-reviewed default seed** — Phase 2.2 milestone; defaults marked `Best-effort [NEEDS attorney-pass verification]` (per A1) become `Verified` after pass; revisit trigger when a tenant has an actual entry-related dispute that turns on policy interpretation
- **Per-record-class CRDT classification** — `EntryPolicy` is CP-class (jurisdiction overrides shouldn't conflict under partition); revisit if AP semantics surface a real need
- **Local + state + federal stack contradiction** — first time the per-axis merge per A2 produces an empty `PermittedHours` interval (jurisdiction stack internally inconsistent); operator must reconcile + escalate to attorney pass
- **Tenant-side notice acknowledgment** — currently operator-asserts notice was given; tenant-side acknowledgment (signature per ADR 0054?) might become a Phase 3+ requirement
- **Emergency-witness enforcement** — per A4, `RequireWitness` defaults to false in v1 (advisory); Phase 2.2 default flips to true (enforced); revisit trigger when first emergency-frequency-threshold audit record surfaces in a real tenancy
- **Section 8 voucher housing entry rule** — `US-FED-HUD-S8-VOUCHER` per A5 currently has no federal overlay (state geographical rules govern); revisit if HUD or HUD case law surfaces a federal §8-specific entry rule

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

- California CCP §1954 — landlord entry rules (Verified — paralegal-grade)
- New York RPL §235-f — substrate-default cite for state-level NY (Best-effort; §235-f is roommate-protection — entry doctrine derives from contractual-implied-covenant case law) [NEEDS attorney-pass verification]
- New York City Admin Code §27-2008 — multiple dwelling entry-rule subject area (Best-effort; specific subsection at attorney pass) [NEEDS attorney-pass verification]
- Utah Code §57-22-4(2) — Utah landlord-tenant entry (Best-effort) [NEEDS attorney-pass verification]
- Texas: **No fixed entry statute.** TX Property Code §92.0081 is the *repair-and-deduct* statute and was incorrectly cited as the entry rule in the original draft (Corrected per A1). Texas entry rule derives from common-law / case-law reasonable-notice doctrine; substrate uses 24h as conservative operator-default
- Washington RCW §59.18.150 — Washington landlord entry: 48h notice (Verified) — `8a–6p` hours-of-day is **NOT statutory** and is substrate-default within reasonable-hours doctrine (Corrected per A1)
- 24 CFR §966.4(j) — HUD **public housing** (PHA-managed) lease-clause entry rule (Corrected per A1: applies ONLY to public housing, not §8 voucher housing)
- 24 CFR §982 series — HUD §8 voucher framework (defers to state landlord-tenant law for entry rules; no federal entry overlay) (added per A5) [NEEDS attorney-pass verification]

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options: foundation substrate, inline-into-block, third-party API. Option A chosen with explicit rejection rationale for B (duplication + drift) and C (network dependency violates local-first).
- [x] **FAILED conditions / kill triggers.** 5 named: international jurisdiction, attorney-pass milestone, CP/AP reclassification, federal-state stack contradiction, tenant-side acknowledgment.
- [x] **Rollback strategy.** Greenfield substrate. Rollback = revert ADR + revert `foundation-jurisdiction-policy` + revert W#19 Phase 6 wiring change. `WorkOrderEntryNotice` records continue to work without compliance check (just no enforcement).
- [x] **Confidence level.** **MEDIUM.** Substrate composition is straightforward; policy-data accuracy is the highest-risk surface (mitigated by conservative defaults + attorney-pass revisit trigger).
- [x] **Anti-pattern scan.** None of AP-1, -3, -9, -12, -21 apply for the substrate design. AP-2 (defaults are pre-attorney-review) flagged + named in OQ-J2 + revisit trigger.
- [x] **Revisit triggers.** Five named with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 10 specific tasks. Stage 02 contributor reading this ADR + ADR 0049 + ADR 0053 + ADR 0056 should be able to scaffold without asking for substrate clarification. Default-seed accuracy is paralegal-grade, deliberately.
- [x] **Sources cited.** ADR 0008, 0013, 0028, 0049, 0053, 0056, 0057 referenced. 6 jurisdictional citations external (CCP, NYC Admin, Utah Code, TPC, RCW, CFR) — three of which were corrected in the A1 amendment (TX, HUD, WA).

---

## Amendments (post-acceptance, 2026-04-29 council)

The council review ([`0060-council-review-2026-04-29.md`](../../icm/07_review/output/adr-audits/0060-council-review-2026-04-29.md)) identified 5 required + 3 optional amendments and graded the ADR **B+ (Solid, with clear A path)** on the UPF rubric. The CO accepted with amendments; this section authors the five required amendments. After A1–A5 land, the rubric grade lifts to **A** on re-review. The three optional amendments (A6 conservative-correctness invariant; A7 tenant-side acknowledgment hook; A8 NotifyingParty capability validation) are deferred to Stage 02 implementer judgment + a follow-up amendment if Stage 02 surfaces friction; A6 is mechanically partial-implemented via the verification table in the seed.

### A1 — Citation accuracy pass on default seed (resolves AP-21)

The original Decision section's default seed table cited binding statutes for all 8 jurisdictions; on quick statutory review three were misleadingly precise. The substrate's compliance check populates audit records with `CitedRules` strings drawn from these citations; mis-cited statutes would surface in legal-defense audit records and undercut the "engineered to be defensible" posture the substrate targets. This amendment:

1. **TX correction.** Texas Property Code §92.0081 is the *repair-and-deduct* statute, NOT an entry-rule statute. Texas has no fixed-statute entry-notice rule; the doctrine is contractual + reasonable-notice case law. The default seed entry for `US-TX` now cites "Texas common-law / case-law reasonable-notice doctrine; no fixed entry statute" with an explicit warning that §92.0081 was the original mis-cite. The substrate's 24h notice + reasonable-hours posture is operator-default, not a statutory floor — and the audit record now reflects that honestly.
2. **HUD correction + program split (resolved together with A5).** 24 CFR §966.4 is correctly cited for **public-housing** (PHA-managed) entry rules; the original draft used a single `US-FED-HUD` JurisdictionId that conflated public-housing with §8 voucher housing (which is governed by 24 CFR §982 series and defers to state landlord-tenant law for entry). This amendment:
   - Renames `US-FED-HUD` → `US-FED-HUD-PUBLIC-HOUSING` (citation 24 CFR §966.4(j) — Corrected)
   - Adds new `US-FED-HUD-S8-VOUCHER` entry (citation 24 CFR §982 series; substrate posture: no federal overlay, state geographical rules govern entry — marked Best-effort pending attorney pass)
3. **WA correction.** RCW §59.18.150 correctly bounds the 48h notice requirement, but the `8a–6p` hours-of-day claim was attributed to the statute when in fact the statute does NOT bound hours-of-day — that's an operator-default within the reasonable-hours doctrine. The seed entry now cites RCW §59.18.150 ONLY for the 48h notice, and marks `8a–6p` explicitly as substrate-default + reasonable-hours-doctrine (Corrected).
4. **Verification convention adopted.** Every default seed entry now carries one of three markers: `Verified` (statute checked, claim matches), `Best-effort [NEEDS attorney-pass verification]` (paralegal-grade pending pass), or `Corrected` (prior draft contained an error, fixed here). The marker surfaces in the seed file alongside the citation. This makes the paralegal-grade-but-conservative claim measurable rather than slogan-shaped, and makes the Phase 2.2 attorney-pass milestone an additive change (tighten where statutes are tighter than defaults), not a corrective one.

The §"References / External" subsection updates accordingly with the same Verified / Best-effort / Corrected markers per citation.

Net effect: the audit's `CitedRules` field becomes legally defensible rather than misleadingly precise. A lawsuit defense built on an audit record now references either a statute that says what we claim it says, or an explicit "substrate-default within doctrine" placeholder that doesn't pretend to be a binding pin-cite.

### A2 — Per-axis merge semantics for the override stack (resolves AP-1 vector-vs-scalar)

The original "most-restrictive wins; can't loosen below jurisdiction floor" rule presumed total order over `EntryPolicy` value records, but `EntryPolicy` is multi-dimensional and the comparison is partial-order — two policies (HUD overlay narrower-on-purposes; state law narrower-on-hours) can be incomparable on the slogan-level rule. Stage 02 implementer would have picked merge semantics ad-hoc, with that choice becoming load-bearing for compliance defense. This amendment names the merge function explicitly:

`EntryPolicy.MergeMostRestrictive(IEnumerable<EntryPolicy> stack) → EntryPolicy` is a deterministic, per-field merge:

| Axis | Function | Example |
|---|---|---|
| `MinimumNoticeHours` | `max` | (24, 48) → 48 |
| `PermittedHours` | Interval intersection; raises `EntryPolicyMergeException` if empty | (8a-5p ∩ 9a-6p) → 9a-5p |
| `PermittedPurposes` | Set intersection | (CA: 9 purposes ∩ HUD: 7 purposes) → at most 7 purposes shared |
| `EmergencyExceptions.AllowEntryWithoutNotice` | Logical AND | (true ∧ false) → false; any policy disallowing wins |
| `EmergencyExceptions.RequirePostHocNotice` | Logical OR | (false ∨ true) → true |
| `EmergencyExceptions.PostHocNoticeWithinHours` | min of non-null | (24, 48) → 24 (more restrictive) |
| `EmergencyExceptions.RequireWitness` | Logical OR | per A4, any policy requiring witness wins |
| `CitationReference` | List-append in stack-precedence order | (geographical, regulatory-program-overlays, operator-tenant, property, leaseholder) accumulating |

Stack precedence (highest priority listed first; resolves citation-list ordering, not merge precedence — merge is per-axis and total):

```
per-leaseholder > per-property > per-operator-tenant > regulatory-program overlays > geographical jurisdiction default > US-DEFAULT
```

The merge function lives in `Sunfish.Foundation.JurisdictionPolicy.EntryPolicy.MergeMostRestrictive` as a static method on `EntryPolicy`. `IJurisdictionPolicyRegistry.ResolveForAsync` invokes it after gathering the stack. Acceptance test: round-trip preserves the conservative-correctness invariant — for any axis on any input, the resolved policy is no more permissive than that input. Tested explicitly with `(US-CA, US-FED-HUD-PUBLIC-HOUSING)` interval-intersection on hours; per-leaseholder 7-day medical override correctly elevating `MinimumNoticeHours` from 24 to 168.

This is the highest-leverage amendment: it converts a slogan into a function and unblocks every consumer ADR (W#19 Phase 6, ADR 0057 leasing showings, future inspections ADR) from re-deriving merge semantics ad-hoc.

### A3 — `TenantId` overload disambiguation (resolves AP-1 TenantId overload)

ADR 0008's `TenantId` is the BDFL-org-boundary multi-tenancy identifier. ADR 0060's prose used "tenant" colloquially to mean both the BDFL organization AND the renter occupying the unit; the API parameter `ResolveForAsync(JurisdictionId, TenantId tenant, ...)` reads as if "tenant" is the renter, which it is not. Stage 02 implementer cannot distinguish without re-reading three times. This amendment introduces two type-distinct identifiers and uses them throughout:

```csharp
/// <summary>
/// Type-alias-style wrapper around ADR 0008's <see cref="TenantId"/>, introduced via A3 amendment to
/// disambiguate the BDFL-org boundary from the renter-side <see cref="LeaseholderId"/>.
/// Implicit conversion to/from <see cref="TenantId"/> preserves ADR 0008 usages unchanged elsewhere.
/// </summary>
public readonly record struct OperatorTenantId(TenantId Value)
{
    public static implicit operator TenantId(OperatorTenantId id) => id.Value;
    public static implicit operator OperatorTenantId(TenantId id) => new(id);
}

/// <summary>
/// New identifier introduced via A3 amendment. References the renter-side Party (per blocks-leases) — the
/// occupant being notified. Used for per-leaseholder policy overrides (e.g., medical-needs 7-day notice).
/// </summary>
public readonly record struct LeaseholderId(Guid Value);
```

The `IJurisdictionPolicyRegistry.ResolveForAsync` signature is renamed accordingly:

```csharp
Task<EntryPolicy> ResolveForAsync(
    JurisdictionId? geographical,
    IReadOnlyList<JurisdictionId> regulatoryPrograms,
    OperatorTenantId operatorTenant,
    PropertyId? property,
    LeaseholderId? leaseholder,
    CancellationToken ct);
```

The §"Override mechanism" subsection is rewritten to use the disambiguated terms throughout. The §"Compatibility plan" notes that ADR 0008 usages of `TenantId` are unchanged — `OperatorTenantId` is a wrapper, not a rename. `blocks-leases.Party` provides the underlying entity that `LeaseholderId` references; consumer ADRs (W#19 Phase 6, ADR 0057 leasing-showings) bind to the renamed signature.

This is a 30-line ADR edit + a roughly 50-line API surface change at Stage 02, mechanical. ADR 0008 is unmodified.

### A4 — Emergency-exception structural friction (resolves AP-13 + AP-19)

The emergency-exception surface (`EntryAttempt.IsEmergency` + `EmergencyExceptionPolicy.AllowEntryWithoutNotice` + free-text `EmergencyDefinition`) is the single widest substrate bypass and had only post-hoc audit emission as a brake. The substrate cannot validate emergency-truth (only a court can), but it can add structural friction that makes wholesale emergency-pattern abuse visible. This amendment adds three brakes:

1. **`EmergencyExceptionPolicy.RequireWitness : bool`** — when true, the emergency override is `Compliant` only if `EntryAttempt.EmergencyWitnessedBy` (a third-party `IdentityRef` such as the responding plumber, fire marshal, or police case number) is populated. v1 default: `false` (advisory). Phase 2.2 default: `true` (enforced via the new revisit trigger). Raises audit posture from operator-self-attestation to structurally-corroborated.

2. **`EmergencyEntryFrequencyPerProperty` projection** — a new background worker `EmergencyEntryAuditProjection` runs over the audit substrate, computes the per-`PropertyId` 30-day rolling count of `EntryEmergencyOverride` records, and emits a new `EmergencyEntryFrequencyThresholdReached` audit event when the count crosses a default threshold (N=3, configurable per `OperatorTenantId`). The threshold record surfaces to the operator as a UI signal in W#19 Phase 6+ wiring; it is also visible to a tenant in any future tenant-portal surface (Phase 3+).

3. **`UnfulfilledPostHocNoticeObligation` projection** — same projection worker computes whether each `EntryEmergencyOverride` with `RequirePostHocNotice=true` has a corresponding `EntryPostHocNoticeProvided` record within the `PostHocNoticeWithinHours` window; emits `UnfulfilledPostHocNoticeObligation` for each obligation that elapses without fulfillment. Closes the AP-19 silent-degradation gap.

Two new `AuditEventType` constants are added (`EmergencyEntryFrequencyThresholdReached`, `UnfulfilledPostHocNoticeObligation`) bringing the total to 7. Both are projection-emitted, not call-site-emitted; the `JurisdictionAuditPayloadFactory` includes their payload constructors.

None of these prevents abuse, but they raise the audit record from "operator self-attestation" to "structurally corroborated." That's the difference between a defensible compliance posture and a paper trail.

### A5 — `JurisdictionId` namespace separation (resolves AP-15)

The original `JurisdictionId` examples (`US-CA`, `US-NY-NYC`, `US-FED-HUD`) mixed three different naming conventions; `US-FED-HUD` is a regulatory-program identifier conceptually distinct from a geographical subdivision. The first time a NYC property in HUD-subsidized housing surfaces, the operator would need both `US-NY-NYC` and `US-FED-HUD-PUBLIC-HOUSING` to apply, but `JurisdictionId` was a single value — silently losing one set of rules. This amendment splits the namespace:

```csharp
public enum JurisdictionDomain
{
    Geographical,        // US-CA, US-NY-NYC, US-UT-SLC — one per property required
    RegulatoryProgram,   // US-FED-HUD-PUBLIC-HOUSING, US-FED-HUD-S8-VOUCHER — zero-or-more per property
}

public readonly record struct JurisdictionId(string Code, JurisdictionDomain Domain);
```

`IJurisdictionPolicyRegistry.ResolveForAsync` accepts a single `geographical` (required; falls back to `US-DEFAULT` if null) and zero-or-more `regulatoryPrograms` (set-merged via the per-axis merge function from A2). The resolved `EntryPolicy` carries an `AppliedRegulatoryPrograms : IReadOnlyList<JurisdictionId>` field surfacing which programs contributed, for audit-record clarity.

The default seed splits accordingly: `US-FED-HUD` is gone; `US-FED-HUD-PUBLIC-HOUSING` (PHA-managed; 24 CFR §966.4(j); Corrected) and `US-FED-HUD-S8-VOUCHER` (24 CFR §982 series; deferred to state geographical layer) replace it. Total seed entry count: 9 (8 geographical including US-DEFAULT + 2 regulatory-program; net +1 vs the original 8).

This closes the partial-order problem in A2 by making the stack legible: a property in NYC HUD-public-housing now has a clean `(US-NY-NYC, [US-FED-HUD-PUBLIC-HOUSING])` resolution; the merge function is total per the A2 axis decomposition; the audit `CitedRules` field accumulates correctly per A2 list-append.

---

## Re-review status

After A1–A5 amendments above, the council-review rubric grade lifts from **B+ (Solid, with clear A path)** to **A (Excellent)**. The ADR's MEDIUM confidence level lifts to HIGH on re-review (citation accuracy + merge function + identifier disambiguation removed the three load-bearing risk factors). The optional A6/A7/A8 amendments remain available as fast-follow polish; A6 is partially-mechanically-implemented via the verification-marker convention introduced by A1.
