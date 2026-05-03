# Hand-off — Properties domain block (first-slice)

**From:** research session
**To:** sunfish-PM session
**Created:** 2026-04-28
**Status:** `ready-to-build`
**Spec source:** Cluster intake [`property-properties-intake-2026-04-28.md`](../../00_intake/output/property-properties-intake-2026-04-28.md) (Stage 00) + cluster INDEX [`property-ops-INDEX-intake-2026-04-28.md`](../../00_intake/output/property-ops-INDEX-intake-2026-04-28.md) (recommended Stage 01 entry point, no upstream blockers)
**Approval:** Cluster INDEX explicitly names Properties as the recommended Stage 01 entry; this hand-off compresses Stages 01–05 into the hand-off itself for the narrow first-slice scope (root entity + CRUD only). PropertyUnit + ownership log + advanced features are deferred to follow-up hand-offs.
**Estimated cost:** ~3–5 hours sunfish-PM (small block; no novel primitives; mirrors existing `blocks-rent-collection` shape)
**Pipeline:** `sunfish-feature-change`

---

## Context (one paragraph)

The property-operations cluster needs `Property` as its root entity — every other domain (Assets, Inspections, Leases, Work Orders, Receipts, Public Listings, Owner Cockpit) FK to Property. Without it, those modules have nothing to attach to and the cluster cannot advance. Properties is the cleanest first-slice: a single entity, no upstream blockers (depends only on `foundation-multitenancy` + `foundation-persistence`, both shipping), no dependencies on Proposed-but-not-yet-Accepted ADRs (0051, 0052, 0053). This hand-off scopes the first slice to: scaffold the package + ship the `Property` entity + basic CRUD via `ISunfishEntityModule` + kitchen-sink demo. PropertyUnit (multi-unit modeling) and `PropertyOwnershipRecord` event log are explicitly **deferred to subsequent hand-offs** — they have open architectural questions (cluster intake OQ-P1, OQ-P2, OQ-P3) better resolved after the root entity is real and exercising the foundation patterns.

---

## Phases (binary gates)

### Phase 1 — Scaffold `packages/blocks-properties/`

**Files:**

- **NEW** `packages/blocks-properties/Sunfish.Blocks.Properties.csproj`
  - Mirror `packages/blocks-rent-collection/Sunfish.Blocks.RentCollection.csproj` patterns
  - References: `Sunfish.Foundation` (existing), `Sunfish.Foundation.MultiTenancy` (existing), `Sunfish.Foundation.Persistence` (existing), `Sunfish.Foundation.Catalog` (existing if other blocks consume — verify)
  - `<TargetFramework>` matches existing blocks
  - `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` per repo defaults
- **NEW** `packages/blocks-properties/AssemblyInfo.cs` — empty placeholder if other blocks have one; skip otherwise
- **NEW** Add the project to `Sunfish.slnx` under `/blocks/properties/` (mirror `/blocks/rent-collection/` structure)

**PASS gate:** `dotnet build` succeeds; `dotnet test` (no tests yet — empty project) returns 0; provider-neutrality analyzer passes (no vendor SDK references).

### Phase 2 — `PropertyId` + `Property` entity types

**Files:**

- **NEW** `packages/blocks-properties/Models/PropertyId.cs`
  - Mirror `packages/blocks-rent-collection/Models/PaymentId.cs` exactly: opaque `record struct` with implicit string conversion, JSON converter, `NewId()` factory backed by `Guid`
  - Public sealed record struct `PropertyId(string Value)`
  - `internal sealed class PropertyIdJsonConverter : JsonConverter<PropertyId>` mirroring the Payment one
- **NEW** `packages/blocks-properties/Models/Property.cs`
  - `public sealed record Property(...)` with the following fields:

```csharp
public sealed record Property
{
    public required PropertyId Id { get; init; }
    public required TenantId Tenant { get; init; }                      // foundation-multitenancy
    public required string DisplayName { get; init; }                   // e.g., "123 Main St"
    public required PostalAddress Address { get; init; }                // see Phase 3
    public string? ParcelNumber { get; init; }                          // APN / parcel ID; nullable for jurisdictions w/o
    public required PropertyKind Kind { get; init; }                    // SingleFamily | MultiUnit | Mixed | Land
    public Money? AcquisitionCost { get; init; }                        // ADR 0051 Money type when Accepted; bare decimal placeholder if blocked
    public DateTimeOffset? AcquiredAt { get; init; }
    public int? YearBuilt { get; init; }
    public decimal? TotalSquareFeet { get; init; }
    public int? TotalBedrooms { get; init; }                            // sum across units for MultiUnit
    public decimal? TotalBathrooms { get; init; }                       // can be 1.5
    public string? Notes { get; init; }
    public string? PrimaryPhotoBlobRef { get; init; }                   // FK into existing blob storage (when ready)
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DisposedAt { get; init; }                    // soft-delete on disposition
    public string? DisposalReason { get; init; }                        // free-text
}

public enum PropertyKind { SingleFamily, MultiUnit, Mixed, Land }
```

