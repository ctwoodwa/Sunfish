# Hand-off — Property-Assets domain block (first-slice)

**From:** research session
**To:** sunfish-PM session
**Created:** 2026-04-28 (revised 2026-04-28 to resolve `blocks-property-assets` package-name decision)
**Status:** `ready-to-build`
**Revision note:** Initial draft proposed `packages/blocks-assets/` — collides with existing UI-catalog block `Sunfish.Blocks.Assets` (`AssetCatalogBlock.razor`). **Renamed to `packages/blocks-property-assets/` and namespace `Sunfish.Blocks.PropertyAssets`** to clarify the property-management-vertical scope and avoid collision. Convention going forward: property-ops cluster blocks use `blocks-property-*` prefix where bare names collide with existing blocks (Properties already shipped as `blocks-properties` with no prefix — that's the cluster root and stays as-is).
**Spec source:** Cluster intake [`property-assets-intake-2026-04-28.md`](../../00_intake/output/property-assets-intake-2026-04-28.md) (Stage 00) + cluster INDEX [`property-ops-INDEX-intake-2026-04-28.md`](../../00_intake/output/property-ops-INDEX-intake-2026-04-28.md)
**Approval:** Cluster intake names this as the natural second cluster module after Properties (Asset FKs to Property; no other dependencies). This hand-off compresses Stages 01–05 into the hand-off itself for the first-slice scope. Vehicle subtype + mileage + ownership log + condition assessments deferred to follow-up hand-offs.
**Estimated cost:** ~4–6 hours sunfish-PM (slightly larger than Properties first-slice; introduces lifecycle event log pattern)
**Pipeline:** `sunfish-feature-change`
**Blocked by:** Properties first-slice (workstream #17) merging — Asset.Property FK target must exist

---

## Context (one paragraph)

Assets are the second cluster module to ship — they FK to Property and provide the inventory backbone for inspections, work orders, receipts (acquisition cost basis), and depreciation reporting. The cluster intake names this as the natural follow-on after Properties: depends only on Accepted ADRs (0008 multi-tenancy, 0015 entity registration, 0049 audit substrate); no Proposed-ADR gates. First-slice scope: scaffold the package + ship `Asset` entity + `AssetClass` discriminator + `AssetLifecycleEvent` append-only log + basic CRUD. Vehicle subtype (with mileage Trip events), `AssetConditionAssessment` integration with inspections, OCR-ingested asset capture from iOS, and tax-advisor depreciation projection are explicitly **deferred to subsequent hand-offs** — they have integration dependencies on cluster intakes that haven't shipped yet.

---

## Phases (binary gates)

### Phase 1 — Scaffold `packages/blocks-property-assets/`

**Files:**

- **NEW** `packages/blocks-property-assets/Sunfish.Blocks.PropertyAssets.csproj` — mirror `blocks-properties` patterns; references `Sunfish.Foundation`, `Sunfish.Foundation.MultiTenancy`, `Sunfish.Foundation.Persistence`, `Sunfish.Blocks.Properties` (FK target), `Sunfish.Kernel.Audit` (event emission per ADR 0049)
- **NEW** Add to `Sunfish.slnx` under `/blocks/assets/`

**PASS gate:** `dotnet build` green; provider-neutrality analyzer passes.

### Phase 2 — `AssetId` + `Asset` entity + `AssetClass` discriminator

**Files:**

- **NEW** `packages/blocks-property-assets/Models/AssetId.cs` — mirror `PropertyId` shape (record struct + JSON converter + `NewId()` factory)
- **NEW** `packages/blocks-property-assets/Models/AssetClass.cs`

```csharp
public enum AssetClass
{
    WaterHeater,
    HVAC,
    Appliance,        // generic — fridge, dishwasher, range, washer, dryer, microwave; sub-classification later
    Roof,
    Vehicle,          // reserved; full subtype gated on follow-up hand-off
    Plumbing,         // fixtures, pipes
    Electrical,       // panels, fixtures
    Other             // catch-all; tag with Asset.Notes for now
}
```

(Schema-registry-backed AssetClass per cluster intake OQ-A2 is a Phase 2.3+ amendment; enum suffices for first-slice.)

- **NEW** `packages/blocks-property-assets/Models/Asset.cs`

```csharp
public sealed record Asset
{
    public required AssetId Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required PropertyId Property { get; init; }                  // FK; required (no orphan assets)
    public PropertyUnitId? Unit { get; init; }                          // forward-compat for multi-unit; nullable until PropertyUnit ships
    public required AssetClass Class { get; init; }
    public required string DisplayName { get; init; }                   // e.g., "Master bath water heater"
    public string? Make { get; init; }                                  // e.g., "Rheem"
    public string? Model { get; init; }                                 // e.g., "XR50T06EC36U1"
    public string? SerialNumber { get; init; }                          // captured from nameplate
    public string? LocationInProperty { get; init; }                    // e.g., "Garage west wall"
    public DateTimeOffset? InstalledAt { get; init; }
    public Money? AcquisitionCost { get; init; }                        // ADR 0051 (Proposed). Use decimal? placeholder if Money not yet importable; one-line follow-up on ADR 0051 acceptance
    public AssetId? AcquisitionReceiptRef { get; init; }                // forward-compat for Receipts FK; nullable until Receipts module ships
    public int? ExpectedUsefulLifeYears { get; init; }                  // e.g., 12 for water heater
    public WarrantyMetadata? Warranty { get; init; }                    // see Phase 3
    public string? Notes { get; init; }
    public string? PrimaryPhotoBlobRef { get; init; }                   // FK reservation (blob ingest gated on cluster OQ3)
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DisposedAt { get; init; }                    // soft-delete on replacement/disposal
    public string? DisposalReason { get; init; }
}
```

**PASS gate:** Compiles; XML doc; round-trip JSON test on `AssetId`.

### Phase 3 — `WarrantyMetadata` value object

**Files:**

- **NEW** `packages/blocks-property-assets/Models/WarrantyMetadata.cs`

```csharp
public sealed record WarrantyMetadata
{
    public required DateTimeOffset StartsAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public string? Provider { get; init; }                              // e.g., "Manufacturer", "Best Buy Geek Squad"
    public string? PolicyNumber { get; init; }
    public string? CoverageNotes { get; init; }
}
```

**PASS gate:** Compiles; XML doc; one round-trip JSON test.

### Phase 4 — `AssetLifecycleEvent` append-only log

**Files:**

- **NEW** `packages/blocks-property-assets/Models/AssetLifecycleEventType.cs`

```csharp
public enum AssetLifecycleEventType
{
    Installed,
    Serviced,
    Inspected,
    WarrantyClaimed,
    Replaced,
    Disposed,
    PhotoAdded,
    NotesUpdated
}
```

- **NEW** `packages/blocks-property-assets/Models/AssetLifecycleEvent.cs`

```csharp
public sealed record AssetLifecycleEvent
{
    public required Guid EventId { get; init; }
    public required AssetId Asset { get; init; }
    public required TenantId Tenant { get; init; }
    public required AssetLifecycleEventType EventType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required IdentityRef RecordedBy { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyDictionary<string,string>? Metadata { get; init; } // event-type-specific payload
}
```

- **NEW** `packages/blocks-property-assets/IAssetLifecycleEventStore.cs` — append-only contract:

```csharp
public interface IAssetLifecycleEventStore
{
    Task AppendAsync(AssetLifecycleEvent ev, CancellationToken ct);
    Task<IReadOnlyList<AssetLifecycleEvent>> GetForAssetAsync(TenantId tenant, AssetId asset, CancellationToken ct);
    Task<IReadOnlyList<AssetLifecycleEvent>> GetForPropertyAsync(TenantId tenant, PropertyId property, CancellationToken ct);
}
```

- **NEW** `packages/blocks-property-assets/InMemoryAssetLifecycleEventStore.cs` — thread-safe in-memory implementation
- **AUDIT-EMISSION** Each `AppendAsync` ALSO emits to ADR 0049 audit substrate via `IAuditTrail` (existing primitive). Audit record type: `AssetLifecycleEventEmitted` (subtype of existing audit record envelope; review existing kernel-audit subtypes for the right pattern).

**PASS gate:** Compiles; round-trip tests for store; tenant isolation tests; ADR 0049 audit emission verified by existing kernel-audit test harness.

### Phase 5 — `IAssetRepository` + in-memory CRUD

**Files:**

- **NEW** `packages/blocks-property-assets/IAssetRepository.cs`

```csharp
public interface IAssetRepository
{
    Task<Asset?> GetByIdAsync(TenantId tenant, AssetId id, CancellationToken ct);
    Task<IReadOnlyList<Asset>> ListByPropertyAsync(TenantId tenant, PropertyId property, bool includeDisposed = false, CancellationToken ct = default);
    Task<IReadOnlyList<Asset>> ListByTenantAsync(TenantId tenant, bool includeDisposed = false, CancellationToken ct = default);
    Task<IReadOnlyList<Asset>> ListByClassAsync(TenantId tenant, AssetClass assetClass, CancellationToken ct);
    Task UpsertAsync(Asset asset, CancellationToken ct);
    Task SoftDeleteAsync(TenantId tenant, AssetId id, string reason, DateTimeOffset disposedAt, CancellationToken ct);
}
```

- **NEW** `packages/blocks-property-assets/InMemoryAssetRepository.cs` — thread-safe; Get/List/Upsert/SoftDelete; SoftDeleteAsync ALSO appends an `AssetLifecycleEvent` of type `Disposed` with the reason
- **NEW** `packages/blocks-property-assets/AssetEntityModule.cs` — `ISunfishEntityModule` registration per ADR 0015
- **NEW** `packages/blocks-property-assets/ServiceCollectionExtensions.cs` — `AddAssetBlock(this IServiceCollection services)` registers repository + event store + entity module

**PASS gate:** All files compile; runtime DI resolves repository + event store; SoftDelete emits Disposed lifecycle event.

### Phase 6 — Tests + kitchen-sink demo seed

**Files:**

- **NEW** `packages/blocks-property-assets/tests/Sunfish.Blocks.PropertyAssets.Tests.csproj`
- **NEW** `packages/blocks-property-assets/tests/AssetTests.cs` — record equality + JSON round-trip
- **NEW** `packages/blocks-property-assets/tests/InMemoryAssetRepositoryTests.cs` — Get/List/Upsert/SoftDelete; tenant isolation; class-filter; property-filter; soft-delete preserves record but excludes from default List
- **NEW** `packages/blocks-property-assets/tests/AssetLifecycleEventStoreTests.cs` — append + retrieve; tenant isolation; chronological ordering
- **NEW** `packages/blocks-property-assets/tests/AssetLifecycleEventStoreAuditEmissionTests.cs` — verify each AppendAsync emits to ADR 0049 substrate
- **NEW** seed data in `apps/kitchen-sink/`:
  - For Property "123 Main St" (single-family): water heater, HVAC, dishwasher, washer, dryer
  - For Property "456 Oak Ave" (multi-unit): water heater per unit, shared HVAC, shared roof
  - Each asset gets one or two `AssetLifecycleEvent`s (Installed, plus optional Serviced)

**PASS gate:** `dotnet test packages/blocks-property-assets/tests/` returns 0 failures; kitchen-sink boots and seed assets render alongside seed properties.

### Phase 7 — Documentation

**Files:**

- **NEW** `apps/docs/blocks/assets.md` — block summary, field reference for `Asset`, lifecycle event types table, "what's not in this slice" deferred list (Vehicle subtype + Trip events; AssetConditionAssessment integration; OCR ingest; depreciation projection), cross-link to cluster intake.

**PASS gate:** apps/docs builds; new doc page renders.

### Phase 8 — Workstream ledger flip

**Files:**

- **EDIT** `icm/_state/active-workstreams.md` row #24 (Assets domain): Status → `built` (merged); reference merged PR; notes append "First-slice (Asset entity + AssetClass + lifecycle event log + CRUD) shipped. Vehicle subtype + Trip events + AssetConditionAssessment integration + OCR ingest queued as separate hand-offs."

**PASS gate:** Ledger updated; PR ready to merge.

---

## Out of scope (explicit deferred to follow-up hand-offs)

- **Vehicle AssetClass subtype + Trip events** — cluster intake folds vehicles + mileage into Assets. Defer Trip event entity + Vehicle-specific fields (VIN, license plate, base_mileage_at_acquisition, business_use_percentage, primary_driver) to next hand-off.
- **AssetConditionAssessment integration** — gated on Inspections module shipping (cluster intake #25). Asset has NO condition field in this first-slice; condition lives on Inspection.
- **OCR-ingested asset capture from iOS** — gated on iOS Field App intake (#23). Asset capture path is defined in cluster intake; first-slice has no iOS path.
- **Tax-advisor depreciation projection** — gated on `blocks-tax-reporting` consumer + ADR 0051 acceptance. First-slice carries `AcquisitionCost` and `ExpectedUsefulLifeYears` as raw fields; depreciation calc is downstream.
- **Schema-registry-backed AssetClass** — cluster intake OQ-A2; deferred to Phase 2.3+ when more asset classes accumulate.
- **Asset hierarchy (parent-child for HVAC = compressor + air handler)** — cluster intake OQ-A1; defer (flat for v1).
- **Migration import tool** (one-shot from BDFL's spreadsheet) — Phase 2 onboarding work; separate hand-off.

---

## What sunfish-PM should NOT touch

- `packages/blocks-properties/` (consumer only; Property FK)
- `packages/blocks-rent-collection/` (unrelated)
- `accelerators/` (consumer integration is a follow-up; first-slice doesn't wire UI)
- iOS app (`accelerators/anchor-mobile-ios/` — doesn't exist yet; cluster intake)
- ADR documents (no new or amended ADRs at this slice; ADR 0008 + 0015 + 0049 cover the patterns)

---

## Open questions sunfish-PM should flag back to research

1. **`PropertyUnit` FK readiness.** The `Asset.Unit: PropertyUnitId?` field references a type that's deferred to a follow-up Properties hand-off. For first-slice, you can either: (a) define `PropertyUnitId` as a placeholder record struct in `blocks-properties` (cleaner; aligns with intent), or (b) drop the field entirely from first-slice and add via api-change hand-off. **Recommend (a).** Flag if disagree.
2. **AssetEvent vs AuditRecord shape.** `IAuditTrail` substrate has its own record envelope. Should `AssetLifecycleEvent` extend the audit record's `IAuditRecord` interface, or stay as a domain-specific record + emit a separate audit record? **Read existing kernel-audit subtypes (per PR #190 + #198 patterns) and follow the convention there.** Flag if patterns conflict.
3. **`AcquisitionReceiptRef` shape.** Field reserves an FK to a receipt record that doesn't exist yet (cluster intake #26). Type: `AssetId?` is wrong — should be a future `ReceiptId?`. **Recommend reserving as opaque `string?` for first-slice;** convert to typed FK when Receipts module ships.
4. **kitchen-sink rendering surface.** Properties first-slice should be rendering its 2 seed properties somewhere (either in a UI page or in startup logs). If Properties hand-off shipped without a UI page, this Assets hand-off should match that pattern (logging only) rather than introducing a new UI surface.

---

## Acceptance criteria (research-session sign-off)

- [ ] All 8 phases complete with PASS gates
- [ ] `dotnet build` + `dotnet test` repo-wide green
- [ ] Provider-neutrality analyzer passes on `blocks-property-assets`
- [ ] kitchen-sink demo seed renders/logs assets per property
- [ ] `apps/docs/blocks/assets.md` exists with deferred-list
- [ ] Workstream #24 ledger row flipped to `built` (merged) with PR link
- [ ] PR description names Phase 1 (this hand-off) as the slice scope; flags Vehicle/Trip + Condition + OCR + depreciation as deferred to follow-up hand-offs
- [ ] No code outside `packages/blocks-property-assets/`, `Sunfish.slnx`, `apps/kitchen-sink/<seed>`, `apps/docs/blocks/assets.md`, `icm/_state/active-workstreams.md` is touched

---

## After this hand-off ships

Research session will write the next two hand-offs to keep the queue full:

- `property-assets-vehicle-trip-events-handoff.md` — Vehicle subtype + Trip event entity (mileage logging); manual entry only (GPS auto-tracking is Phase 2.1c separate ADR)
- `property-properties-unit-entity-handoff.md` — PropertyUnit child entity (multi-unit modeling); resolves cluster intake OQ-P1
- `property-receipts-stage06-handoff.md` — Receipts first-slice (depends on ADR 0051 acceptance + Properties + Assets first-slices shipped)

---

## Sign-off

Research session — 2026-04-28
