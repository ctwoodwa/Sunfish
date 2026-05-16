# Hand-off — W#23.5 iOS Mileage capture flow

**From:** XO (research session)
**To:** sunfish-PM (COB)
**Created:** 2026-05-15
**Parent workstream:** W#23 (iOS Field-Capture App substrate v1)
**Pipeline variant:** `sunfish-feature-change`
**Estimate:** ~4–6h / 2 PRs

> **GATE — TWO prerequisites, both must be on `main` before starting:**
> 1. **W#23 Phase 6** (home screen) — ships the `SunfishFieldApp` nav root that all capture
>    flows hang off of.
> 2. **W#61** (`VehicleMetadata` + `TripRecord` + `ITripStore` + `MileageRecorded`) — the
>    .NET substrate this flow writes into. Without it there is no `ITripStore` to inject and
>    no `MileageRecorded` event type to emit.
>
> **Ordering note:** W#23.2 (Equipment Photo), W#23.3 (Inspections), W#23.4 (Signatures),
> and W#23.6 (Work Orders) are all independent of this workstream and may ship in parallel
> after W#23 P6 lands.

---

## Context

W#23.5 implements the Mileage capture flow on top of the W#23 substrate. It is a
form-only flow (no camera, no PencilKit, no cryptographic signing) — the simplest
odometer-recording interaction in the field app:

1. Open "Mileage" from the home screen.
2. Choose a vehicle (Equipment with `Class = Vehicle`) from the tenant's fleet.
3. Enter start odometer, end odometer, trip date, property visited, and optional purpose.
4. Tap "Record Trip" → emits a `Mileage` event via `EventQueueService`.
5. Bridge syncs the event → routes to `ITripStore.AppendAsync(TripRecord {...})` →
   `VehicleMetadata.CurrentOdometer` is updated to `EndOdometer`.

**Domain foundation (all on `main` after gates clear):**
- `Sunfish.Blocks.PropertyEquipment` — W#24 ✓ + W#61 (gate)
  - `IEquipmentRepository.ListByClassAsync(tenant, EquipmentClass.Vehicle, ct)` ✓
  - `ITripStore.AppendAsync(TripRecord, ct)` — added by W#61
  - `EquipmentLifecycleEventType.MileageRecorded` — added by W#61
  - `TripRecordId.NewId()` — added by W#61
  - `VehicleMetadata` — added by W#61
- `accelerators/anchor-mobile-ios/SunfishField/Events/EventType.swift`
  - `.Mileage` case — **already exists** (confirmed; no addition needed)
- Transport + event substrate — W#23 P3 + P4 ✓

**No new iOS entitlements, frameworks, or packages required.**

---

## Event payload

```swift
/// Posted as `envelope.payload` in the canonical-JSON field event envelope.
struct MileageCapturedPayload: Codable {
    /// `EquipmentId.Value` of the vehicle being tracked (string-backed).
    let equipmentId: String

    /// `PropertyId.Value` of the property visited (string-backed).
    let propertyId: String

    /// ISO-8601 UTC string of the trip date. Midnight UTC is fine for day-only entry.
    let tripDate: String

    /// Odometer reading at trip start in miles.
    let startOdometer: Double

    /// Odometer reading at trip end in miles. Must be ≥ startOdometer (enforced by UI).
    let endOdometer: Double

    /// Optional free-text purpose (e.g. "maintenance", "inspection", "delivery").
    let purpose: String?

    /// Optional free-text notes.
    let notes: String?
}
```

The Swift `EventType` raw value for this payload is `"Mileage"` — matches the existing
`.Mileage` case in `EventType.swift`.

---

## GRDB schema (add to existing migration sequence)

```sql
CREATE TABLE mileage_records (
    id            TEXT    PRIMARY KEY,
    equipment_id  TEXT    NOT NULL,
    property_id   TEXT    NOT NULL,
    trip_date     TEXT    NOT NULL,
    start_odometer REAL   NOT NULL,
    end_odometer  REAL    NOT NULL,
    purpose       TEXT,
    notes         TEXT,
    status        TEXT    NOT NULL DEFAULT 'pending',
    cached_at     TEXT    NOT NULL
) WITHOUT ROWID;

CREATE INDEX idx_mileage_records_equipment
    ON mileage_records (equipment_id);
```

