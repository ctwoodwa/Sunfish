# Hand-off — W#61 Vehicle Equipment Subtype + Trip Records

**From:** XO (research session)
**To:** sunfish-PM (COB)
**Created:** 2026-05-15
**Parent workstream:** W#24 / W#61 (extension of `blocks-property-equipment`)
**Pipeline variant:** `sunfish-feature-change`
**Estimate:** ~3–5h / 1–2 PRs

> **No gate.** Immediately buildable. Unblocks W#23.5 (iOS Mileage capture flow).

---

## Context

`blocks-property-equipment` ships `EquipmentClass.Vehicle` as a reserved discriminator
with a comment that "full subtype (VIN, mileage Trip events) gated on follow-up hand-off."
W#61 delivers that follow-up: `VehicleMetadata` on `Equipment`, `TripRecord` append-only
log, `ITripStore`, and `MileageRecorded` lifecycle event type.

No new package — everything is additive to `Sunfish.Blocks.PropertyEquipment`.

---

## Phase 1 — VehicleMetadata + TripRecord + ITripStore (~2–3h)

### New files in `packages/blocks-property-equipment/Models/`

**`VehicleMetadata.cs`**
```csharp
namespace Sunfish.Blocks.PropertyEquipment.Models;

/// <summary>
/// Vehicle-specific metadata for <see cref="Equipment"/> records where
/// <see cref="Equipment.Class"/> is <see cref="EquipmentClass.Vehicle"/>.
/// Null on all other equipment classes.
/// </summary>
public sealed record VehicleMetadata
{
    /// <summary>Vehicle Identification Number (17-character VIN).</summary>
    public string? Vin { get; init; }

    /// <summary>Manufacturer name (e.g. <c>"Ford"</c>, <c>"Toyota"</c>).</summary>
    public string? Make { get; init; }

    /// <summary>Model name (e.g. <c>"F-150"</c>, <c>"Camry"</c>).</summary>
    public string? Model { get; init; }

    /// <summary>Model year (e.g. <c>2019</c>).</summary>
    public int? Year { get; init; }

    /// <summary>License plate (state-abbreviation + plate combined; e.g. <c>"WA ABC1234"</c>).</summary>
    public string? LicensePlate { get; init; }

    /// <summary>Current odometer reading in miles; updated when a <see cref="TripRecord"/> is appended.</summary>
    public decimal CurrentOdometer { get; init; }
}
```

**`TripRecordId.cs`** (follow exact pattern of `EquipmentId.cs`)
```csharp
namespace Sunfish.Blocks.PropertyEquipment.Models;

[JsonConverter(typeof(TripRecordIdJsonConverter))]
public readonly record struct TripRecordId(string Value)
{
    public override string ToString() => Value;
    public static implicit operator TripRecordId(string value) => new(value);
    public static implicit operator string(TripRecordId id) => id.Value;
    public static TripRecordId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class TripRecordIdJsonConverter : JsonConverter<TripRecordId>
{
    public override TripRecordId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? throw new JsonException("TripRecordId must be non-null."));
    public override void Write(Utf8JsonWriter writer, TripRecordId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
```

**`TripRecord.cs`**
```csharp
using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.PropertyEquipment.Models;

/// <summary>
/// Append-only record of a vehicle trip. Miles = <see cref="EndOdometer"/> minus
/// <see cref="StartOdometer"/>. The parent <see cref="VehicleMetadata.CurrentOdometer"/>
/// is updated to <see cref="EndOdometer"/> on append.
/// </summary>
public sealed record TripRecord : IMustHaveTenant
{
    public required TripRecordId Id { get; init; }
    public required TenantId TenantId { get; init; }

    /// <summary>FK to the <see cref="Equipment"/> record (must have Class = Vehicle).</summary>
    public required EquipmentId EquipmentId { get; init; }

    /// <summary>FK to the property visited during this trip.</summary>
    public required PropertyId PropertyId { get; init; }

    /// <summary>UTC date of the trip.</summary>
    public required DateTimeOffset TripDate { get; init; }

    /// <summary>Odometer reading at trip start (miles).</summary>
    public required decimal StartOdometer { get; init; }

    /// <summary>Odometer reading at trip end (miles).</summary>
    public required decimal EndOdometer { get; init; }

    /// <summary>Distance driven (computed; always non-negative).</summary>
    public decimal Miles => Math.Max(0m, EndOdometer - StartOdometer);

    /// <summary>Purpose of the trip (e.g. <c>"maintenance"</c>, <c>"inspection"</c>, <c>"delivery"</c>).</summary>
    public string? Purpose { get; init; }

    /// <summary>Optional free-text notes.</summary>
    public string? Notes { get; init; }
}
```

