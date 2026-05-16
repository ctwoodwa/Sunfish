# Hand-off — W#62 PropertyUnit substrate

**From:** XO (research session)
**To:** sunfish-PM (COB)
**Created:** 2026-05-16
**Parent workstream:** W#62 (new; additive extension of `blocks-properties`)
**Pipeline variant:** `sunfish-feature-change`
**Estimate:** ~8–12h / 3–4 PRs

> **No gate.** Immediately buildable. Unblocks:
> - W#29 Phase 1.5 (real property-detail aggregation for cockpit)
> - W#29 Phase 5 (dashboard — same root cause)
> - Future: W#22/W#25/W#27 integrations that join on unit
>
> **Background:** `Property.cs` explicitly deferred `PropertyUnit` to a follow-up
> hand-off (its doc-comment says "PropertyUnit child deferred"). W#62 is that follow-up.
>
> **W#29 Phase 2 instruction (from XO ruling):** Ship Phase 2 PR with Property card +
> Equipment list; stub Lease/WO/Inspection aggregation fields as `null`/`0` with
> comments `// Stubbed — W#62 PropertyUnit substrate required`. PRs 3 and 4 ship
> unaffected. Then pick up W#62 and Phase 1.5 to wire the real data.

---

## Context

`blocks-properties` shipped its first slice (W#17) covering the `Property` root entity
only. The `PropertyUnit` child entity was explicitly deferred. Meanwhile,
`blocks-leases` and `blocks-inspections` reference units via `EntityId` (wire form:
`unit:{authority}/{localPart}`), but there is no `IPropertyUnitRepository` to query
which units belong to which property. W#62 fills that gap.

W#29 Phase 2 discovered this gap: `IEquipmentRepository.ListByPropertyAsync` exists
(equipment already links to property), but leases and inspections cannot be filtered by
property because the unit-to-property join layer doesn't exist.

---

## PR 1 — PropertyUnit entity + IPropertyUnitRepository + DI + EFCore (~3–4h)

### New files in `packages/blocks-properties/Models/`

**`UnitStatus.cs`**
```csharp
namespace Sunfish.Blocks.Properties.Models;

/// <summary>Operational status of a <see cref="PropertyUnit"/>.</summary>
public enum UnitStatus
{
    /// <summary>Ready for occupancy; no active lease.</summary>
    Available,

    /// <summary>Unit has an active lease.</summary>
    Occupied,

    /// <summary>Temporarily out of service for maintenance or renovation.</summary>
    MaintenanceHold,
}
```

**`PropertyUnit.cs`**
```csharp
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.Properties.Models;

/// <summary>
/// A rentable or manageable unit within a <see cref="Property"/>.
/// Single-family homes typically have one unit; multi-family may have many.
/// </summary>
/// <remarks>
/// <para>
/// <b>Unit identity:</b> <see cref="Id"/> is an <see cref="EntityId"/> with
/// scheme <c>"unit"</c> — e.g. <c>unit:acme-rentals/550e8400-e29b</c>.
/// This matches the <c>EntityId UnitId</c> FK type already used by
/// <c>blocks-leases</c> and <c>blocks-inspections</c>.
/// </para>
/// </remarks>
public sealed record PropertyUnit : IMustHaveTenant
{
    /// <summary>
    /// Stable identifier. Scheme = <c>"unit"</c>. Use
    /// <see cref="NewId"/> to generate.
    /// </summary>
    public required EntityId Id { get; init; }

    /// <summary>Owning tenant.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Parent property.</summary>
    public required PropertyId PropertyId { get; init; }

    /// <summary>
    /// Human-readable unit identifier within the property
    /// (e.g. <c>"101"</c>, <c>"2B"</c>, <c>"Main"</c>, <c>"Garage"</c>).
    /// </summary>
    public required string UnitNumber { get; init; }

    /// <summary>Bedroom count. Optional; null for non-residential or unknown.</summary>
    public int? Bedrooms { get; init; }

    /// <summary>Bathroom count (allows half-baths e.g. <c>1.5</c>).</summary>
    public decimal? Bathrooms { get; init; }

    /// <summary>Interior square footage. Optional.</summary>
    public decimal? SquareFootage { get; init; }

    /// <summary>Current operational status.</summary>
    public required UnitStatus Status { get; init; }

    /// <summary>Record-creation timestamp; immutable after first persist.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Free-text notes.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Generates a new <see cref="EntityId"/> with scheme <c>"unit"</c>
    /// for use as a <see cref="PropertyUnit"/> identifier.
    /// </summary>
    public static EntityId NewId(TenantId tenant)
        => EntityId.Parse($"unit:{tenant.Value}/{Guid.NewGuid():N}");
}
```

### New files in `packages/blocks-properties/Services/`

**`IPropertyUnitRepository.cs`**
```csharp
using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.Properties.Services;

/// <summary>
/// Domain repository for <see cref="PropertyUnit"/>. Tenant-scoping is
/// mandatory on every call — the repository never returns units from other
/// tenants.
/// </summary>
public interface IPropertyUnitRepository
{
    /// <summary>
    /// Returns the unit with the given <paramref name="id"/>, or
    /// <c>null</c> if not found in the tenant's scope.
    /// </summary>
    Task<PropertyUnit?> GetByIdAsync(
        TenantId tenant, EntityId id, CancellationToken ct = default);

    /// <summary>
    /// Lists all units belonging to the given property.
    /// </summary>
    Task<IReadOnlyList<PropertyUnit>> ListByPropertyAsync(
        TenantId tenant, PropertyId propertyId, CancellationToken ct = default);

    /// <summary>
    /// Lists all units for the tenant across all properties.
    /// </summary>
    Task<IReadOnlyList<PropertyUnit>> ListByTenantAsync(
        TenantId tenant, CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates the unit. Asserts that
    /// <see cref="PropertyUnit.TenantId"/> matches the caller's scope.
    /// </summary>
    Task UpsertAsync(PropertyUnit unit, CancellationToken ct = default);
}
```

**`InMemoryPropertyUnitRepository.cs`** — follow the exact pattern of
`InMemoryPropertyRepository.cs` (concurrent-dictionary keyed on
`(TenantId, EntityId)`).

```csharp
using System.Collections.Concurrent;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.Properties.Services;

public sealed class InMemoryPropertyUnitRepository : IPropertyUnitRepository
{
    private readonly ConcurrentDictionary<(TenantId, EntityId), PropertyUnit> _store = new();

    public Task<PropertyUnit?> GetByIdAsync(
        TenantId tenant, EntityId id, CancellationToken ct = default)
    {
        _store.TryGetValue((tenant, id), out var unit);
        return Task.FromResult(unit);
    }

    public Task<IReadOnlyList<PropertyUnit>> ListByPropertyAsync(
        TenantId tenant, PropertyId propertyId, CancellationToken ct = default)
    {
        IReadOnlyList<PropertyUnit> result = _store
            .Where(kvp => kvp.Key.Item1.Equals(tenant)
                       && kvp.Value.PropertyId.Equals(propertyId))
            .Select(kvp => kvp.Value)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<PropertyUnit>> ListByTenantAsync(
        TenantId tenant, CancellationToken ct = default)
    {
        IReadOnlyList<PropertyUnit> result = _store
            .Where(kvp => kvp.Key.Item1.Equals(tenant))
            .Select(kvp => kvp.Value)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpsertAsync(PropertyUnit unit, CancellationToken ct = default)
    {
        _store[(unit.TenantId, unit.Id)] = unit;
        return Task.CompletedTask;
    }
}
```

### EFCore entity module extension

In `Sunfish.Blocks.Properties`'s `ISunfishEntityModule` contribution, add `PropertyUnit`
to the `ModelBuilder` configuration:
- Primary key: `EntityId` (mapped as string via `ToString()` / `EntityId.Parse()` value
  converter — follow the existing `Property` entity's `PropertyId` string-backing pattern)
- Index on `(TenantId, PropertyId)` for `ListByPropertyAsync`
- Index on `(TenantId)` for `ListByTenantAsync`

**Halt condition:** If `ISunfishEntityModule` is not used in `blocks-properties` → read
the existing entity-module registration in the package and adapt; do NOT invent a new
registration path. The pattern from `blocks-property-equipment` (W#61) is the closest
reference.

### DI registration

In the package's service-registration extension, add:
```csharp
services.AddScoped<IPropertyUnitRepository, InMemoryPropertyUnitRepository>();
```
Follow the existing `IPropertyRepository` registration pattern exactly.

### Tests (PR 1)

In `packages/blocks-properties/tests/`:
1. `UpsertAsync` then `ListByPropertyAsync` returns the unit for the correct property
2. `ListByPropertyAsync` excludes units from other properties (same tenant)
3. `ListByTenantAsync` returns all units across properties for the tenant
4. `GetByIdAsync` returns null for unknown EntityId
5. `PropertyUnit.NewId(tenant)` produces an EntityId with scheme `"unit"` and non-empty
   authority + localPart

**PR title:** `feat(blocks-properties): W#62 Phase 1 — PropertyUnit entity + IPropertyUnitRepository + InMemory impl + EFCore + DI`

---

## PR 2 — W#29 Phase 1.5: real property aggregation in cockpit (~2–3h)

This PR upgrades `PropertyDetailEndpoint.cs` from W#29 Phase 2 (which ships stubs)
to live aggregation now that `IPropertyUnitRepository` exists.

### Inject into `PropertyDetailEndpoint.cs`

Add `IPropertyUnitRepository` to the endpoint's injected dependencies alongside the
existing `IPropertyRepository`, `IEquipmentRepository`, `ILeaseService`, and
`IInspectionsService`.

### Aggregation logic

```csharp
// 1. Get units for this property.
var units = await _unitRepo.ListByPropertyAsync(tenant, propertyId, ct);
var unitIds = units.Select(u => u.Id).ToHashSet();  // HashSet<EntityId>

// 2. Find the active lease on any unit in this property (in-memory filter).
//    ILeaseService.ListAsync returns IAsyncEnumerable<Lease>.
Lease? activeLease = null;
if (unitIds.Count > 0)
{
    activeLease = await _leaseService
        .ListAsync(new ListLeasesQuery { Phase = LeasePhase.Active }, ct)
        .FirstOrDefaultAsync(l => unitIds.Contains(l.UnitId), ct);
}

// 3. Open work-order count — stubbed 0 until W#62 PR 3 adds PropertyId to WorkOrder.
var openWorkOrderCount = 0;

// 4. Last inspection across all units.
//    ListInspectionsQuery with UnitId = null returns all inspections for the tenant.
Inspection? latestInspection = null;
if (unitIds.Count > 0)
{
    var allInspections = await _inspectionsService.ListAsync(
        new ListInspectionsQuery(), ct).ToListAsync(ct);
    latestInspection = allInspections
        .Where(i => unitIds.Contains(i.UnitId))
        .OrderByDescending(i => i.ScheduledAt)
        .FirstOrDefault();
}
```

**Halt conditions:**
- `ILeaseService.ListAsync` signature or `IAsyncEnumerable<Lease>` return type has changed
  → read the actual interface and adapt.
- `IInspectionsService.ListAsync` takes a different query parameter → read and adapt.
- `Lease.UnitId` type is not `EntityId` → read `blocks-leases/Models/Lease.cs` and use
  the correct comparison (e.g. `l.UnitId.LocalPart == unit.Id.LocalPart`).
- `Inspection.UnitId` type differs → same: read and adapt.

**Tests (PR 2):** Update the Bridge integration test from W#29 Phase 2 to seed a
`PropertyUnit`, a `Lease` on that unit, and an `Inspection` on that unit, and assert
that `PropertyDetailDto.ActiveLease` and `LastInspectionDate` are now populated.

**PR title:** `feat(bridge,anchor): W#29 Phase 1.5 / W#62 Phase 2 — real property-detail aggregation via IPropertyUnitRepository`

---

## PR 3 — WorkOrder.PropertyId FK addition (~2–3h)

This is an API-change PR against `blocks-maintenance`. `WorkOrder` currently has no
property or unit link. Work orders are property-level (not unit-level), so `PropertyId`
is the correct FK.

### Modifications to `blocks-maintenance`

**`WorkOrder.cs`** — add after `TenantId`:
```csharp
/// <summary>
/// Property this work order is associated with. Required for all work
/// orders issued against a specific property; null only for tenant-wide
/// administrative work orders.
/// </summary>
public PropertyId? PropertyId { get; init; }
```

Note: `PropertyId?` is nullable so existing `WorkOrder` creation paths compile without
changes. Infrastructure paths that need property filtering must supply it going forward.

**`ListWorkOrdersQuery`** — add after existing filter fields:
```csharp
/// <summary>When set, only work orders for this property are returned.</summary>
public PropertyId? PropertyId { get; init; }
```

**`IWorkOrderService`** — verify `ListAsync(ListWorkOrdersQuery, ct)` already exists
and applies the new filter. If `InMemoryWorkOrderService.ListAsync` doesn't filter by
`PropertyId` yet, add the filter clause.

**EFCore entity module:** add index on `(TenantId, PropertyId)` for the `WorkOrder`
table in the `blocks-maintenance` module registration.

**Tests (PR 3):**
1. `WorkOrder` with `PropertyId` set → `ListAsync(new { PropertyId = x })` returns it
2. `WorkOrder` with `PropertyId = null` → excluded from the above query
3. `WorkOrder` with a different `PropertyId` → excluded from the query

**After PR 3 lands:** Update `PropertyDetailEndpoint.cs` to replace the `openWorkOrderCount = 0`
stub with:
```csharp
var openWorkOrderCount = await _workOrderService
    .ListAsync(new ListWorkOrdersQuery
    {
        PropertyId = propertyId,
        Status = WorkOrderStatus.Open,
    }, ct)
    .CountAsync(ct);
```

**PR title:** `feat(blocks-maintenance): W#62 Phase 3 — WorkOrder.PropertyId FK + ListByPropertyAsync`

---

## PR 4 — Docs + ledger flip (~30min)

- `apps/docs/blocks/properties/property-unit.md` — `PropertyUnit` model reference,
  `EntityId` scheme (`"unit"`), `NewId(tenant)` usage, `IPropertyUnitRepository` methods
- `apps/docs/blocks/properties/property-aggregation.md` — guide: how to aggregate
  data across leases, inspections, and work orders for a single property using
  `IPropertyUnitRepository`
- Flip W#62 ledger row from `ready-to-build` → `built`
- Update W#29 Phase 2 row note to reflect Phase 1.5 landed

**PR title:** `docs: W#62 PropertyUnit + property-aggregation guide + ledger flip`

---

## Acceptance criteria

- [ ] `PropertyUnit` with `EntityId` (scheme `"unit"`) persists via `IPropertyUnitRepository`
- [ ] `ListByPropertyAsync` returns only units for the given property
- [ ] `PropertyUnit.NewId(tenant)` produces a valid scheme-`"unit"` EntityId
- [ ] `InMemoryPropertyUnitRepository` registered via DI
- [ ] Bridge `GET /cockpit/{propertyId}/detail` returns populated `ActiveLease` and
  `LastInspectionDate` when units + lease + inspection are seeded (Phase 1.5 test)
- [ ] `WorkOrder.PropertyId` nullable FK added; `ListWorkOrdersQuery.PropertyId` filter
  works in memory and EFCore
- [ ] Cockpit `OpenWorkOrderCount` populated from real data after PR 3 lands
- [ ] 5 unit tests for `IPropertyUnitRepository` (PR 1)
- [ ] 3 unit tests for `WorkOrder.PropertyId` filter (PR 3)
- [ ] Docs pages live
- [ ] W#62 ledger row flipped to `built`

---

## Halt conditions

1. `ISunfishEntityModule` pattern differs in `blocks-properties` → read the existing
   module registration and adapt.
2. `ILeaseService.ListAsync` return type is not `IAsyncEnumerable<Lease>` → adapt;
   do not invent a replacement.
3. `Lease.UnitId` or `Inspection.UnitId` type is not `EntityId` → read the actual
   model files and adjust the comparison in the aggregation logic.
4. `IInspectionsService.ListAsync` requires a mandatory `UnitId` parameter → filter
   per-unit in a loop instead of post-filter.
5. `ListWorkOrdersQuery` does not exist or is sealed → read the actual type and adapt.