`status` values: `'pending'` (not yet synced) → `'synced'` (Bridge accepted) → `'failed'`
(retry exhausted). Read locally for offline recent-trips display.

---

## Phase 1 — Screens + vehicle list + Bridge vehicle endpoint (~2–3h)

### New files in `accelerators/anchor-mobile-ios/SunfishField/Mileage/`

**`VehiclePickerView.swift`**
- SwiftUI `List` of Vehicle-class equipment for the authenticated tenant.
- Fetches via Bridge: `GET /api/v1/field/vehicles` (added below).
- Each row: vehicle name, make/model/year, license plate (if set), current odometer.
- Offline fallback: reads from local GRDB `vehicles_cache` table (see below).
- Selecting a row → `TripEntryView(vehicle: vehicle)`.

**`TripEntryView.swift`**
- Form sheet for recording a trip against the selected vehicle:
  - **Equipment** (pre-filled from picker, read-only label showing make/model/year/plate).
  - **Property** — picker populated from cached property list (same GRDB cache as other
    capture flows; no new Bridge call).
  - **Trip Date** — `DatePicker` defaulting to today; time component hidden (day-only).
  - **Start Odometer** — `TextField` (numeric, miles). Pre-filled from
    `vehicle.currentOdometer` if known; editable.
  - **End Odometer** — `TextField` (numeric, miles). Validated ≥ start odometer inline.
  - **Miles** — computed read-only label `(endOdometer - startOdometer)` with green/red
    color depending on validity.
  - **Purpose** — optional `TextField` (free text, 120 char max).
  - **Notes** — optional `TextEditor` (free text, 500 char max).
  - "Record Trip" button: disabled until end ≥ start and equipmentId + propertyId non-empty.
  - On submit → `MileageCaptureService.record(trip:)`.
  - On success → sheet dismiss + toast "Trip recorded (pending sync)".

**`MileageListView.swift`**
- Entry point from home screen (tapping the Mileage tile).
- Displays two sections: "Pending Sync" (from GRDB `mileage_records WHERE status='pending'`)
  and "Recent Trips" (GRDB `mileage_records WHERE status='synced'` last 30 days).
- Each row: vehicle name, property, miles, date, purpose (if set), sync-status chip.
- "+" toolbar button → `VehiclePickerView`.

**GRDB cache table for vehicles** (add to existing migration):

```sql
CREATE TABLE vehicles_cache (
    id            TEXT PRIMARY KEY,
    name          TEXT NOT NULL,
    make          TEXT,
    model         TEXT,
    year          INTEGER,
    license_plate TEXT,
    current_odometer REAL NOT NULL DEFAULT 0,
    cached_at     TEXT NOT NULL
) WITHOUT ROWID;
```

### Bridge additions (Phase 1)

**New file: `accelerators/bridge/Sunfish.Bridge/Field/VehicleFieldEndpoints.cs`**

```csharp
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Bridge.Field;

public static class VehicleFieldEndpoints
{
    public static IEndpointRouteBuilder MapVehicleFieldEndpoints(
        this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/v1/field");
        group.MapGet("/vehicles", HandleVehicleListGetAsync);
        return app;
    }

    internal static async Task<IResult> HandleVehicleListGetAsync(
        IEquipmentRepository equipmentRepository,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(equipmentRepository);

        // Substrate v1: TenantId from DI sentinel (auth layer lands as follow-up).
        var tenantId = new TenantId("bridge-anonymous");

        var vehicles = await equipmentRepository
            .ListByClassAsync(tenantId, EquipmentClass.Vehicle, includeDisposed: false, ct)
            .ConfigureAwait(false);

        return Results.Ok(vehicles.Select(v => new VehicleFieldDto(
            Id: v.Id.Value,
            Name: v.Name,
            Make: v.VehicleData?.Make,
            Model: v.VehicleData?.Model,
            Year: v.VehicleData?.Year,
            LicensePlate: v.VehicleData?.LicensePlate,
            CurrentOdometer: v.VehicleData?.CurrentOdometer ?? 0m)));
    }

    private sealed record VehicleFieldDto(
        string Id,
        string Name,
        string? Make,
        string? Model,
        int? Year,
        string? LicensePlate,
        decimal CurrentOdometer);
}
```

