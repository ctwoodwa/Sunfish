# Hand-off — W#23.6 iOS Work-Order Response capture flow

**From:** XO (research session)
**To:** sunfish-PM (COB)
**Created:** 2026-05-15
**Parent workstream:** W#23 (iOS Field-Capture App substrate v1)
**Pipeline variant:** `sunfish-feature-change`

> **GATE:** Do not start until W#23 Phase 6 (home screen) is shipped and merged to main.
> W#23.2 (Equipment Photo) ships before this one in the queue.

---

## Context

W#23.6 implements the Work-Order Response capture flow on top of the W#23 substrate. It is
form-only (no camera, no PencilKit, no OCR) — the simplest capture-flow follow-on. Total
estimate: **4–6h / 2 PRs**.

**Domain foundation:**
- Work orders: `Sunfish.Blocks.Maintenance` (`IWorkOrderService`, `WorkOrder`, `WorkOrderStatus`,
  `WorkOrderEntryNotice`, `WorkOrderCompletionAttestation`) — W#19, fully built ✓
- Transport: `accelerators/anchor-mobile-ios/SunfishField/Sync/` (substrate, W#23 P4 ✓)
- Event envelope: `accelerators/anchor-mobile-ios/SunfishField/Events/EventEnvelope.swift` (W#23 P3 ✓)
- Bridge field endpoint: `POST /api/v1/field/event` (W#23 P4.5 ✓)

**What this adds:**
- SwiftUI screens to view open work orders and update status from the field
- Ability to open a new work order from an in-field finding (inspection or ad hoc)
- Work-order events queued via `EventQueueService` → synced to Bridge → routed to `IWorkOrderService`

---

## Event types used

These event types MUST already be in the Swift `EventType` enum from W#23 Phase 3. Confirm
before starting Phase 1 and add if missing:

| Swift EventType | .NET AuditEventType | Payload fields |
|---|---|---|
| `.workOrderStatusUpdated` | `WorkOrderStatusUpdated` | `workOrderId`, `newStatus`, `note?` |
| `.workOrderNoteAdded` | `WorkOrderNoteAdded` | `workOrderId`, `note`, `attachmentRef?` |
| `.workOrderCreated` | n/a (Bridge creates via `IWorkOrderService`) | `title`, `description`, `propertyId`, `unitId?` |

---

## Phase 1 — Work-Order list screen (~2-3h)

### New files

`accelerators/anchor-mobile-ios/SunfishField/WorkOrders/`
- `WorkOrderListView.swift` — SwiftUI `List` of work orders for the current tenant
  - Fetches via Bridge: `GET /api/v1/field/work-orders?status=open,in-progress`
  - Each row: title, property, vendor name (if assigned), status chip, due date (if set)
  - Pull-to-refresh + offline-cached stale data indicator (read from GRDB local cache on offline)
  - Tapping a row → `WorkOrderDetailView`
  - "+" toolbar button → `NewWorkOrderSheet`

`accelerators/anchor-mobile-ios/SunfishField/WorkOrders/WorkOrderDetailView.swift`
- Displays: title, description, status, entry notice (if present), assigned vendor, appointment
- **Status update buttons** (context-sensitive):
  - If `open` → "Start Work" button (→ emits `.workOrderStatusUpdated` with `newStatus = "in-progress"`)
  - If `in-progress` → "Mark Complete" button (→ emits `.workOrderStatusUpdated` with `newStatus = "done"` + required note sheet)
  - "Add Note" button (always visible → emits `.workOrderNoteAdded`)
- Completion note sheet: `TextField` (multiline, 500 char max) + optional photo attachment (single photo; uses `PHPickerViewController`; stores blob via existing `BlobStore`; sets `attachmentRef` to content-SHA256)
- All mutations go through `EventQueueService.enqueue(_:)` — no direct API writes

`accelerators/anchor-mobile-ios/SunfishField/WorkOrders/NewWorkOrderSheet.swift`
- "Create Work Order" modal: title (required), description (optional), property picker (pre-populated
  from cached property list), unit picker (optional)
- Emits `.workOrderCreated` event on submit
- Bridge-side handler creates the WO via `IWorkOrderService.CreateAsync(...)` on sync

### Bridge route additions (new file)

`accelerators/bridge/Sunfish.Bridge/Field/WorkOrderFieldEndpoints.cs`
- `GET /api/v1/field/work-orders` — returns open + in-progress work orders for the authenticated
  tenant; query params: `?status=open,in-progress&propertyId=<id>` (optional filter)
- Returns `WorkOrderFieldDto[]` — subset of fields iOS needs: `{ id, title, description, status,
  propertyId, unitId, vendorName?, dueAt?, entryNoticeSummary? }`
- Existing `POST /api/v1/field/event` already receives the mutation events; add routing for the 3
  new event types in the Bridge field-event dispatcher

### Bridge field-event dispatcher additions

In the existing Bridge field-event dispatcher (wherever `POST /api/v1/field/event` routes events
to domain services), add handlers:

```csharp
case "WorkOrderStatusUpdated":
    var wo = await _workOrderService.GetAsync(payload.workOrderId, ct);
    await _workOrderService.TransitionAsync(wo.Id, Enum.Parse<WorkOrderStatus>(payload.newStatus), 
        payload.note, actorId, ct);
    break;

case "WorkOrderNoteAdded":
    await _workOrderService.AddNoteAsync(payload.workOrderId, payload.note,
        payload.attachmentRef, actorId, ct);
    break;

case "WorkOrderCreated":
    await _workOrderService.CreateAsync(new CreateWorkOrderCommand(
        payload.title, payload.description, payload.propertyId, payload.unitId), actorId, ct);
    break;
```

**Halt condition:** If `IWorkOrderService` does not have `AddNoteAsync` — STOP, drop a
`cob-question-*.md`. Do not add domain methods without a research ruling.

### Tests (Phase 1)

- `GET /api/v1/field/work-orders` returns only open + in-progress WOs for tenant
- `WorkOrderStatusUpdated` event transitions WO status correctly
- `WorkOrderCreated` event creates a new WO with correct title + property

**PR title:** `feat(anchor-mobile-ios,bridge): W#23.6 Phase 1 — work-order list + status update + Bridge routing`

---

## Phase 2 — GRDB offline cache + docs + ledger note (~1-2h)

### Local cache

Add `work_orders` table to GRDB schema (alongside existing tables from W#23 P2):

```sql
CREATE TABLE work_orders (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    description TEXT,
    status TEXT NOT NULL,
    property_id TEXT NOT NULL,
    unit_id TEXT,
    vendor_name TEXT,
    due_at TEXT,
    cached_at TEXT NOT NULL
) WITHOUT ROWID;
```

`WorkOrderListView` reads from cache when offline (status badge shows stale indicator if
`cached_at` > 24h). Cache populated on successful `GET /api/v1/field/work-orders` response.

### Docs

- `apps/docs/blocks/maintenance/ios-work-order-response.md` — one-page doc describing the
  capture flow, event types, offline behavior, and Bridge routing

### Hand-off note update

Add W#23.6 to the W#23 workstream source file's Notes section
(do not flip the W#23 row itself to built — that only happens after all intended phases complete).

**PR title:** `feat(anchor-mobile-ios): W#23.6 Phase 2 — GRDB offline cache + docs`

---

## Acceptance criteria

- [ ] Work-order list screen appears in home screen navigation (behind "Work Orders" nav item)
- [ ] Tapping a work order shows detail with status + status-update buttons
- [ ] Marking a WO "in-progress" queues a `workOrderStatusUpdated` event with correct payload
- [ ] Marking a WO "done" requires a completion note (empty note rejected in UI)
- [ ] Completion note with photo: blob ref populated in event payload
- [ ] Creating a new WO from the "+" button queues a `workOrderCreated` event
- [ ] Offline mode: list screen shows cached WOs with stale indicator
- [ ] Bridge routing: `WorkOrderStatusUpdated` event transitions WO status in domain
- [ ] Bridge routing: `WorkOrderCreated` event creates WO record
- [ ] 3 Bridge tests passing (list endpoint + status-update routing + create routing)
- [ ] docs page live at `apps/docs/blocks/maintenance/ios-work-order-response.md`

---

## Halt conditions

1. `EventType` enum in Swift does not contain `.workOrderStatusUpdated` / `.workOrderNoteAdded` /
   `.workOrderCreated` → add them (no research ruling needed — EventType additions are mechanical).
2. `IWorkOrderService` lacks `AddNoteAsync` → STOP; file `cob-question-*.md`.
3. Bridge field-event dispatcher is structured differently than assumed (if routing is not a
   switch/dispatch pattern) → adapt to existing pattern; if architectural change needed → STOP.
4. W#23 Phase 6 not yet shipped → do not start; check `gh pr list` for P6 PR first.