**Money type note:** ADR 0051 (Foundation.Integrations.Payments) defines `Money` as a record struct (Proposed, PR #203 merged but Status: Proposed not yet Accepted). For this hand-off, **use `decimal? AcquisitionCost` as a placeholder** if `Money` is not yet importable; convert to `Money?` in a one-line follow-up commit when ADR 0051 is Accepted. Add a `// TODO: Money — gated on ADR 0051 acceptance` comment.

**PASS gate:** Both files compile; XML doc on every public member; no null-warning suppressions; one round-trip JSON serialize/deserialize test for `PropertyId`.

### Phase 3 — `PostalAddress` value object

**Files:**

- **NEW** `packages/blocks-properties/Models/PostalAddress.cs`

```csharp
public sealed record PostalAddress
{
    public required string Line1 { get; init; }
    public string? Line2 { get; init; }
    public required string City { get; init; }
    public required string Region { get; init; }                        // US: state code (e.g., "CA"); int'l: free-form region
    public required string PostalCode { get; init; }                    // not normalized; locale-specific
    public required string CountryCode { get; init; }                   // ISO 3166-1 alpha-2 (e.g., "US")
    public double? Latitude { get; init; }                              // optional; for showings + mapping
    public double? Longitude { get; init; }
}
```

No GeoJSON polygon support in first-slice (cluster intake OQ-P2; deferred).

**PASS gate:** Compiles; XML doc; one round-trip JSON test.

### Phase 4 — `ISunfishEntityModule` registration + in-memory CRUD repository

**Files:**

- **NEW** `packages/blocks-properties/PropertyEntityModule.cs`
  - Implements `Sunfish.Foundation.Persistence.ISunfishEntityModule` per ADR 0015
  - Registers `Property` entity with the foundation-persistence registry
  - Mirror existing patterns in `blocks-rent-collection` (likely `RentCollectionEntityModule.cs` or similar — verify name)
- **NEW** `packages/blocks-properties/IPropertyRepository.cs` — domain repository contract
  - `Task<Property?> GetByIdAsync(TenantId tenant, PropertyId id, CancellationToken ct);`
  - `Task<IReadOnlyList<Property>> ListByTenantAsync(TenantId tenant, CancellationToken ct);`  // includes disposed by default? recommend false; add `bool includeDisposed = false` flag
  - `Task UpsertAsync(Property property, CancellationToken ct);`
  - `Task SoftDeleteAsync(TenantId tenant, PropertyId id, string reason, DateTimeOffset disposedAt, CancellationToken ct);`
- **NEW** `packages/blocks-properties/InMemoryPropertyRepository.cs`
  - Default in-memory implementation for tests + demos
  - Thread-safe via `ConcurrentDictionary<(TenantId, PropertyId), Property>`
  - `SoftDeleteAsync` updates `DisposedAt` + `DisposalReason`; doesn't actually remove
- **NEW** `packages/blocks-properties/ServiceCollectionExtensions.cs`
  - `AddPropertyBlock(this IServiceCollection services)` — registers `IPropertyRepository → InMemoryPropertyRepository` + the entity module
  - Mirror `packages/blocks-rent-collection/` ServiceCollectionExtensions if present

**PASS gate:** All files compile; `AddPropertyBlock` resolves the repository at runtime; `ISunfishEntityModule` registration is picked up by foundation-persistence catalog query.

### Phase 5 — Tests + kitchen-sink demo seed

**Files:**

- **NEW** `packages/blocks-properties/tests/Sunfish.Blocks.Properties.Tests.csproj` (mirror `blocks-rent-collection/tests/`)
- **NEW** `packages/blocks-properties/tests/PropertyTests.cs` — basic record equality + JSON round-trip
- **NEW** `packages/blocks-properties/tests/InMemoryPropertyRepositoryTests.cs` — Get/List/Upsert/SoftDelete round-trip; tenant isolation (cross-tenant query returns empty); soft-delete preserves record but excludes from default List
- **NEW** seed data in `apps/kitchen-sink/` (mirror existing block seeds — verify path)
  - 2 properties: one `SingleFamily` (123 Main St), one `MultiUnit` (456 Oak Ave; bedroom/bathroom totals nonzero)
  - Tenant: use existing seed tenant; add a second seed tenant if cluster needs cross-tenant demo (verify with existing seed pattern)

**PASS gate:** `dotnet test packages/blocks-properties/tests/` returns 0 failures; `apps/kitchen-sink/` boots and the 2 seed properties render in the existing block-listing UI (verify what's there; if no UI exists yet, log seed insertion via existing logging).

### Phase 6 — Documentation

**Files:**

- **NEW** `apps/docs/blocks/properties.md` (mirror existing `apps/docs/blocks/<other-block>.md` if pattern exists)
  - One-paragraph block summary
  - Field reference table for `Property`
  - "What's not in this slice" — explicit deferred list (PropertyUnit, ownership log, multi-unit modeling)
  - Cross-link to cluster intake at `icm/00_intake/output/property-properties-intake-2026-04-28.md`

**PASS gate:** apps/docs builds without warnings; new doc page renders in the docs nav.

### Phase 7 — Workstream ledger flip

**Files:**

- **EDIT** `icm/_state/active-workstreams.md` row #17 (Properties domain)
  - Status: `ready-to-build` → `built` (merged)
  - Reference: add the merged PR link
  - Notes: append "First-slice (Property entity + CRUD) shipped. PropertyUnit + ownership log queued as separate hand-offs."
- **EDIT** Last-updated section: append a research-session "(later still)" line OR a sunfish-PM line — sunfish-PM's session per protocol

**PASS gate:** Ledger updated; PR ready to merge.

---

## Out of scope (explicit deferred to follow-up hand-offs)

The cluster intake names PropertyUnit + PropertyOwnershipRecord + cross-tenant ownership decision as in-scope for the *intake*. This first-slice hand-off explicitly defers them:

- **PropertyUnit child entity** — unit-level fields (unit number, sqft, beds/baths, current lease ref). Open question: whether to model as separate entity (recommended) vs flattened JSON column (intake OQ-P1). **Defer to hand-off #2 after first-slice ships.**
- **PropertyOwnershipRecord event log** — acquisition / disposition / refinance / transfer events; immutable; sourced for tax basis. **Defer to hand-off #3** after `kernel-audit` substrate Tier 2 retrofit lands (which is gated on workstream #1 multi-tenancy types).
- **Multi-tenant ownership question** — whether holding-co LLC tenant has cross-tenant read into child LLC tenants' properties (intake OQ-P3). **Resolves alongside workstream #1** (`tenant-id-sentinel-pattern-intake-2026-04-28.md`); not a Properties-block question.
- **GeoJSON polygon for parcel boundary** — intake OQ-P2; defer.
- **Photo storage / blob handling** — `PrimaryPhotoBlobRef` is a string field reserving the FK; the actual blob ingest pipeline is gated on Bridge blob-ingest API spec (cluster cross-cutting OQ3). Field is reserved; usage gated.
- **Migration import tool** (one-shot from BDFL's existing spreadsheet) — Phase 2 onboarding work; separate from this domain block.

---

## What sunfish-PM should NOT touch

- `packages/blocks-rent-collection/` (existing; unrelated to this hand-off)
- `packages/foundation-multitenancy/` (consumer only; no changes here)
- `packages/foundation-persistence/` (consumer only; no changes here)
- `accelerators/anchor/` or `accelerators/bridge/` (consumer integration is a follow-up; first-slice doesn't wire UI)
- ADR documents (Properties doesn't require new or amended ADRs at this slice; ADR 0015 + ADR 0008 cover the registration patterns)

---

## Open questions sunfish-PM should flag back to research

If any of these surface during build, halt and write a memory note + comment on the PR:

1. **Naming collision.** If `Property` is too generic and conflicts with existing types (e.g., property metadata, reflection scenarios), surface for naming guidance — likely `RealEstateProperty` or namespace-qualified.
2. **Money type readiness.** If ADR 0051 is Accepted and `Sunfish.Foundation.Integrations.Payments.Money` is importable, use it directly. If still Proposed-but-not-Accepted, use `decimal?` placeholder per the field-list note. If Accepted mid-build, flag for one-line follow-up.
3. **kitchen-sink seed pattern.** If `apps/kitchen-sink/` doesn't have an obvious place to add seed data, or if the existing pattern requires research-session input on tenant scoping, surface.
4. **Documentation pattern.** If `apps/docs/blocks/<block>.md` doesn't exist as a pattern, default to creating one + adding a nav link in the docs config; flag if unclear.
5. **Blob-ref string vs typed FK.** `PrimaryPhotoBlobRef` is `string?` for now — if existing blob substrate has a typed `BlobRef` value object, use that instead and flag.

---

## Acceptance criteria (research-session sign-off on the PR)

- [ ] All 7 phases complete with their PASS gates green
- [ ] `dotnet build` + `dotnet test` repo-wide green
- [ ] Provider-neutrality analyzer (`SUNFISH_PROVNEUT_001`) passes on `blocks-properties` (no vendor SDK references; build error if violated)
- [ ] kitchen-sink demo seed renders 2 sample properties
- [ ] `apps/docs/blocks/properties.md` exists with the deferred-list
- [ ] Workstream #17 ledger row flipped to `built` (merged) with PR link
- [ ] PR description names Phase 1 (this hand-off) as the slice scope; flags PropertyUnit + ownership log as deferred to follow-up hand-offs
- [ ] No code outside `packages/blocks-properties/`, `Sunfish.slnx`, `apps/kitchen-sink/<seed>`, `apps/docs/blocks/properties.md`, `icm/_state/active-workstreams.md` is touched

---

## After this hand-off ships

Research session will write the next two hand-offs to keep the queue full:

- `property-properties-unit-entity-handoff.md` — PropertyUnit child entity (multi-unit modeling)
- `property-properties-ownership-log-handoff.md` — PropertyOwnershipRecord event log (gated on kernel-audit Tier 2 retrofit)

Plus other cluster-intake first-slices in parallel (Assets is the next-cleanest candidate; depends only on already-Accepted ADRs 0015 + 0049).

---

## Sign-off

Research session — 2026-04-28