### Modifications

**`Equipment.cs`** — add one property after `Warranty`:
```csharp
/// <summary>
/// Vehicle-specific metadata. Non-null only when <see cref="Class"/> is
/// <see cref="EquipmentClass.Vehicle"/>; null on all other classes.
/// </summary>
public VehicleMetadata? VehicleData { get; init; }
```

**`EquipmentLifecycleEventType.cs`** — add after `NotesUpdated`:
```csharp
/// <summary>A vehicle trip was recorded against this equipment (Vehicle class only).</summary>
MileageRecorded,
```

### New file in `packages/blocks-property-equipment/Services/`

**`ITripStore.cs`**
```csharp
using Sunfish.Blocks.PropertyEquipment.Models;

namespace Sunfish.Blocks.PropertyEquipment.Services;

/// <summary>
/// Append-only store for <see cref="TripRecord"/> entries. Implementations
/// must reject <see cref="TripRecord.StartOdometer"/> greater than
/// <see cref="TripRecord.EndOdometer"/> (negative-miles guard).
/// </summary>
public interface ITripStore
{
    Task<TripRecord?> GetAsync(TripRecordId id, CancellationToken ct);
    Task<IReadOnlyList<TripRecord>> GetForEquipmentAsync(EquipmentId equipmentId, CancellationToken ct);
    Task AppendAsync(TripRecord record, CancellationToken ct);
}
```

**`InMemoryTripStore.cs`** — concurrent dictionary implementation (follow
`InMemoryEquipmentRepository.cs` pattern exactly).

### Entity module extension

In `Sunfish.Blocks.PropertyEquipment`'s `ISunfishEntityModule` contribution, add
`TripRecord` to the `ModelBuilder` configuration so EFCore picks it up:
- Primary key: `TripRecordId` (string-backed)
- Index on `(TenantId, EquipmentId)` for `GetForEquipmentAsync`
- Index on `(TenantId, PropertyId)` for future property-trip queries

### DI registration

Add `ITripStore` → `InMemoryTripStore` to the package's service registration extension
(follow the existing `IEquipmentRepository` registration pattern).

### Tests (Phase 1)

- `AppendAsync` then `GetForEquipmentAsync` returns the record
- `TripRecord.Miles` = `EndOdometer - StartOdometer` (positive case)
- `TripRecord.Miles` = 0 when `EndOdometer <= StartOdometer` (guard case)
- `GetAsync` returns null for unknown id
- `VehicleMetadata.CurrentOdometer` follows latest `EndOdometer` (integration smoke)

**Halt condition:** If `ISunfishEntityModule` contribution pattern differs from assumed
(e.g., EFCore not used in this package) → read the existing module registration and adapt;
do NOT invent a new registration path.

**PR title:** `feat(blocks-property-equipment): W#61 — VehicleMetadata + TripRecord + ITripStore + MileageRecorded`

---

## Phase 2 — Docs + ledger (~30min)

- `apps/docs/blocks/property-equipment/vehicle-trip-records.md` — vehicle subtype guide,
  `VehicleMetadata` fields, `TripRecord` append semantics, odometer update behavior
- Flip W#61 ledger row to `built`

**PR title:** `docs: W#61 vehicle equipment subtype + trip records guide + ledger flip`

---

## Acceptance criteria

- [ ] `Equipment` with `Class = Vehicle` accepts non-null `VehicleData`
- [ ] `TripRecord` persists via `ITripStore.AppendAsync`; `GetForEquipmentAsync` retrieves it
- [ ] `TripRecord.Miles` is always non-negative
- [ ] `MileageRecorded` appears in `EquipmentLifecycleEventType`
- [ ] `InMemoryTripStore` is registered via DI and passes the store tests
- [ ] EFCore entity module includes `TripRecord` table with correct indexes
- [ ] 5 unit tests passing (see above)
- [ ] docs page live

---

## Halt conditions

1. `ISunfishEntityModule` does not exist or uses a different EFCore registration pattern →
   read the existing `blocks-property-equipment` module registration and adapt.
2. `EquipmentId` or `PropertyId` FK types have changed since W#24 shipped →
   read current model files and use actual types.
3. `InMemoryEquipmentRepository.cs` does not exist at the expected path → `ls` the
   Services/ directory and follow the nearest in-memory pattern.
