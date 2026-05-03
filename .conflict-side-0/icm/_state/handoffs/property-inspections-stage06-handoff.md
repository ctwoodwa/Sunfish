# Hand-off — Property-Inspections extension to `blocks-inspections` (first-slice)

**From:** research session
**To:** sunfish-PM session
**Created:** 2026-04-29
**Status:** `ready-to-build` (gated on workstream #24 Equipment rename merging — `EquipmentConditionAssessment` references `EquipmentId`)
**Spec source:** Cluster intake [`property-inspections-intake-2026-04-28.md`](../../00_intake/output/property-inspections-intake-2026-04-28.md) (revised 2026-04-28: EXTEND disposition) + [`property-ops-cluster-vs-existing-reconciliation-2026-04-28.md`](../../07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md) workstream #25 row + UPF naming review Rule 3 (extend over parallel) + Rule 4 (Equipment not Asset).
**Approval:** User accepted EXTEND disposition + Equipment rename 2026-04-29. This hand-off operationalizes that decision.
**Estimated cost:** ~3–4 hours sunfish-PM (extension to existing block; mechanical additions; no behavior change to existing Inspection lifecycle)
**Pipeline:** `sunfish-feature-change` (additive; no breaking changes to existing API)
**Blocked by:** workstream #24 Equipment rename merging (`Sunfish.Blocks.PropertyEquipment.EquipmentId` must exist before this hand-off's Phase 4 references it)

---

## Context (one paragraph)

`blocks-inspections` already ships a full inspection domain: Inspection + InspectionTemplate + InspectionChecklistItem + InspectionResponse + InspectionReport + Deficiency (with severity + status) + InspectionPhase + InspectionItemKind + IInspectionsService + InMemoryInspectionsService + ScheduleInspection / CreateTemplate / RecordDeficiency operations. Cluster contribution is **two additions**: (1) an `InspectionTrigger` enum + `Inspection.Trigger` nullable field categorizing inspections by purpose (Annual / MoveIn / MoveOut / PostRepair / OnDemand), and (2) a new `EquipmentConditionAssessment` child entity (parallel to Deficiency, but for proactive condition rating of physical equipment — water heaters, HVAC, etc.) with FK to `Sunfish.Blocks.PropertyEquipment.Equipment`. Plus seed data: move-in and move-out checklist templates. **No changes to existing Inspection lifecycle, Deficiency semantics, or service contracts.** Extension is purely additive.

---

## Phases (binary gates)

### Phase 1 — `InspectionTrigger` enum + `Inspection.Trigger` field

**Files:**

- **NEW** `packages/blocks-inspections/Models/InspectionTrigger.cs`

```csharp
namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Categorizes the purpose / context of an inspection.
/// </summary>
public enum InspectionTrigger
{
    /// <summary>Routine annual inspection (default for property-management cadence).</summary>
    Annual,

    /// <summary>Move-in baseline inspection at lease start; documents unit + equipment condition before tenancy.</summary>
    MoveIn,

    /// <summary>Move-out delta inspection at lease end; documents unit + equipment condition for security-deposit reconciliation.</summary>
    MoveOut,

    /// <summary>Verification inspection after maintenance/repair work; confirms work-order completion quality.</summary>
    PostRepair,

    /// <summary>Ad-hoc inspection initiated by owner or contractor; not on a regular cadence.</summary>
    OnDemand,
}
```

- **EDIT** `packages/blocks-inspections/Models/Inspection.cs`

Add `Trigger` as a **nullable** field (preserves backward compatibility — existing Inspection records have `Trigger = null` meaning "trigger not specified / pre-revision"):

```csharp
public sealed record Inspection(
    InspectionId Id,
    InspectionTemplateId TemplateId,
    EntityId UnitId,
    string InspectorName,
    DateOnly ScheduledDate,
    InspectionPhase Phase,
    Instant? StartedAtUtc,
    Instant? CompletedAtUtc,
    IReadOnlyList<InspectionResponse> Responses,
    InspectionTrigger? Trigger = null);  // NEW; nullable for backward compat
```

(Default-value parameter at end of positional record params; preserves existing constructor-call shape.)

- **EDIT** `packages/blocks-inspections/Services/ScheduleInspectionRequest.cs` (verify exact filename)

Add an optional `Trigger` field to the request shape so callers can specify trigger at scheduling time:

```csharp
public sealed record ScheduleInspectionRequest(
    InspectionTemplateId TemplateId,
    EntityId UnitId,
    string InspectorName,
    DateOnly ScheduledDate,
    InspectionTrigger? Trigger = null);
```

**PASS gate:** `dotnet build packages/blocks-inspections/` green; existing tests pass (no semantics changed); new `InspectionTrigger.cs` has full XML doc.

### Phase 2 — `EquipmentConditionAssessment` child entity

**Files:**

- **NEW** `packages/blocks-inspections/Models/EquipmentConditionAssessmentId.cs` — opaque record struct + JSON converter, mirror existing `DeficiencyId.cs` pattern
- **NEW** `packages/blocks-inspections/Models/ConditionRating.cs`

```csharp
namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Condition rating for a piece of equipment observed during an inspection.
/// </summary>
public enum ConditionRating
{
    /// <summary>Equipment is in good working order; no concerns.</summary>
    Good,

    /// <summary>Equipment functions but shows wear; serviceable.</summary>
    Fair,

    /// <summary>Equipment is functional but degraded; replacement should be planned.</summary>
    Poor,

    /// <summary>Equipment has failed or is non-operational; immediate replacement needed.</summary>
    Failed,
}
```

- **NEW** `packages/blocks-inspections/Models/EquipmentConditionAssessment.cs`

```csharp
using Sunfish.Foundation.Assets.Common;
using Sunfish.Blocks.PropertyEquipment;  // for EquipmentId — gated on workstream #24 rename merging

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// A condition assessment of a specific piece of equipment recorded during an inspection.
/// Distinct from <see cref="Deficiency"/>: this is a proactive condition rating for any
/// equipment (good, fair, poor, failed); a Deficiency is by definition something wrong.
/// </summary>
public sealed record EquipmentConditionAssessment
{
    public required EquipmentConditionAssessmentId Id { get; init; }
    public required InspectionId InspectionId { get; init; }
    public required EquipmentId EquipmentId { get; init; }
    public required ConditionRating Condition { get; init; }
    public int? ExpectedRemainingLifeYears { get; init; }
    public string? Observations { get; init; }
    public string? Recommendations { get; init; }
    public IReadOnlyList<string> PhotoBlobRefs { get; init; } = Array.Empty<string>();  // placeholder; photo capture deferred (matches Deficiency pattern)
    public required Instant ObservedAtUtc { get; init; }
}
```

**PASS gate:** Compiles; full XML doc; round-trip JSON test on `EquipmentConditionAssessmentId`.

**Note on EquipmentId reference:** if workstream #24 Equipment rename is not yet merged, `Sunfish.Blocks.PropertyEquipment.EquipmentId` does not exist. **Halt and write a memory note** rather than proceed; do not use `AssetId` placeholder (Rule 4 forbids the cluster `Asset` term going forward).

### Phase 3 — Service methods for `EquipmentConditionAssessment`

**Files:**

- **NEW** `packages/blocks-inspections/Services/RecordEquipmentConditionRequest.cs`

```csharp
public sealed record RecordEquipmentConditionRequest(
    InspectionId InspectionId,
    EquipmentId EquipmentId,
    ConditionRating Condition,
    int? ExpectedRemainingLifeYears = null,
    string? Observations = null,
    string? Recommendations = null);
```

- **EDIT** `packages/blocks-inspections/Services/IInspectionsService.cs`

Add three methods (mirror existing Deficiency pattern):

```csharp
/// <summary>
/// Records an equipment condition assessment linked to an inspection and returns the created record.
/// </summary>
ValueTask<EquipmentConditionAssessment> RecordEquipmentConditionAsync(
    RecordEquipmentConditionRequest request,
    CancellationToken ct = default);

/// <summary>
/// Streams all equipment condition assessments associated with the given inspection.
/// </summary>
IAsyncEnumerable<EquipmentConditionAssessment> ListEquipmentConditionsAsync(
    InspectionId inspectionId,
    CancellationToken ct = default);

/// <summary>
/// Streams the most recent equipment condition assessments for a specific equipment item across all inspections.
/// Useful for "show me this water heater's condition history."
/// </summary>
IAsyncEnumerable<EquipmentConditionAssessment> ListConditionHistoryForEquipmentAsync(
    EquipmentId equipmentId,
    CancellationToken ct = default);
```

- **EDIT** `packages/blocks-inspections/Services/InMemoryInspectionsService.cs`

Implement the three methods. Storage is `ConcurrentDictionary<EquipmentConditionAssessmentId, EquipmentConditionAssessment>`. The history-by-equipment query is a filter scan; in-memory only, so O(N) is fine for the first-slice.

**PASS gate:** Compiles; methods have full XML doc; new tests for each method (record, list-by-inspection, list-history-by-equipment).

### Phase 4 — Move-in / move-out delta projection (lightweight)

**Files:**

- **EDIT** `packages/blocks-inspections/Services/IInspectionsService.cs`

Add one query method that pairs move-in and move-out inspections for a given Unit + computes a simple delta:

```csharp
/// <summary>
/// Returns paired move-in vs move-out inspection responses + condition assessments
/// for a given unit. Used by security-deposit reconciliation. Returns null if either
/// move-in or move-out inspection is missing.
/// </summary>
ValueTask<MoveInOutDelta?> GetMoveInOutDeltaAsync(
    EntityId unitId,
    CancellationToken ct = default);
```

- **NEW** `packages/blocks-inspections/Services/MoveInOutDelta.cs`

```csharp
public sealed record MoveInOutDelta(
    EntityId UnitId,
    Inspection MoveIn,
    Inspection MoveOut,
    IReadOnlyList<ResponseDelta> ResponseDeltas,
    IReadOnlyList<EquipmentConditionDelta> EquipmentConditionDeltas);

public sealed record ResponseDelta(
    InspectionChecklistItemId ItemId,
    string MoveInValue,
    string MoveOutValue,
    bool Changed);

public sealed record EquipmentConditionDelta(
    EquipmentId EquipmentId,
    ConditionRating MoveInCondition,
    ConditionRating MoveOutCondition,
    bool Degraded);  // true if MoveOut < MoveIn (e.g., Good → Fair)
```

- **EDIT** `InMemoryInspectionsService.cs` to implement `GetMoveInOutDeltaAsync`. Implementation: find most recent `Trigger = MoveIn` inspection for unit; find most recent `Trigger = MoveOut` for same unit; pair their responses + condition assessments.

**PASS gate:** Method works; tests cover (a) both inspections present + delta computed, (b) only move-in present → null, (c) only move-out → null, (d) neither → null.

### Phase 5 — Seed data: move-in + move-out checklist templates

**Files:**

- **EDIT** `apps/kitchen-sink/` seed code (verify path; mirror existing seed pattern)

Add two `InspectionTemplate` records with seeded checklist items:

```
"Move-In Checklist" — 6 to 12 items covering: walls, floors, appliances on/working, windows operate, locks function, no leaks, photos of any pre-existing damage.

"Move-Out Checklist" — same items as Move-In + additional cleanliness, repair-of-damage, return-of-keys items.
```

Items are `InspectionChecklistItem` records. Use existing `InspectionItemKind` values (PassFail + FreeText + Photo as needed).

**PASS gate:** kitchen-sink boots; seed templates render in the existing Inspection list block.

### Phase 6 — Tests

**Files:**

- **NEW** `packages/blocks-inspections/tests/InspectionTriggerTests.cs` — enum coverage + JSON round-trip
- **NEW** `packages/blocks-inspections/tests/EquipmentConditionAssessmentTests.cs` — record equality + JSON round-trip
- **NEW** `packages/blocks-inspections/tests/EquipmentConditionAssessmentServiceTests.cs` — record + list-by-inspection + list-history-by-equipment
- **NEW** `packages/blocks-inspections/tests/MoveInOutDeltaTests.cs` — 4 scenarios per Phase 4 PASS gate

**PASS gate:** `dotnet test packages/blocks-inspections/tests/` green; coverage on new code ≥ existing-block average.

### Phase 7 — Documentation

**Files:**

- **EDIT** `apps/docs/blocks/inspections.md` (verify exists; if not, create)
  - Add section: "Trigger types" — table of InspectionTrigger values + when each applies
  - Add section: "Equipment condition assessments" — distinct from Deficiency; for proactive condition rating
  - Add section: "Move-in / move-out delta" — how the delta is computed; how to consume in security-deposit reconciliation

**PASS gate:** apps/docs builds without warnings; new sections render.

### Phase 8 — Workstream ledger flip

**Files:**

- **EDIT** `icm/_state/active-workstreams.md` row #25 (Inspections):
  - Status: `ready-to-build` (extension) → `built` (extension shipped)
  - Reference: append PR link
  - Notes: append "Extension shipped: InspectionTrigger enum + Inspection.Trigger nullable field + EquipmentConditionAssessment child entity + 4 new service methods + move-in/out delta projection + 2 seed templates. iOS walkthrough wizard deferred to follow-up hand-off (gated on iOS App intake #23)."

**PASS gate:** Ledger updated; PR ready to merge.

---

## Out of scope (deferred to follow-up hand-offs)

- **iOS walkthrough wizard** — gated on iOS Field-Capture App intake (#23). EquipmentConditionAssessment + InspectionTrigger are usable from any UI; the iOS-specific wizard (PencilKit signature on move-in/out + camera-roll integration for photos) is a separate accelerator concern.
- **Photo blob storage** — `EquipmentConditionAssessment.PhotoBlobRefs: IReadOnlyList<string>` is a placeholder; matches Deficiency's deferred-photo pattern. Real blob ingest is gated on Bridge blob-ingest API spec (cluster cross-cutting OQ3).
- **Move-in/out signature sign-off** — gated on signatures ADR (0054) acceptance + `kernel-signatures` package shipping. Inspection at `Phase = Completed` doesn't yet bind to a SignatureEvent; that's a future Phase-2.1b enhancement.
- **Security-deposit reconciliation calculation** — `MoveInOutDelta` provides the data; the actual deposit-vs-damage computation lives in `blocks-rent-collection` or `blocks-accounting` (Phase 2 commercial intake's deliverable).
- **Work-order rollup from deficiency / failed condition** — existing `blocks-inspections` description notes this is "deferred to G16 second pass"; cluster's work-order extension to `blocks-maintenance` (workstream #19) will add the rollup integration when its hand-off is written.

---

## What sunfish-PM should NOT touch

- Existing Inspection / InspectionTemplate / InspectionChecklistItem / InspectionResponse / Deficiency semantics — purely additive extension
- `packages/blocks-property-equipment/` (consumer; FK target only — wait for #24 rename to merge)
- iOS app (doesn't exist; cluster intake #23)
- ADR documents (this hand-off does not require ADR amendment beyond the ADR 0053 already-shipped amendment)

---

## Open questions sunfish-PM should flag back to research

1. **Should `Inspection.Trigger` be required (with default `OnDemand` for backward-compat) or stay nullable?** Hand-off recommends nullable for cleanest backward compat. Flag if disagree.
2. **Should `RecordEquipmentConditionAsync` enforce the Inspection is `InProgress` (matching Deficiency-record semantics) or allow any phase?** Hand-off does not specify; recommend enforcing `InProgress` for consistency with `RecordResponseAsync`. Flag if you find a stronger reason for either.
3. **`MoveInOutDelta` "most recent" semantics** — what if a unit has multiple move-in inspections (e.g., over multiple tenancies)? Hand-off says "most recent" implying chronological; should it pair *most-recent-pair* or *most-recent-with-matching-tenancy*? Recommend most-recent for first-slice; tenancy-pairing is a Phase 2.2 enhancement when leasing-pipeline state machine ships.
4. **Photo placeholder format** — Deficiency uses what format? Match it. (If Deficiency has no photo placeholder, use `string PhotoBlobRef` as a single-photo placeholder for now.)

---

## Acceptance criteria

- [ ] All 8 phases complete with PASS gates green
- [ ] `dotnet build` + `dotnet test` repo-wide green
- [ ] Provider-neutrality analyzer (`SUNFISH_PROVNEUT_001`) passes on `blocks-inspections` (no vendor SDK references; build error if violated)
- [ ] Existing Inspection lifecycle tests still pass (no regressions)
- [ ] kitchen-sink demo: at least 1 move-in + 1 move-out inspection populated; delta query returns paired record
- [ ] `apps/docs/blocks/inspections.md` extended with new sections
- [ ] Workstream #25 ledger row flipped to `built` (extension)
- [ ] PR description names this as the EXTEND-disposition first-slice; flags iOS walkthrough wizard + signature sign-off + photo blob storage as deferred follow-ups
- [ ] No code outside `packages/blocks-inspections/`, `apps/kitchen-sink/<seed>`, `apps/docs/blocks/inspections.md`, `icm/_state/active-workstreams.md` is touched

---

## After this hand-off ships

Research session writes the next EXTEND hand-off. Likely candidates in priority order:
- Property-Leases EXTEND (#27) — `blocks-leases` versioning + signature binding (gated on ADR 0054 acceptance; can be drafted ahead)
- Property-Vendors EXTEND (#18) — `blocks-maintenance.Vendor` W-9 + magic-link onboarding (gated on Vendor Onboarding ADR draft)
- Property-WorkOrders EXTEND (#19) — `blocks-maintenance.WorkOrder` multi-party threads + entry-notice + completion-attestation (gated on ADR 0053 acceptance + ADR 0054 acceptance + ADR 0052 acceptance)

---

## Sign-off

Research session — 2026-04-29
