# Hand-off — W#23.3 iOS Inspections walkthrough capture flow

**From:** XO (research session)
**To:** sunfish-PM (COB)
**Created:** 2026-05-15
**Parent workstream:** W#23 (iOS Field-Capture App substrate v1)
**Pipeline variant:** `sunfish-feature-change`
**Estimate:** ~10–14h / 4 PRs

> **GATE:** Do not start until W#23 Phase 6 (home screen) is shipped and merged to main.
> W#23.2 (Equipment Photo) ships before this one in the queue — Phase 3 of THIS
> hand-off references the equipment list populated by W#23.2.

---

## Context

W#23.3 implements the Inspections walkthrough capture flow on top of the W#23 substrate. It
surfaces the `IInspectionsService` contract through a multi-step SwiftUI flow: start an
inspection → walk through checklist items → record deficiencies with photos → record equipment
condition assessments → complete and sync.

**Domain foundation (all on main):**
- `Sunfish.Blocks.Inspections.Services.IInspectionsService` — W#25, built ✓
  - `ScheduleAsync`, `StartAsync`, `RecordResponseAsync`, `CompleteAsync`
  - `RecordDeficiencyAsync`, `RecordEquipmentConditionAsync`
  - `GetInspectionAsync`, `GenerateReportAsync`
- `InspectionPhase`: `Scheduled → InProgress → Completed`
- `DeficiencySeverity` enum: severity levels for deficiency recording
- `ConditionRating` enum: condition scale for equipment assessments
- `EquipmentConditionAssessment`: links `EquipmentId` + rating + notes + photos