Wire into `Program.cs` alongside the existing `MapFieldEndpoints()` call:

```csharp
app.MapVehicleFieldEndpoints();
```

### Halt condition (Phase 1)

If `IEquipmentRepository.ListByClassAsync` does not exist or its signature differs from
what is specified here → read the actual interface at
`packages/blocks-property-equipment/Services/IEquipmentRepository.cs` and adapt the
Bridge handler; do NOT invent a replacement query path.

**PR title:** `feat(anchor-mobile-ios,bridge): W#23.5 Phase 1 — vehicle list + trip entry + mileage list + Bridge /vehicles endpoint`

---

## Phase 2 — MileageCaptureService + Bridge routing + docs (~2–3h)

### New file: `SunfishField/Mileage/MileageCaptureService.swift`

```swift
import Foundation
import GRDB

/// Records a vehicle trip as a `Mileage` field event.
/// Persists locally via GRDB, then enqueues for Bridge upload.
final class MileageCaptureService {
    private let db: DatabaseQueue
    private let eventQueue: EventQueueService

    init(db: DatabaseQueue, eventQueue: EventQueueService) {
        self.db = db
        self.eventQueue = eventQueue
    }

    func record(
        equipmentId: String,
        propertyId: String,
        tripDate: Date,
        startOdometer: Double,
        endOdometer: Double,
        purpose: String?,
        notes: String?
    ) throws {
        precondition(endOdometer >= startOdometer,
            "endOdometer must be ≥ startOdometer")

        let id = UUID().uuidString
        let now = ISO8601DateFormatter().string(from: Date())
        let tripDateStr = ISO8601DateFormatter().string(from: tripDate)

        // 1. Persist locally.
        try db.write { db in
            try db.execute(sql: """
                INSERT INTO mileage_records
                  (id, equipment_id, property_id, trip_date,
                   start_odometer, end_odometer, purpose, notes,
                   status, cached_at)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'pending', ?)
                """,
                arguments: [id, equipmentId, propertyId, tripDateStr,
                            startOdometer, endOdometer, purpose, notes, now])
        }

        // 2. Enqueue for Bridge upload.
        let payload = MileageCapturedPayload(
            equipmentId: equipmentId,
            propertyId: propertyId,
            tripDate: tripDateStr,
            startOdometer: startOdometer,
            endOdometer: endOdometer,
            purpose: purpose,
            notes: notes)

        try eventQueue.enqueue(
            eventType: .Mileage,
            payload: payload,
            localRecordId: id)
    }
}
```

### Bridge dispatcher addition (Phase 2)

In `FieldEndpoints.HandleFieldEventPostAsync`, add a domain-dispatch block after the
idempotency cache insertion and before the final `Results.Ok(...)` return. If W#23.4
(Signatures) has already shipped, a `switch (envelope.EventType)` block will already
exist — add the `"Mileage"` case to it. If W#23.4 has NOT shipped yet, create the
switch block following the same structure.

```csharp
// Domain dispatch: route the accepted event to its domain service.
// Extend this switch as capture-flow hand-offs ship.
// Inject ITripStore via constructor parameter (see DI section below).
switch (envelope.EventType)
{
    case "Mileage":
        var mileagePayload = JsonSerializer.Deserialize<MileageCapturedPayload>(
            envelope.Payload.GetRawText(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException("Mileage payload null after schema validation.");

        await _tripStore.AppendAsync(new TripRecord
        {
            Id         = TripRecordId.NewId(),
            TenantId   = envelope.TenantId,
            EquipmentId = new EquipmentId(mileagePayload.EquipmentId),
            PropertyId  = new PropertyId(mileagePayload.PropertyId),
            TripDate    = DateTimeOffset.Parse(mileagePayload.TripDate,
                              System.Globalization.CultureInfo.InvariantCulture,
                              System.Globalization.DateTimeStyles.AssumeUniversal),
            StartOdometer = (decimal)mileagePayload.StartOdometer,
            EndOdometer   = (decimal)mileagePayload.EndOdometer,
            Purpose = mileagePayload.Purpose,
            Notes   = mileagePayload.Notes,
        }, ct).ConfigureAwait(false);
        break;

    default:
        // Unknown event type accepted at envelope level; domain routing
        // deferred. Log for diagnostics.
        break;
}
```

**Payload record** (add to `FieldEndpoints.cs` or a new `MileageCapturedPayload.cs`
in `Sunfish.Bridge/Field/`):

```csharp
private sealed record MileageCapturedPayload(
    string EquipmentId,
    string PropertyId,
    string TripDate,
    double StartOdometer,
    double EndOdometer,
    string? Purpose,
    string? Notes);
```

**DI wiring** — inject `ITripStore` into the route handler via the minimal-API
parameter binding or via `HttpContext.RequestServices`. Follow the same pattern used
for `ISignatureCapture` in `W#23.4` if it has shipped. If W#23.4 has not yet shipped,
follow the same parameter-injection pattern used for `IAuditTrail` and `IOperationSigner`
in the existing `HandleFieldEventPostAsync` signature.

### Tests (Phase 2)

In `tests/Sunfish.Bridge.Tests.Unit/Field/` (following existing test structure):

1. `POST /api/v1/field/event` with `EventType = "Mileage"` → HTTP 200; `ITripStore.AppendAsync`
   called once with correct `EquipmentId`, `PropertyId`, `StartOdometer`, `EndOdometer`.
2. `endOdometer < startOdometer` in payload → still HTTP 200 (Bridge accepts; `ITripStore`
   enforces negative-miles guard via `TripRecord.Miles = Math.Max(0, End - Start)`).
3. Idempotency re-POST of same `eventId` → HTTP 200, `ITripStore.AppendAsync` NOT called
   a second time.

### Docs (Phase 2)

`apps/docs/blocks/property-equipment/ios-mileage-capture.md`
- Mileage capture flow overview (iOS → Bridge → `ITripStore`)
- `MileageCapturedPayload` field reference
- `TripRecord.Miles` non-negative guarantee
- `VehicleMetadata.CurrentOdometer` update behavior (updated on `AppendAsync`)
- Sequence diagram: field entry → GRDB local persist → sync → Bridge → domain store

### Ledger flip (Phase 2)

- Flip W#23.5 ledger row from `ready-to-build` → `built`.
- Update W#23 source file: add "W#23.5 Mileage shipped" note to Phase 6 follow-on section.

**PR title:** `feat(anchor-mobile-ios,bridge): W#23.5 Phase 2 — MileageCaptureService + Bridge mileage routing + docs`

---

## Acceptance criteria

- [ ] `MileageListView` reachable from home screen Mileage tile
- [ ] `VehiclePickerView` fetches Vehicle-class equipment from Bridge `/api/v1/field/vehicles`
- [ ] `TripEntryView` disables "Record Trip" when end odometer < start odometer
- [ ] Successful entry → row appears in "Pending Sync" section of `MileageListView`
- [ ] `MileageCaptureService.record(...)` writes a row to `mileage_records` GRDB table
- [ ] Event enqueued with `EventType = .Mileage` and correct `MileageCapturedPayload` fields
- [ ] Bridge `POST /api/v1/field/event` with `EventType = "Mileage"` → `ITripStore.AppendAsync`
  called with correct `TripRecord` fields
- [ ] `TripRecord.Miles` is always ≥ 0 (even if payload is reversed)
- [ ] Idempotent re-POST does NOT call `AppendAsync` a second time
- [ ] 3 unit tests passing (Bridge side)
- [ ] Docs page live at `apps/docs/blocks/property-equipment/ios-mileage-capture.md`
- [ ] W#23.5 ledger row flipped to `built`

---

## Halt conditions

1. `IEquipmentRepository.ListByClassAsync` signature differs from assumed → read
   `packages/blocks-property-equipment/Services/IEquipmentRepository.cs` and adapt.
2. `ITripStore` or `TripRecord` not present on `main` → W#61 has NOT shipped yet;
   stop and wait for W#61 gate to clear.
3. `FieldEndpoints.HandleFieldEventPostAsync` has been substantially refactored since
   this hand-off was authored → read the current file before adding the dispatch block.
4. `EventType.Mileage` case missing from `SunfishField/Events/EventType.swift` →
   add it (raw value `"Mileage"`) following the existing enum pattern.