**iOS frameworks used:**
- `PHPickerViewController` — photo selection (same as W#23.2; no new entitlements)
- `UIImagePickerController` — in-flow camera capture (add `NSCameraUsageDescription` to Info.plist
  if not already present from W#23 Phase 1 scaffold)
- No Vision / OCR / PencilKit needed

---

## Event types used

Add to the Swift `EventType` enum if not already present (W#23 Phase 3 should have stubs):

| Swift EventType | Payload fields |
|---|---|
| `.inspectionStarted` | `inspectionId`, `propertyId`, `templateId?` |
| `.checklistResponseRecorded` | `inspectionId`, `itemId`, `response` (`pass`/`fail`/`na`), `note?` |
| `.deficiencyRecorded` | `inspectionId`, `itemId`, `description`, `severity` (enum string), `photoRef?` |
| `.equipmentConditionRecorded` | `inspectionId`, `equipmentId`, `rating` (enum string), `note?`, `photoRef?` |
| `.inspectionCompleted` | `inspectionId`, `completedAt` (ISO 8601) |

All events flow through `EventQueueService.enqueue(_:)` — no direct API writes from iOS.

---

## Phase 1 — Inspection list + start + checklist walkthrough (~3-4h)

### New directory
`accelerators/anchor-mobile-ios/SunfishField/Inspections/`

### Files

`InspectionListView.swift`
- Fetches via Bridge: `GET /api/v1/field/inspections?phase=scheduled,in-progress`
- Each row: property address, inspection phase chip, scheduled date, template name if available
- Pull-to-refresh + offline stale indicator (read from GRDB cache if offline)
- Tapping a scheduled inspection → `InspectionDetailView` with "Start Inspection" button
- Tapping an in-progress inspection → continues walkthrough at last recorded step

`InspectionDetailView.swift`
- Header: property, template name, phase, started time (if in progress)
- **"Start Inspection" button** (if Scheduled) — emits `.inspectionStarted` event + updates local
  phase to InProgress
- Checklist section: `List` of `InspectionChecklistItem` rows
  - Each row: item description, response picker (`Pass` / `Fail` / `N/A`), tap to expand notes
  - Selecting a response emits `.checklistResponseRecorded`
  - "Add Deficiency" link appears on `Fail` items → pushes `RecordDeficiencySheet`
- Progress bar: `respondedItems / totalItems`
- "Complete Inspection" button (active when all required items have responses) → confirms then
  emits `.inspectionCompleted`

`RecordDeficiencySheet.swift`
- Modal: description `TextField` (required, 500 chars), severity picker (`Minor` / `Moderate` /
  `Severe` / `Critical`), optional photo button (PHPicker, 1 photo max)
- On submit: emits `.deficiencyRecorded`; photo stored via existing `BlobStore` → `photoRef` set
  to content-SHA256

### Bridge route additions (`WorkerFieldEndpoints.cs` or new `InspectionFieldEndpoints.cs`)

`GET /api/v1/field/inspections`
- Query param: `?phase=scheduled,in-progress&propertyId=<id>` (optional)
- Returns `InspectionFieldDto[]`:

```csharp
public record InspectionFieldDto(
    string InspectionId,
    string PropertyId,
    string Phase, // "Scheduled" | "InProgress"
    DateTimeOffset? ScheduledFor,
    string? TemplateName,
    int TotalItems,
    int RespondedItems
);
```

Field-event dispatcher additions (Bridge side):

```csharp
case "InspectionStarted":
    await _inspectionsService.StartAsync(new InspectionId(payload.inspectionId), ct);
    break;

case "ChecklistResponseRecorded":
    await _inspectionsService.RecordResponseAsync(
        new InspectionId(payload.inspectionId),
        new InspectionResponse(new ChecklistItemId(payload.itemId),
            Enum.Parse<ChecklistResponse>(payload.response), payload.note),
        ct);
    break;

case "DeficiencyRecorded":
    await _inspectionsService.RecordDeficiencyAsync(new RecordDeficiencyRequest(
        new InspectionId(payload.inspectionId),
        new ChecklistItemId(payload.itemId),
        payload.description,
        Enum.Parse<DeficiencySeverity>(payload.severity),
        payload.photoRef), ct);
    break;

case "InspectionCompleted":
    await _inspectionsService.CompleteAsync(new InspectionId(payload.inspectionId), ct);
    break;
```

**Halt condition:** If `RecordDeficiencyAsync` signature doesn't match `(RecordDeficiencyRequest)`
— adapt to actual signature; do NOT modify the domain service.

### Tests (Phase 1)

- `GET /api/v1/field/inspections` returns only scheduled + in-progress for tenant
- `InspectionStarted` event transitions phase to InProgress
- `DeficiencyRecorded` with `Severe` severity creates deficiency with correct fields
- `InspectionCompleted` transitions phase to Completed

**PR title:** `feat(anchor-mobile-ios,bridge): W#23.3 Phase 1 — inspection walkthrough + checklist + deficiency + Bridge routing`

---

## Phase 2 — Equipment condition assessment (~3-4h)

> **GATE on W#23.2 shipped.** Equipment condition assessment uses the equipment list
> cached by W#23.2. If W#23.2 has not shipped, defer Phase 2 and proceed to Phase 3.

`EquipmentConditionView.swift`
- Accessible from `InspectionDetailView` via "Assess Equipment" section below checklist
- Shows cached equipment list for the inspected property (reads from W#23.2's `equipment` GRDB
  table; falls back to empty with "Equipment list unavailable offline" message)
- Each equipment row: name, kind, last condition rating (from cache), "Assess" button
- "Assess" button → `EquipmentConditionSheet`

`EquipmentConditionSheet.swift`
- `SegmentedPicker` for `ConditionRating` values (e.g., `Good` / `Fair` / `Poor` / `Failed`)
- Optional note `TextField`
- Optional photo (1 photo, PHPicker)
- On submit: emits `.equipmentConditionRecorded`; updates local cache row `last_condition_rating`

Field-event dispatcher Bridge addition:

```csharp
case "EquipmentConditionRecorded":
    await _inspectionsService.RecordEquipmentConditionAsync(
        new InspectionId(payload.inspectionId),
        new EquipmentId(payload.equipmentId),
        Enum.Parse<ConditionRating>(payload.rating),
        payload.note, payload.photoRef, ct);
    break;
```

### Tests (Phase 2)

- `EquipmentConditionRecorded` creates an `EquipmentConditionAssessment` linked to the inspection
- Condition assessment with `Failed` rating and photo ref stores correctly

**PR title:** `feat(anchor-mobile-ios,bridge): W#23.3 Phase 2 — equipment condition assessment`

---

## Phase 3 — GRDB offline cache + ad hoc inspection creation (~2-3h)

### Inspection cache schema

```sql
CREATE TABLE inspections (
    id TEXT PRIMARY KEY,
    property_id TEXT NOT NULL,
    phase TEXT NOT NULL,
    scheduled_for TEXT,
    template_name TEXT,
    total_items INTEGER NOT NULL DEFAULT 0,
    responded_items INTEGER NOT NULL DEFAULT 0,
    cached_at TEXT NOT NULL
) WITHOUT ROWID;
```

Populated on successful `GET /api/v1/field/inspections`. Stale indicator if `cached_at > 24h`.

### Ad hoc inspection creation

`NewInspectionSheet.swift` (accessible via "+" in `InspectionListView`)
- Property picker + unit picker (optional) + brief description field
- Uses `ScheduleAsync` indirectly: emits a new event type `.inspectionScheduled` with
  `{ propertyId, unitId?, description, scheduledFor: now }` → Bridge creates via
  `IInspectionsService.ScheduleAsync(...)` on sync

Bridge dispatcher addition:

```csharp
case "InspectionScheduled":
    await _inspectionsService.ScheduleAsync(new ScheduleInspectionRequest(
        new PropertyId(payload.propertyId),
        payload.unitId is null ? null : new UnitId(payload.unitId),
        payload.description,
        payload.scheduledFor,
        null), // no template for ad hoc
        ct);
    break;
```

**PR title:** `feat(anchor-mobile-ios): W#23.3 Phase 3 — GRDB offline cache + ad hoc inspection creation`

---

## Phase 4 — Docs + ledger note (~1h)

- `apps/docs/blocks/inspections/ios-field-walkthrough.md` — capture flow description, event
  types, offline behavior, checklist response semantics, deficiency severity guide
- Add W#23.3 note to W#23 workstream source file's Notes section (do not flip W#23 to built)
- Update W#23 workstream `status_cell` to reflect W#23.3 pre-authored

**PR title:** `docs: W#23.3 iOS inspections walkthrough capture flow guide + ledger note`

---

## Acceptance criteria

- [ ] Inspection list screen shows scheduled + in-progress inspections for tenant
- [ ] Starting an inspection emits `InspectionStarted` event; phase updates to InProgress in local cache
- [ ] Checklist walkthrough: all items can receive Pass/Fail/N/A response
- [ ] `Fail` response shows "Add Deficiency" link; deficiency records with severity + optional photo
- [ ] Equipment condition assessment: `ConditionRating` picker + optional note + photo
- [ ] Completing inspection emits `InspectionCompleted` event
- [ ] Ad hoc inspection creation via "+" button queues `InspectionScheduled` event
- [ ] Offline: list screen shows cached inspections with stale indicator
- [ ] Bridge routing: `InspectionStarted` / `ChecklistResponseRecorded` / `DeficiencyRecorded` /
  `EquipmentConditionRecorded` / `InspectionCompleted` / `InspectionScheduled` all route correctly
- [ ] 5 Bridge tests passing (list endpoint + 4 event type routing tests)
- [ ] docs page live

---

## Halt conditions

1. `EventType` enum in Swift missing inspection event types → add them (mechanical, no research ruling).
2. `ChecklistItemId` or `ChecklistResponse` types don't exist in `Sunfish.Blocks.Inspections` →
   STOP; inspect `IInspectionsService.RecordResponseAsync` signature and adapt payload accordingly.
3. W#23.2 equipment cache table (`equipment`) not in GRDB schema → Phase 2 equipment condition
   section is unavailable; show "Equipment list requires W#23.2" placeholder; skip Phase 2 and
   proceed to Phase 3.
4. `ConditionRating` enum values differ from assumed → read `Models/ConditionRating.cs` and use
   actual values.
5. W#23 Phase 6 not yet shipped → do not start; check `gh pr list` first.
