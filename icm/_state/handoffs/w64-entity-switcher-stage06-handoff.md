# Hand-off — W#64 ERPNext Company ↔ Sunfish Team Context Binding (entity-switcher)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build`
**Workstream:** W#64 — ERPNext Company ↔ Sunfish Team Context Binding (multi-entity cockpit)
**Spec source:** This hand-off + `icm/_state/active-workstreams.md` row W#64
**ADR:** [ADR 0032 Multi-Team Anchor (Slack-Style Workspace Switching)](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) (Accepted 2026-04-23) — substrate this hand-off composes onto
**Ratifications:** `coordination/inbox/xo-question-2026-05-16T16-05Z-w64-entity-switcher-option.md` — CO ratified **Option A — Blocks-first** on 2026-05-16
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~6–8h sunfish-PM (schema extension + EF migration + Bridge endpoint filter + React switcher rewire + ~25–30 tests)
**PR count:** 4 PRs (PRs 1–3 mandatory; PR 4 optional — see §PR sequence)
**Pre-merge council:** NOT required (additive schema extension; no API-break; mirrors the substrate-only pattern). Standard COB self-audit applies.
**Audit before build:** `ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-prop"` — confirms `blocks-properties/` is the canonical home for `Property` (siblings `blocks-property-equipment/` + `blocks-property-leasing-pipeline/` are derivative).

---

## Context

### What CO needs

CO operates 4 LLC properties:

- **Acero Properties LLC**
- **Bosco Properties LLC**
- **Escola Properties LLC**
- **Shirin Properties LLC**

(Names and addresses are private — see project memory `project_canonical_test_properties.md`; do not commit to public repos.)

The W#60 P2 React cockpit (`apps/anchor-react/`) loads all properties across
all 4 LLCs under one `TenantId`. CO cannot today filter the cockpit by LLC.
W#64 is the entity-switcher that fixes this.

### The audit finding (2026-05-16, origin/main)

A `CompanySwitcher` + `companyStore` (zustand) **already exist** in
`apps/anchor-react/src/` — but they are wired to **two separate dead-ends**:

1. The cockpit endpoint (`/api/v1/cockpit/properties` →
   `accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs` →
   `IPropertyRepository.ListByTenantAsync(tenant, …)`) returns **all
   properties for the tenant, no entity filter**.
2. The React `useProperties` hook (`apps/anchor-react/src/hooks/useProperties.ts`)
   includes `activeCompany` in the React-Query cache key (so cache-busts on
   switch) but calls `getProperties()` from `erpnext.ts` — the ERPNext data
   path, not the cockpit data path. The query string `company=…` is never sent.
3. The `Property` entity in `packages/blocks-properties/Models/Property.cs`
   has no `EntityTag` (or equivalent) field.

The switcher UI renders correctly, the cache invalidates on switch — but
neither the API nor the domain model carries the filter. The result is
**the same property list regardless of selection**.

### Why Option A (Blocks-first) was ratified

CO ratified Option A on 2026-05-16:

> Add `EntityTag: string?` to `Property` in `blocks-properties`. EF
> migration. Cockpit entity-switcher in the React app filters by
> `EntityTag`. Works offline. Reuses ADR 0032 team-switcher substrate as
> a thin adapter pattern.

The alternatives were:

| | Approach | Why rejected |
|---|---|---|
| **B** | ERPNext-first | Loses offline capability until W#60 P3; ERPNext becomes the source-of-truth instead of `blocks-properties`; contradicts ADR 0088 Path II direction (Anchor as all-in-one local-first runtime). |
| **C** | Defer to W#60 P3 | Blocks WS-H (spouse co-ownership) which depends on per-entity capability grants. Postponing W#64 postpones WS-H. |

### Relationship to ADR 0032 substrate

ADR 0032 (Accepted 2026-04-23) shipped the **team-switcher** substrate:
`TeamContext` + `ITeamContextFactory` + `IActiveTeamAccessor` +
`SunfishTeamSwitcher.razor` (Blazor) + `TeamSwitcherPage.razor`. That
substrate handles **teams** (tenants) — one user belonging to multiple
organizations.

W#64's **entity-switcher** is a **sibling** with the same UX shape but
narrower scope:

- Team-switcher (ADR 0032): scope = one user across multiple **tenants**.
  Each tenant has its own SQLCipher DB, event log, gossip daemon.
- Entity-switcher (W#64): scope = one tenant across multiple **LLCs**
  (Acero / Bosco / Escola / Shirin), all of which share a single
  `TenantContext`, single DB, single event log. The switcher filters the
  cockpit view; it does not switch data planes.

These are deliberately separate concerns. A future workstream may merge
them into a single hierarchical switcher (team › entity); **that
consolidation is out of scope here.** The W#64 design pattern (UX
dropdown + LocalStorage-persisted active selection + cache invalidation
on switch) is a thinner sibling and explicitly references ADR 0032 in
its docstrings.

### Downstream gate

**WS-H (spouse co-ownership)** depends on W#64. Spouse capability grants
need entity-by-entity scoping (CO can grant spouse access to Acero +
Bosco but withhold Escola + Shirin). Without an `EntityTag` on `Property`,
the spouse-capability model cannot scope. WS-H is the next gate-clear
once W#64 ships.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify Property entity location + audit shape.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-prop"
   cat /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-properties/Models/Property.cs
   ```
   Expected: `blocks-properties/` exists; `Property` is a sealed record
   with `Id`, `TenantId`, `DisplayName`, `Address`, `Kind`, `CreatedAt`,
   etc. — but **no** `EntityTag`. `blocks-property-equipment/` +
   `blocks-property-leasing-pipeline/` are derivative siblings that FK
   to `Property` and are out of scope.

2. **Read the existing React switcher wiring (origin/main).**
   ```bash
   git show origin/main:apps/anchor-react/src/components/CompanySwitcher.tsx
   git show origin/main:apps/anchor-react/src/stores/companyStore.ts
   git show origin/main:apps/anchor-react/src/hooks/useProperties.ts
   git show origin/main:apps/anchor-react/src/cockpit/api.ts
   ```
   Confirm understanding: `CompanySwitcher` renders a `<select>` over
   `availableCompanies`; `companyStore` is a zustand store with
   `activeCompany` + `availableCompanies` + setters; `useProperties`
   includes `activeCompany` in the cache key but does not pass it to
   the network call. **The hand-off renames this switcher from
   "Company" to "Entity" terminology where it doesn't break the contract**
   (see §Naming, below).

3. **Read the Bridge cockpit endpoint.**
   ```bash
   git show origin/main:accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs
   ```
   Expected: `HandleListPropertiesAsync` calls `properties.ListByTenantAsync(tenant, includeDisposed: false, ct)` —
   no entity filter parameter today. PR 2 extends this to accept an
   optional `entity` query-string parameter.

4. **Confirm ADR 0032 substrate is on main.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0032-multi-team-anchor-workspace-switching.md
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/kernel-runtime/Teams/ 2>/dev/null
   ```
   Expected: status `Accepted` (2026-04-23); `kernel-runtime/Teams/`
   contains `TeamContext`, `ITeamContextFactory`, `IActiveTeamAccessor`,
   etc. If `Teams/` does not exist on the working branch, the ADR 0032
   substrate is not on this branch and the W#64 hand-off cannot proceed —
   file `cob-question-*` (see §Halt conditions).

5. **Confirm no parallel-session PRs touch `blocks-properties/` or
   `apps/anchor-react/src/components/CompanySwitcher.tsx`.**
   ```bash
   gh pr list --state open --search "blocks-properties in:title,body"
   gh pr list --state open --search "CompanySwitcher in:title,body"
   gh pr list --state open --search "EntityTag in:title,body"
   ```
   Expected: empty. If anything is open, file `cob-question-*` before
   starting PR 1.

6. **Check existing migration cadence.**
   ```bash
   ls accelerators/bridge/Sunfish.Bridge.Data/Migrations/ | tail -5
   ```
   PR 1 creates the next migration in this folder. The current naming
   convention is `YYYYMMDD_<DescriptiveName>` (with optional time suffix
   for same-day disambiguation).

7. **Confirm `but status` (or `git status`) is clean** and current branch
   is a fresh worktree from `main`.

---

## Naming convention — Entity, EntityTag, useActiveEntity

This hand-off settles the naming question that surfaced during the audit:

- **Database column / record field:** `EntityTag: string?` on `Property`.
- **Wire / DTO / query-string:** `entity` (lowercase, matches REST query-param
  conventions).
- **React store / hook:** `useActiveEntity()` (new hook) — wraps and supersedes
  the call-site usage of `useCompanyStore.activeCompany`.
- **React component:** `EntitySwitcher` (new file) — wraps the existing
  `CompanySwitcher` UX. The existing `CompanySwitcher` + `companyStore` are
  preserved as the **backing implementation**; the new naming is a thin
  facade so that downstream code reads "entity" (LLC) rather than "company"
  (ERPNext terminology that leaked through W#60 P2). See PR 3 for the
  facade strategy.

**Why preserve `CompanySwitcher` underneath:** the W#60 P2 ERPNext data
path uses `company` as ERPNext's column name. Renaming `CompanySwitcher`
to `EntitySwitcher` outright would force a rename of every test +
import + the `companyStore` zustand store, and risk a merge conflict
with any in-flight W#60 P3 work. Instead, **PR 3 introduces
`EntitySwitcher.tsx` + `useActiveEntity.ts` as a facade**; the underlying
`companyStore` keeps its name (it can be renamed later in a one-line
mechanical commit when W#60 P3 lands).

**Do NOT** in this hand-off:
- Rename `Property.Company` on the ERPNext data path. That field stays as
  the ERPNext wire shape.
- Rename `companyStore` / `useCompanyStore`. That's a follow-on cleanup
  outside this hand-off.
- Add a `Company` (or `LegalEntity`) **entity** to `blocks-properties`.
  The Stage 02 spec is `EntityTag: string?` only — see §Cross-cluster
  touches for the future-direction note on Party-model migration.

---

## Per-PR deliverables

This hand-off splits into **4 PRs**. PRs 1 + 2 + 3 are sequential
(PR 2 depends on PR 1's schema; PR 3 depends on PR 2's endpoint).
**PR 4 is optional** — the ERPNext importer wiring; defer if
prioritization shifts toward W#60 P4 or WS-H.

---

### PR 1 — `Property.EntityTag` schema extension + migration + repository filter

**Estimated effort:** ~2–3h
**Scope:** add `EntityTag: string?` to `Property`; EF migration; update
EF configuration; extend `IPropertyRepository` with a filtered list overload;
update `InMemoryPropertyRepository`; ~10 tests
**Commit subject:** `feat(blocks-properties): add EntityTag to Property + repository filter overload`
**Branch:** `cob/w64-property-entity-tag`
**Depends on:** nothing

#### File changes

**`packages/blocks-properties/Models/Property.cs`** — add the field:

```csharp
/// <summary>
/// Free-text legal-entity tag (typically the LLC name — e.g.
/// "Acero Properties LLC"). Nullable for properties not yet assigned
/// to an entity; CockpitEndpoints filters on this when an `entity`
/// query-string parameter is provided. CRDT semantics: last-writer-wins
/// is acceptable for this free-text tag — not state-machine territory.
/// </summary>
/// <remarks>
/// Long-term, this should become an FK to a Party (LLC) entity in
/// blocks-people-* per `_shared/engineering/party-model-convention.md`
/// §3. The string form is the Phase 1 / W#64 expedient; migration to
/// FK-to-Party is tracked as an open question on this hand-off (see
/// §Open questions). Do NOT pre-introduce the FK in this PR.
/// </remarks>
public string? EntityTag { get; init; }
```

Place the field **alphabetically after `DisposedAt`/`DisposalReason`** OR
**immediately after `Kind`** (whichever the existing convention prefers —
the file currently groups required fields first, then optional fields).
COB's judgment call; document the placement choice in the PR description.

**`packages/blocks-properties/Data/PropertyEntityConfiguration.cs`** —
add column mapping + index:

```csharp
builder.Property(x => x.EntityTag)
    .HasMaxLength(128);

builder.HasIndex(x => new { x.TenantId, x.EntityTag })
    .HasDatabaseName("ix_properties_property_tenant_entity_tag");
```

The `(TenantId, EntityTag)` composite index is critical for the
cockpit's filtered list (PR 2). The index is non-unique (multiple
properties per entity tag is the common case).

**`packages/blocks-properties/Services/IPropertyRepository.cs`** — add
a filtered list overload:

```csharp
/// <summary>
/// Lists properties owned by the tenant, optionally filtered by
/// <see cref="Property.EntityTag"/>. When <paramref name="entityTag"/>
/// is non-null, returns only properties whose tag equals the argument
/// (case-sensitive — entity tags are administrator-provided strings,
/// not user-entered free text). When null, behavior matches
/// <see cref="ListByTenantAsync(TenantId, bool, CancellationToken)"/>.
/// </summary>
Task<IReadOnlyList<Property>> ListByTenantAsync(
    TenantId tenant,
    string? entityTag,
    bool includeDisposed = false,
    CancellationToken cancellationToken = default);
```

**Do NOT replace** the existing `ListByTenantAsync(TenantId, bool, …)`
overload. Both overloads coexist — the new one is the filtered variant;
the old one stays for un-filtered call sites. **Both** must have working
implementations on `InMemoryPropertyRepository`.

**`packages/blocks-properties/Services/InMemoryPropertyRepository.cs`** —
implement the new overload:

```csharp
public Task<IReadOnlyList<Property>> ListByTenantAsync(
    TenantId tenant,
    string? entityTag,
    bool includeDisposed = false,
    CancellationToken cancellationToken = default)
{
    var query = _store
        .Where(kvp => kvp.Key.Tenant.Equals(tenant))
        .Select(kvp => kvp.Value);

    if (!includeDisposed)
    {
        query = query.Where(p => p.DisposedAt is null);
    }

    if (entityTag is not null)
    {
        query = query.Where(p => string.Equals(p.EntityTag, entityTag, StringComparison.Ordinal));
    }

    IReadOnlyList<Property> result = query.ToList();
    return Task.FromResult(result);
}
```

The existing 2-argument overload should delegate to the new one
(`return ListByTenantAsync(tenant, entityTag: null, includeDisposed, ct);`) —
keeps a single source of truth.

**`accelerators/bridge/Sunfish.Bridge.Data/Migrations/YYYYMMDDHHMMSS_AddEntityTagToProperty.cs`** —
EF migration. Generate via:

```bash
cd accelerators/bridge/Sunfish.Bridge.Data
dotnet ef migrations add AddEntityTagToProperty \
  --context SunfishBridgeDbContext \
  --output-dir Migrations
```

Verify the generated migration:
- `Up()` adds `EntityTag` column (`nvarchar(128) NULL`) to
  `properties_property` table.
- `Up()` adds `ix_properties_property_tenant_entity_tag` index on
  `(TenantId, EntityTag)`.
- `Down()` drops the index then the column.
- `SunfishBridgeDbContextModelSnapshot.cs` is updated.

**Do NOT** edit the migration file by hand beyond adjusting cosmetic
issues (XML doc, formatting). The migration is generated by `dotnet ef`
and must round-trip.

#### Tests (`packages/blocks-properties/tests/`)

Add these to `PropertyTests.cs` (or a new `PropertyEntityTagTests.cs` —
COB's call):

1. `Property_EntityTag_DefaultsToNull` — a Property constructed without
   `EntityTag` has `EntityTag == null`.
2. `Property_EntityTag_RoundtripsThroughWith` — `property with { EntityTag = "Acero Properties LLC" }`
   preserves the value.
3. `InMemoryPropertyRepository_ListByTenantAsync_NoFilter_ReturnsAllProperties` —
   regression test for the 2-arg overload.
4. `InMemoryPropertyRepository_ListByTenantAsync_WithEntityFilter_ReturnsMatchingOnly` —
   seed 3 properties (2 with `EntityTag="Acero"`, 1 with `EntityTag="Bosco"`); call with `entityTag: "Acero"`; expect 2.
5. `InMemoryPropertyRepository_ListByTenantAsync_WithEntityFilter_NullArgIsTreatedAsUnfiltered` —
   call with `entityTag: null`; expect all 3 from test (4)'s seed.
6. `InMemoryPropertyRepository_ListByTenantAsync_WithEntityFilter_IsCaseSensitive` —
   seed with `EntityTag="Acero"`; query with `entityTag: "acero"`; expect 0.
7. `InMemoryPropertyRepository_ListByTenantAsync_WithEntityFilter_ExcludesDisposedByDefault` —
   seed 1 disposed property with the queried entity tag; expect it excluded.
8. `InMemoryPropertyRepository_ListByTenantAsync_WithEntityFilter_IncludesDisposedWhenAsked` —
   same as (7) but with `includeDisposed: true`; expect 1.
9. `InMemoryPropertyRepository_ListByTenantAsync_WithEntityFilter_ScopesByTenant` —
   seed 2 properties in TenantA + 1 in TenantB with the same EntityTag; query TenantA; expect 2.
10. `PropertyEntityConfiguration_EntityTag_HasIndex` — reflection-based
    test (if your existing test conventions use one) confirming the
    `(TenantId, EntityTag)` index is configured on the entity. Skip if
    the existing test conventions do not have a reflection-based pattern;
    the migration round-trip in PR 2's endpoint tests covers the index
    indirectly.

#### Verification

- `dotnet build packages/blocks-properties/` succeeds.
- `dotnet test packages/blocks-properties/tests/` — all green (~10 new tests + existing tests preserved).
- `dotnet ef migrations script --context SunfishBridgeDbContext` produces a clean SQL diff (no orphan columns).
- `grep -r "EntityTag" packages/blocks-properties/` — confirms the field
  is referenced in `Property.cs`, `PropertyEntityConfiguration.cs`,
  `IPropertyRepository.cs`, `InMemoryPropertyRepository.cs`, and tests.

#### PR description template

```
Add nullable `EntityTag: string?` to `Property` in `blocks-properties` to
support per-LLC cockpit filtering for W#64.

Per CO ratification of Option A (Blocks-first) on 2026-05-16, this
ships the domain-side schema extension. PR 2 surfaces the field through
the Bridge cockpit endpoint; PR 3 wires the React entity-switcher.

- `Property.EntityTag` — nullable string, max 128 chars
- `(TenantId, EntityTag)` composite index for filtered lookups
- `IPropertyRepository.ListByTenantAsync(tenant, entityTag, …)` overload
- `InMemoryPropertyRepository` implements both overloads
- EF migration `AddEntityTagToProperty` (Up + Down + model snapshot)
- ~10 new tests

CRDT semantics: last-writer-wins; not state-machine territory.
Long-term: migrate to FK-to-Party per party-model-convention.md (open).

Refs: W#64 hand-off; xo-question-2026-05-16T16-05Z-w64-entity-switcher-option.md
```

#### Do NOT in this PR

- Do NOT add a `LegalEntity` or `Party` entity. The expedient is a string.
- Do NOT touch `CockpitEndpoints.cs` or the cockpit DTOs — that's PR 2.
- Do NOT touch any React code. That's PR 3.
- Do NOT make `EntityTag` required. Nullable is mandatory; existing
  properties get NULL on migration and remain valid.
- Do NOT make `EntityTag` case-insensitive in the SQL index. Entity
  tags are administrator-provided strings; case-sensitive equality is
  the simpler contract.

---

### PR 2 — Bridge cockpit endpoint `?entity=` filter + DTO + endpoint tests

**Estimated effort:** ~2h
**Scope:** add optional `entity` query parameter to
`/api/v1/cockpit/properties`; thread through to `IPropertyRepository.ListByTenantAsync(…, entityTag, …)`;
add an entity-distinct endpoint for switcher population;
~8 endpoint tests
**Commit subject:** `feat(bridge): cockpit entity filter + entities endpoint for W#64`
**Depends on:** PR 1 merged
**Branch:** `cob/w64-bridge-cockpit-entity-filter`

#### File changes

**`accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs`** —
extend `HandleListPropertiesAsync` and add a new entities-list endpoint:

```csharp
public static IEndpointRouteBuilder MapCockpitEndpoints(this IEndpointRouteBuilder app)
{
    ArgumentNullException.ThrowIfNull(app);
    var group = app.MapGroup("/api/v1/cockpit").RequireAuthorization(CockpitPolicyName);
    group.MapGet("/properties", HandleListPropertiesAsync).WithName("CockpitListProperties");
    group.MapGet("/entities", HandleListEntitiesAsync).WithName("CockpitListEntities");  // NEW
    group.MapPropertyDetail();
    group.MapWorkOrders();
    group.MapVendors();
    group.MapDashboard();
    return app;
}

internal static async Task<Ok<PropertySelectorListDto>> HandleListPropertiesAsync(
    ITenantContext tenantContext,
    IPropertyRepository properties,
    [FromQuery] string? entity,                          // NEW
    CancellationToken ct)
{
    TenantId tenant = tenantContext.TenantId;
    var rows = await properties
        .ListByTenantAsync(tenant, entityTag: entity, includeDisposed: false, ct)
        .ConfigureAwait(false);

    var items = rows
        .Select(p => new PropertySelectorItemDto(
            p.Id.Value,
            p.DisplayName,
            p.Kind.ToString(),
            p.Address.City,
            p.Address.Region,
            p.EntityTag))                                  // NEW DTO field
        .ToArray();

    return TypedResults.Ok(new PropertySelectorListDto(items));
}

/// <summary>
/// W#64 — returns the list of distinct entity tags currently in use across
/// the tenant's properties. The React entity-switcher populates its dropdown
/// from this endpoint. Properties with `EntityTag == null` are excluded
/// (they show up under the "All entities" / no-filter view).
/// </summary>
internal static async Task<Ok<EntityListDto>> HandleListEntitiesAsync(
    ITenantContext tenantContext,
    IPropertyRepository properties,
    CancellationToken ct)
{
    TenantId tenant = tenantContext.TenantId;
    var rows = await properties
        .ListByTenantAsync(tenant, entityTag: null, includeDisposed: false, ct)
        .ConfigureAwait(false);

    var entities = rows
        .Select(p => p.EntityTag)
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(tag => tag, StringComparer.Ordinal)
        .ToArray();

    return TypedResults.Ok(new EntityListDto(entities!));
}
```

Update the DTO at the bottom of the file:

```csharp
/// <summary>Wire-format envelope for the property-selector endpoint.</summary>
public record PropertySelectorListDto(IReadOnlyList<PropertySelectorItemDto> Properties);

/// <summary>
/// One row in the property-selector list. <c>Region</c> is the
/// state/province per <see cref="Sunfish.Blocks.Properties.Models.PostalAddress.Region"/>.
/// <c>EntityTag</c> is the LLC tag (W#64) — null when the property has not been
/// assigned to an entity.
/// </summary>
public record PropertySelectorItemDto(
    string PropertyId,
    string DisplayName,
    string Kind,
    string City,
    string Region,
    string? EntityTag);

/// <summary>Wire-format envelope for the W#64 entities-list endpoint.</summary>
public record EntityListDto(IReadOnlyList<string> Entities);
```

**Backwards compatibility:** Adding `EntityTag` to `PropertySelectorItemDto`
is **additive** (consumers parsing the JSON ignore unknown fields by
default in TypeScript). The new `entity` query parameter is **optional**;
omitting it preserves the existing un-filtered behavior. Both shapes are
non-breaking.

#### Tests (`accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/Cockpit/CockpitEndpointsTests.cs`)

Mirror the existing fact-based test pattern in this file. New tests:

1. `HandleListPropertiesAsync_NoEntityFilter_ReturnsAllTenantProperties` —
   seed 3 properties under the tenant (2 with `EntityTag="Acero"`, 1 with
   `EntityTag="Bosco"`); call without `entity`; expect 3.
2. `HandleListPropertiesAsync_WithEntityFilter_ReturnsMatchingOnly` —
   same seed; call with `entity="Acero"`; expect 2.
3. `HandleListPropertiesAsync_WithEntityFilter_ReturnsEmptyWhenNoMatch` —
   call with `entity="Nonexistent LLC"`; expect 0; HTTP 200 with empty
   list (NOT 404).
4. `HandleListPropertiesAsync_WithEntityFilter_IsCaseSensitive` —
   call with `entity="acero"`; expect 0 even though `Acero` exists.
5. `HandleListPropertiesAsync_WithEntityFilter_ScopesByTenant` —
   seed properties in TenantA + TenantB with the same EntityTag; request
   as TenantA; expect only TenantA's matches (no cross-tenant leakage).
6. `HandleListPropertiesAsync_DtoIncludesEntityTagField` — confirm the
   serialized DTO carries the `EntityTag` field (verify with a property
   that has a non-null tag; verify with a property that has a null tag —
   both should round-trip correctly).
7. `HandleListEntitiesAsync_ReturnsDistinctEntityTags` — seed 4 properties
   (2 Acero, 1 Bosco, 1 with NULL); expect `["Acero", "Bosco"]` in
   sorted order; expect NULL excluded.
8. `HandleListEntitiesAsync_ScopesByTenant` — TenantA has Acero + Bosco;
   TenantB has Escola; request as TenantA; expect only `["Acero", "Bosco"]`.

#### Verification

- `dotnet build accelerators/bridge/Sunfish.Bridge/` succeeds.
- `dotnet test accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/` — all green (~8 new tests).
- Manual curl smoke test (against a Bridge instance with the migration
  applied):
  ```bash
  curl -s --cookie "session=…" 'https://localhost:5001/api/v1/cockpit/properties?entity=Acero%20Properties%20LLC' | jq .
  curl -s --cookie "session=…" 'https://localhost:5001/api/v1/cockpit/entities' | jq .
  ```

#### PR description template

```
Add `?entity=` query parameter to `/api/v1/cockpit/properties` and a new
`/api/v1/cockpit/entities` endpoint for W#64 entity-switcher.

PR 1 added `Property.EntityTag` + repository overload. This PR surfaces
the field through the Bridge cockpit so the React app can filter and
populate its switcher dropdown.

- `GET /api/v1/cockpit/properties?entity=Acero%20Properties%20LLC` filters
  the property list to only that LLC's properties. Omitting `entity`
  preserves the existing un-filtered behavior.
- `GET /api/v1/cockpit/entities` returns the distinct entity-tag list
  for the tenant, sorted ordinal, NULLs excluded.
- `PropertySelectorItemDto.EntityTag: string?` added (additive; non-breaking).
- ~8 endpoint tests across both endpoints.

Refs: W#64 hand-off; ADR 0032 (substrate); xo-question-2026-05-16T16-05Z.
```

#### Do NOT in this PR

- Do NOT add `EntityTag` to the property-detail / dashboard / work-order /
  vendor endpoints. Those are out of scope; the filtering happens at the
  list level only.
- Do NOT change the auth policy (`CockpitPolicy`). Entity-level capability
  scoping (e.g., spouse can see Acero + Bosco only) is **WS-H** and is
  explicitly NOT in this hand-off.
- Do NOT introduce server-side rate limiting on `?entity=…` queries. The
  same `IPropertyRepository.ListByTenantAsync` indexes the request; cost
  is bounded by the existing tenant-scope index.
- Do NOT add pagination to the entities-list endpoint. Tenant entity
  counts are <100 in the foreseeable horizon; sorted ordinal list is the
  right shape. Pagination if needed becomes a follow-on.

---

### PR 3 — React `EntitySwitcher` + `useActiveEntity` hook + page-level filter wiring

**Estimated effort:** ~2–3h
**Scope:** new `EntitySwitcher.tsx` (facade over existing `CompanySwitcher`);
new `useActiveEntity.ts` hook (facade over `useCompanyStore`); LocalStorage
persistence; rewire `useProperties` + `useLeases` to call cockpit endpoints
with `entity=…`; populate switcher from `/api/v1/cockpit/entities`;
~7 component + hook tests
**Commit subject:** `feat(anchor-react): wire entity-switcher to cockpit endpoints + LocalStorage persistence`
**Depends on:** PR 2 merged
**Branch:** `cob/w64-react-entity-switcher`

#### File changes

**New: `apps/anchor-react/src/hooks/useActiveEntity.ts`** — facade hook over
`useCompanyStore`. Reads/writes through the existing zustand store but
exposes the W#64 terminology + handles LocalStorage persistence:

```typescript
import { useEffect } from 'react'
import { useCompanyStore } from '@/stores/companyStore'

const LOCAL_STORAGE_KEY = 'sunfish.cockpit.activeEntity'

/**
 * W#64 — active-entity facade over the existing `companyStore`.
 *
 * Returns the active LLC tag (e.g., "Acero Properties LLC"), the
 * setter (which also persists to LocalStorage), and the list of
 * available entities. The empty string represents "all entities"
 * (no filter applied).
 *
 * Persistence: LocalStorage-backed. On first call, hydrates from
 * `sunfish.cockpit.activeEntity`. On set, writes back. Survives page
 * reload but not data clear.
 *
 * Naming: this is a thin facade so call sites read in W#64
 * terminology ("entity") rather than the underlying ERPNext-derived
 * "company" terminology. The backing store retains its existing name
 * to avoid a merge conflict with in-flight W#60 P3 work.
 *
 * Composition note: this is a sibling pattern to ADR 0032's
 * `IActiveTeamAccessor` — same UX shape, narrower scope (entities
 * within a team vs. teams themselves).
 */
export function useActiveEntity() {
  const activeEntity = useCompanyStore((s) => s.activeCompany)
  const availableEntities = useCompanyStore((s) => s.availableCompanies)
  const setActiveCompany = useCompanyStore((s) => s.setActiveCompany)
  const setAvailableCompanies = useCompanyStore((s) => s.setAvailableCompanies)

  // Hydrate from LocalStorage on first mount (idempotent — safe to run
  // every render; only writes if different).
  useEffect(() => {
    try {
      const persisted = window.localStorage.getItem(LOCAL_STORAGE_KEY)
      if (persisted !== null && persisted !== activeEntity) {
        setActiveCompany(persisted)
      }
    } catch {
      // LocalStorage unavailable (private browsing, SSR) — no-op.
    }
  }, [activeEntity, setActiveCompany])

  function setActiveEntity(entity: string) {
    setActiveCompany(entity)
    try {
      window.localStorage.setItem(LOCAL_STORAGE_KEY, entity)
    } catch {
      // LocalStorage unavailable — accept the in-memory state only.
    }
  }

  return { activeEntity, availableEntities, setActiveEntity, setAvailableEntities: setAvailableCompanies }
}
```

**New: `apps/anchor-react/src/components/EntitySwitcher.tsx`** — facade
component over `CompanySwitcher` with the same UX but the new label:

```tsx
import { useActiveEntity } from '@/hooks/useActiveEntity'
import { useQueryClient } from '@tanstack/react-query'

/**
 * W#64 — entity-switcher dropdown for the cockpit header. Renders the
 * tenant's distinct entity tags (LLCs) as a `<select>`; switching
 * invalidates React-Query so the cockpit re-fetches against the new
 * entity filter.
 *
 * Renders nothing if the tenant has 0 or 1 entities (no choice to make).
 * The "All entities" pseudo-option (empty string) is always available
 * as the un-filtered view.
 *
 * Composition note: ADR 0032 sibling — same UX as `SunfishTeamSwitcher.razor`
 * but scoped to entities within a single tenant.
 */
export function EntitySwitcher() {
  const { activeEntity, availableEntities, setActiveEntity } = useActiveEntity()
  const queryClient = useQueryClient()

  if (availableEntities.length <= 1) return null

  function handleChange(e: React.ChangeEvent<HTMLSelectElement>) {
    setActiveEntity(e.target.value)
    void queryClient.invalidateQueries()
  }

  return (
    <select
      value={activeEntity}
      onChange={handleChange}
      className="rounded border border-gray-300 bg-white px-3 py-1.5 text-sm"
      aria-label="Active entity"
    >
      <option value="">All entities</option>
      {availableEntities.map((tag) => (
        <option key={tag} value={tag}>
          {tag}
        </option>
      ))}
    </select>
  )
}
```

**Update: `apps/anchor-react/src/app.tsx`** — replace `<CompanySwitcher />`
with `<EntitySwitcher />`. Replace the `useEffect` hydration that calls
`/api/v1/whoami` (which today receives `defaultCompany` +
`availableCompanies` from ERPNext) with a fetch to the new
`/api/v1/cockpit/entities` endpoint. The shape:

```tsx
// In AppLayout's useEffect:
fetch('/api/v1/cockpit/entities', { credentials: 'include' })
  .then((r) => r.json())
  .then((data: { entities?: string[] }) => {
    if (data.entities) setAvailableEntities(data.entities)
  })
  .catch(() => { /* best-effort */ })
```

The whoami call may **also** still happen for auth purposes (role,
user identity) — leave that block untouched; just remove the
`defaultCompany` / `availableCompanies` consumption from it (those came
from the ERPNext data path, which W#64 supersedes for entity purposes).

**Update: `apps/anchor-react/src/cockpit/api.ts`** — add an entity-aware
overload to `getCockpitProperties` and add `listCockpitEntities`:

```typescript
export interface CockpitPropertySummary {
  propertyId: string
  displayName: string
  kind: string
  city: string
  region: string
  entityTag: string | null      // W#64 — null when un-tagged
}

export interface CockpitPropertyList {
  properties: CockpitPropertySummary[]
}

/** Returns the property summary list for the authenticated tenant. */
export async function getCockpitProperties(
  options: { entity?: string } = {},
): Promise<CockpitPropertyList> {
  const qs = new URLSearchParams()
  if (options.entity) qs.set('entity', options.entity)
  const url = `/api/v1/cockpit/properties${qs.size > 0 ? `?${qs}` : ''}`
  const resp = await fetch(url, { credentials: 'include' })
  if (!resp.ok) {
    throw new Error(`Failed to load cockpit properties: ${resp.status} ${resp.statusText}`)
  }
  return (await resp.json()) as CockpitPropertyList
}

/** W#64 — returns the distinct entity tags currently in use. */
export async function listCockpitEntities(): Promise<{ entities: string[] }> {
  const resp = await fetch('/api/v1/cockpit/entities', { credentials: 'include' })
  if (!resp.ok) {
    throw new Error(`Failed to load cockpit entities: ${resp.status} ${resp.statusText}`)
  }
  return (await resp.json()) as { entities: string[] }
}
```

**Update: `apps/anchor-react/src/hooks/useProperties.ts`** — rewrite to
call the cockpit endpoint with the active entity filter, not the ERPNext
data path:

```typescript
import { useQuery } from '@tanstack/react-query'
import { getCockpitProperties } from '@/cockpit/api'
import { useActiveEntity } from '@/hooks/useActiveEntity'

export function useProperties() {
  const { activeEntity } = useActiveEntity()
  return useQuery({
    queryKey: ['cockpit', 'properties', activeEntity],
    queryFn: () => getCockpitProperties({ entity: activeEntity || undefined }),
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}
```

**Note:** This rewires `useProperties` from `getProperties` (`erpnext.ts`)
to `getCockpitProperties` (`cockpit/api.ts`). The component that consumes
`useProperties` (`PropertiesPage.tsx`) currently expects the ERPNext
`Property` shape (`name`, `property_name`, `address_line_1`, etc.). The
cockpit shape is different (`propertyId`, `displayName`, `city`, `region`).
**PropertiesPage.tsx must be updated** to read the cockpit shape — see
next item.

**Update: `apps/anchor-react/src/pages/PropertiesPage.tsx`** — re-target
the JSX from ERPNext `Property` fields to cockpit `CockpitPropertySummary`
fields. The change is mechanical:

| ERPNext field | Cockpit field |
|---|---|
| `p.name` | `p.propertyId` |
| `p.property_name` | `p.displayName` |
| `p.address_line_1, p.city, p.state` | `[p.city, p.region].filter(Boolean).join(', ')` (street line is not in cockpit DTO; either add it as PR 2's responsibility or accept the reduced shape — **accept the reduced shape for W#64**; street line is recoverable via property-detail click-through) |
| `p.units` | not in cockpit DTO; **remove** the units line for now (cockpit detail endpoint surfaces unit counts via the work-order/dashboard view) |
| `p.status` Badge | **remove** for now; cockpit DTO has no status field; if a status indicator is needed, surface from `EntityTag` (show the tag as a Badge) |

The status badge → entity-tag badge swap is a meaningful UX win:

```tsx
<Badge variant="secondary">{p.entityTag ?? 'Unassigned'}</Badge>
```

Same change cascade for `LeasesPage.tsx` + `RentCollectionPage.tsx`:
- Re-target their hooks (`useLeases`, etc.) to the cockpit endpoints if
  they exist, **or** if they remain on the ERPNext data path, just pass
  the `activeEntity` filter through to the existing query keys (the
  ERPNext API already supports `company`; the React side just needs to
  pass it).

**Pragmatic scope guidance:** the **PropertiesPage** is the demo-critical
view (CO selects entity → cockpit shows that LLC's properties → demo
passes). **LeasesPage + RentCollectionPage** can either:
(a) Also rewire to cockpit + filter via `entity` if cockpit has the
endpoints — **but the cockpit doesn't have leases/payments endpoints
on origin/main**, so this would be a much larger scope.
(b) Stay on the ERPNext data path and pass `activeEntity` through to the
existing ERPNext query (`company=…`).

**XO recommendation:** go with (b) for LeasesPage + RentCollectionPage —
pass `activeEntity` through to the ERPNext fetch as a `company=…` query
param (the ERPNext API already filters on this). Update `erpnext.ts` to
accept an optional `company` arg on `getLeases`, `getPayments`, etc.; the
hooks pass `activeEntity || undefined`. This keeps the W#64 scope tight
while still making the entity-switcher useful across the cockpit.

**`apps/anchor-react/src/api/erpnext.ts`** — add optional `company` arg
to `getProperties` (used by `useProperties`?  Actually `useProperties` is
now rewired to cockpit; but `getLeases`, `getPayments`, etc. retain the
ERPNext path):

```typescript
export async function getLeases(options: { company?: string } = {}): Promise<Lease[]> {
  const qs = new URLSearchParams()
  if (options.company) qs.set('company', options.company)
  const url = `/api/v1/erpnext/leases${qs.size > 0 ? `?${qs}` : ''}`
  const result = await apiFetch<ERPNextListResponse<Lease>>(url)
  return result.data
}
```

(Same change for `getLease`, `getPayments`, `getOutstandingInvoices`, etc.
The ERPNext side already supports `company=…` per the W#60 P2 design — verify
by reading the corresponding C# Bridge ERPNext route file. If the route does
NOT support `company=…` on a particular endpoint, that endpoint stays
un-filtered for W#64 and is logged as a follow-on. Do NOT block on this.)

`useLeases.ts`, `usePayments`, etc. — pass `{ company: activeEntity || undefined }`
to the fetch.

#### Tests

Two layers: hook-level (vitest) + component-level (testing-library).

**`apps/anchor-react/src/hooks/useActiveEntity.test.ts`** — new file:

1. `useActiveEntity_DefaultsToEmptyString_WhenLocalStorageEmpty` —
   fresh LocalStorage; expect `activeEntity === ''`.
2. `useActiveEntity_HydratesFromLocalStorage_OnMount` — pre-seed
   `localStorage.setItem('sunfish.cockpit.activeEntity', 'Acero Properties LLC')`;
   mount; expect `activeEntity === 'Acero Properties LLC'`.
3. `useActiveEntity_SetActiveEntity_PersistsToLocalStorage` — call
   `setActiveEntity('Bosco Properties LLC')`; expect
   `localStorage.getItem('sunfish.cockpit.activeEntity') === 'Bosco Properties LLC'`.
4. `useActiveEntity_SetActiveEntity_DoesNotThrow_WhenLocalStorageUnavailable` —
   stub `localStorage.setItem` to throw; call setter; expect no throw +
   in-memory state still updates.

**`apps/anchor-react/src/components/EntitySwitcher.test.tsx`** — new file:

5. `EntitySwitcher_RendersNothing_WhenSingleEntity` — seed store with
   one entity; expect `container.firstChild === null` (or appropriate
   "renders nothing" assertion).
6. `EntitySwitcher_RendersOptions_WhenMultipleEntities` — seed store
   with `['Acero Properties LLC', 'Bosco Properties LLC']`; expect 3
   options (the two entities + the "All entities" empty-string option).
7. `EntitySwitcher_OnChange_InvalidatesQueriesAndSetsActive` — render
   inside a `QueryClientProvider`; simulate selection of `Bosco Properties LLC`;
   expect `setActiveEntity` called with `Bosco Properties LLC` +
   `queryClient.invalidateQueries` called.

**`apps/anchor-react/src/pages/PropertiesPage.test.tsx`** — update the
existing test file (`PropertiesPage.test.tsx` exists per the file
listing). Add:

8. `PropertiesPage_PassesActiveEntity_ToFetch` — pre-seed
   `companyStore.activeCompany = 'Acero Properties LLC'`; render; expect
   the fetch was called with `?entity=Acero+Properties+LLC`.
9. `PropertiesPage_RendersEntityTagBadge_PerProperty` — mock the cockpit
   response with two properties (`entityTag: 'Acero …'`, `entityTag: null`);
   expect both badges render (`'Acero …'` and `'Unassigned'`).

#### Verification

- `cd apps/anchor-react && pnpm test` — all green (~7 new tests).
- `pnpm build` — succeeds.
- `pnpm typecheck` — succeeds. (`strict: true` per the project tsconfig.)
- Manual smoke test against a local dev Bridge + dev anchor-react:
  - Seed 4 properties across the 4 LLCs in `IPropertyRepository`
    (via test fixtures or a one-off seed script).
  - `pnpm dev`; open `http://localhost:5173/cockpit/`.
  - Expect the switcher dropdown to show **Acero / Bosco / Escola / Shirin**.
  - Select Acero; expect only Acero-tagged properties.
  - Reload the page; expect the selection persists (LocalStorage).
  - Select "All entities"; expect all 4 LLCs' properties.

#### PR description template

```
Wire React entity-switcher to cockpit endpoints + LocalStorage persistence
for W#64.

PR 1 added `Property.EntityTag`; PR 2 surfaced it through Bridge cockpit.
This PR completes the demo loop: switcher dropdown populates from
`/api/v1/cockpit/entities`; selection persists to LocalStorage;
PropertiesPage re-fetches with the selected entity filter.

- `EntitySwitcher.tsx` + `useActiveEntity.ts` — W#64 facade over
  existing `CompanySwitcher` + `companyStore`. Same UX, W#64 terminology.
- `getCockpitProperties({ entity })` + `listCockpitEntities()` — new
  client functions targeting the PR 2 endpoints.
- `useProperties` rewired from ERPNext data path to cockpit data path.
- `PropertiesPage` rewired to render `CockpitPropertySummary` shape with
  an entity-tag Badge.
- `LeasesPage` + `RentCollectionPage` retain the ERPNext data path but
  thread `activeEntity` through as `company=…` (ERPNext side already
  filters on company).
- LocalStorage key: `sunfish.cockpit.activeEntity`.
- ~7 new tests (hook + component).

Composition note in docstrings: this is an ADR 0032 sibling pattern —
same UX shape as the team-switcher, narrower scope.

Refs: W#64 hand-off; ADR 0032 (substrate); PRs 1 + 2 of this hand-off.
```

#### Do NOT in this PR

- Do NOT rename `companyStore` → `entityStore`. Facade-only rename.
- Do NOT delete `CompanySwitcher.tsx`. The new `EntitySwitcher` supersedes
  it in `app.tsx`'s render tree, but `CompanySwitcher` may remain
  unused-but-present until W#60 P3 work cleans up (small risk of
  unused-export lint warning — acceptable; explicit `// @deprecated W#64`
  comment is fine).
- Do NOT add `EntitySwitcher` to the Blazor adapter (`packages/ui-adapters-blazor/`).
  The ADR 0014 parity policy applies in principle, but the Blazor adapter
  already has `SunfishTeamSwitcher` (ADR 0032). An entity-switcher
  sibling in Blazor is **a separate parity-backlog item** — out of scope
  here.
- Do NOT change the cockpit auth policy. Entity-level capability gating
  (e.g., spouse sees Acero + Bosco only) is WS-H.

---

### PR 4 (optional) — ERPNext importer integration: derive `EntityTag` from ERPNext `company`

**Estimated effort:** ~1–2h
**Scope:** when importing Properties via the ERPNext → Anchor data path,
populate `Property.EntityTag` from the ERPNext `Property.company` field;
~3 tests
**Commit subject:** `feat(importer): derive Property.EntityTag from ERPNext company on import`
**Depends on:** PR 1 merged
**Branch:** `cob/w64-importer-entity-tag`

#### Trigger condition

Ship PR 4 **only if** the ERPNext-to-Anchor importer is already wired in
some form on origin/main (e.g., a `Sunfish.Bridge.Erpnext.PropertyImporter`
or equivalent service). If no importer exists yet, **defer this PR**;
the entity-tag will be populated manually or by a future importer
hand-off (likely `tooling-anchor-import-stage06-handoff.md` per the
ledger-handoff cross-refs).

#### File changes

Find the existing importer:

```bash
grep -rln "ErpnextProperty\|PropertyImporter\|ImportProperty" \
  accelerators/bridge/Sunfish.Bridge/ \
  packages/blocks-properties/ 2>/dev/null
```

For each found importer:
1. Read the ERPNext `Property.company` string field.
2. Assign it to `Property.EntityTag` on the upserted `Property` record.
3. Treat empty string as NULL: if `company == ""`, set `EntityTag = null`.

#### Tests

10. `PropertyImporter_PopulatesEntityTag_FromErpnextCompany` — import an
    ERPNext property with `company: "Acero Properties LLC"`; expect the
    upserted `Property.EntityTag == "Acero Properties LLC"`.
11. `PropertyImporter_TreatsEmptyErpnextCompanyAsNull` — import with
    `company: ""`; expect `EntityTag == null`.
12. `PropertyImporter_RoundTrips_OnReimport` — re-import the same source;
    expect `EntityTag` unchanged + record idempotent (no duplicate row).

#### Do NOT in this PR

- Do NOT introduce a new `IErpnextPropertyImporter` interface if one
  doesn't exist. PR 4 is purely additive to whatever exists.
- Do NOT add the corollary import path for Leases. The W#64 scope is
  Property-only; Lease entity tagging is a follow-on.

---

## PR sequence diagram

```
PR 1 (schema + repo + migration) ──┬── PR 2 (Bridge endpoint + entities list)
                                   │       │
                                   │       └── PR 3 (React switcher + page wiring)
                                   │
                                   └── PR 4 (optional importer)
```

PR 4 can land in parallel with PR 2 + PR 3 if the importer exists.

---

## Halt conditions

If COB hits any of these, **halt + file `cob-question-2026-05-XXTHH-MMZ-w64-{slug}.md`** in
`/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`.
Add a note in `active-workstreams.md` W#64 row. `ScheduleWakeup 1800s`.

### 1. `Property` entity's existing schema makes `EntityTag` placement ambiguous

If `packages/blocks-properties/Models/Property.cs` is no longer the
canonical home (e.g., the entity has been moved to a `Sunfish.Domain.*`
namespace, or split into a Phase 1 / Phase 2 record pair), file
`cob-question-*`; XO will rule on the canonical Property home. The
audit on 2026-05-16 confirmed the canonical home is
`packages/blocks-properties/Models/Property.cs` (sealed record with
`required` properties + `IMustHaveTenant` interface).

### 2. ADR 0032 team-switcher substrate API has changed

The hand-off assumes `IActiveTeamAccessor` exists in
`packages/kernel-runtime/Teams/` per ADR 0032's Implementation checklist.
If the interface has moved or been renamed, the **docstring references**
in PR 3's `useActiveEntity.ts` + `EntitySwitcher.tsx` still hold (they
reference the ADR by number, not by symbol). No code dependency — adjust
docstrings only.

If `IActiveTeamAccessor` does not exist on this branch at all (ADR 0032
not yet implemented), **the W#64 design still ships** — the ADR 0032
references in docstrings become aspirational. File `cob-question-*` only
if you want explicit XO confirmation.

### 3. Bridge cockpit endpoint structure differs from `GET /api/v1/cockpit/properties`

If the route in `accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs`
has been refactored (e.g., the route moved, the DTO shape changed, the
auth policy was renamed), reconcile in place: keep the existing route
and DTO conventions; layer `?entity=…` as additive. File
`cob-question-*` only if the existing semantics are incompatible with
adding an additional query parameter (extremely unlikely — query-string
extension is the standard non-breaking extension point).

### 4. `apps/anchor-react/` package structure changed

The hand-off assumes the file layout per origin/main (2026-05-16):

- `src/components/CompanySwitcher.tsx` + `src/stores/companyStore.ts`
- `src/hooks/useProperties.ts` + `src/hooks/useLeases.ts`
- `src/cockpit/api.ts` + `src/pages/PropertiesPage.tsx`
- `src/api/erpnext.ts`

If a parallel session has restructured this (e.g., moved hooks under
`src/cockpit/hooks/`), follow the actual layout — the hand-off's
relative-path guidance is the intent; the actual location is what
matters. File `cob-question-*` only if the cockpit data path itself
has been replaced (e.g., a different data-fetching library is in use).

### 5. ERPNext data path for leases/payments has been deprecated mid-flight

If the W#60 P3 sync engine has shipped between this hand-off authoring
and the build, the LeasesPage / RentCollectionPage data path may have
moved off `erpnext.ts`. Pivot accordingly: pass `activeEntity` through
to whatever data path is current. The PropertiesPage rewire (to cockpit)
holds regardless. File a `cob-question-*` if the new data path also has
no entity filter — that's a sibling scope question for XO.

### 6. The migration cadence in `Bridge.Data/Migrations/` has changed

If the EF migrations folder no longer holds the `blocks-properties`
table definitions (e.g., the table moved to a `blocks-properties.Data/Migrations/`
package-local migration folder), follow the new convention. The
migration shape (`AddEntityTagToProperty` with `Up()` adding column +
index, `Down()` dropping both) is invariant.

### 7. The four canonical LLC names are wrong or different in the test fixtures

If the W#60 P2 demo data uses different LLC names (e.g., the seed script
ships "Test Property LLC A/B/C/D"), keep the seed script unchanged —
**the four canonical names live in private project memory** and CO will
set them on the demo install via manual entity-tag assignment. The
hand-off's references to Acero / Bosco / Escola / Shirin are for
PASS-gate verification only, not for committed seed data. Do NOT commit
the canonical names to the repo.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PR 1 merged:** `Property.EntityTag` field + migration + repository
   filter + ~10 tests on `main`.
2. **PR 2 merged:** Bridge `/api/v1/cockpit/properties?entity=…` + new
   `/api/v1/cockpit/entities` endpoint + ~8 endpoint tests on `main`.
3. **PR 3 merged:** React `EntitySwitcher` + `useActiveEntity` +
   PropertiesPage rewire + LeasesPage / RentCollectionPage entity-pass-through
   + ~7 component/hook tests on `main`.
4. **(Optional) PR 4 merged** if the ERPNext importer exists; otherwise
   noted as deferred in the PR description of PR 3.
5. **Tests pass:** ~25–30 tests total green across the three layers.
6. **Migration round-trips:** `dotnet ef migrations script` produces a
   clean diff; `dotnet ef database update` against a fresh DB + against
   a DB with existing data both succeed; existing `Property` rows have
   `EntityTag IS NULL` post-migration.
7. **Demo-ready (manual verification by COB):**
   - Seed 4 properties via test fixtures with `EntityTag` set to
     `Acero Properties LLC` / `Bosco Properties LLC` /
     `Escola Properties LLC` / `Shirin Properties LLC` (one each).
   - `pnpm dev` in `apps/anchor-react/`.
   - Open `http://localhost:5173/cockpit/`.
   - Switcher shows all 4 LLCs + "All entities" option.
   - Selecting Acero → cockpit shows only the Acero property.
   - Selecting Bosco → cockpit shows only the Bosco property.
   - Reload → selection persists (LocalStorage).
   - Selecting "All entities" → all 4 visible again.
8. **`active-workstreams.md`** row for W#64 updated to `built` with PR
   numbers (via the source `W64-*.md` file in `icm/_state/workstreams/`,
   not the ledger directly — per
   `feedback_never_add_workstream_rows_directly_to_ledger`).
9. **`cob-status-2026-05-XXTHH-MMZ-w64-built.md`** dropped to the
   coordination inbox.

When the PASS gate is met, the **next gate-clear** is WS-H (spouse
co-ownership) — spouse capability grants can now be entity-scoped.

---

## Open questions

### 1. When do we migrate `EntityTag: string?` to FK-to-Party?

`_shared/engineering/party-model-convention.md` §3 establishes Party
as the canonical anchor for cross-cluster entity identity:

> Every actor in `blocks-people-*` (employee, contact, customer, tenant,
> lead, contractor) shares a single base abstraction: `Party`.

The convention extends Party to **organizations** (LLCs are
organizations), and `blocks-property-*` is one of the convention's
named consumers. Long-term, an LLC is a Party with role `legal-entity`
(or similar), and `Property.EntityTag` becomes `Property.OwningPartyId: ID<Party>?`.

**Why not now:** The Party-model retrofit is its own workstream
(per `icm/02_architecture/blocks-property-party-alignment-review.md`)
and lands when `blocks-people-foundation` ships. Pre-introducing the FK
in W#64 forces a coupling that doesn't yet have a target. The
string-tag is the Phase 1 expedient.

**Decision deferred to:** the Party-model alignment workstream (TBD —
likely W#7x range). When that workstream ships, it migrates
`Property.EntityTag: string?` → `Property.OwningPartyId: ID<Party>?` +
a backfill script. The string-tag column becomes vestigial and is
dropped in a follow-on migration.

### 2. Should `blocks-financial-*` AR Invoice + Bill also carry `EntityTag`?

Per the ledger hand-off (sibling Stage 06 in flight) + the Stage 02
schema design, `Invoice` and `Bill` reference a `LegalEntityId` —
which is the structural analog of `EntityTag`. They will not use the
string-tag shape; they will use the typed `LegalEntityId` from day one.

**Decision:** **No coupling.** `blocks-property-*` adopts the string-tag
expedient (W#64); `blocks-financial-*` adopts the typed `LegalEntityId`
on its first hand-off (the ledger hand-off). The two converge when the
Party-model retrofit lands (Open Question 1).

### 3. Should the Blazor adapter ship a parity `EntitySwitcher` component?

ADR 0014 (adapter parity policy) typically requires Blazor + React to
ship the same surface. The Blazor adapter already has
`SunfishTeamSwitcher.razor` (ADR 0032 substrate). An entity-switcher
parity in Blazor would be additive.

**Decision deferred to:** a future parity-backlog item. The Anchor MAUI
surface does not currently consume the cockpit endpoints in the same
way; the parity question is a routing question (which cockpit pages
need entity-filtering in the MAUI shell?). Not blocking W#64 PASS.

### 4. Capability scoping (spouse sees Acero + Bosco only) — when?

This is **WS-H** (spouse co-ownership). The capability-grant model
lives at the authorization-policy layer (`Sunfish.Foundation.Authorization`),
not at the cockpit-endpoint layer. WS-H will extend `CockpitPolicy`
or introduce a sibling policy that restricts the `?entity=…` query to
the user's granted entities.

**Decision deferred to:** WS-H. W#64 ships the data-plane filter; WS-H
adds the policy-plane scope.

### 5. Entity-tag administrator UI (assign / rename / delete)

W#64 ships the **data model + filter + switcher**. It does **not** ship
a UI for an operator to assign or rename entity tags on existing
properties. The 4 canonical LLCs will be assigned via test fixtures or
manual `IPropertyRepository.UpsertAsync` calls.

**Decision deferred to:** a follow-on UI hand-off (likely embedded in
the property-edit page when that ships). Not blocking W#64 PASS.

---

## Apply conventions

- **CRDT-friendly:** `EntityTag` is mutable on `Property` (CRDT
  semantics: last-writer-wins is acceptable for a free-text tag;
  not state-machine territory). The composite index `(TenantId, EntityTag)`
  is non-unique and CRDT-compatible.
- **MIT license** (Sunfish output).
- **No external borrows:** ADR 0032 substrate is internal; the
  entity-tag pattern is a standard string-column scheme. No upstream
  attribution required.
- **Tenant scoping:** every `EntityTag` query is bracketed by
  `TenantId` (the index leads with `TenantId`, and the repository
  call signature requires a `tenant` argument). No cross-tenant
  leakage is possible.
- **Naming:** `EntityTag` on the C# side; `entity` on the wire;
  `entityTag` on the JSON DTO; `useActiveEntity` on the React hook;
  `EntitySwitcher` on the React component; `sunfish.cockpit.activeEntity`
  as the LocalStorage key. Consistent across layers.

---

## Cohort discipline

W#64 is a **single-workstream** hand-off — no sub-workstreams; no
cohort. The closest precedents are the substrate-only hand-offs (W#34,
W#35, W#36) and the small-scope feature hand-offs (W#47, W#56, W#65).
COB self-audit pattern (per ADR 0028-A10):

- **Cited-symbol verification:** before declaring PR 3 done, read each
  cited symbol (`useCompanyStore`, `CompanySwitcher`, `getCockpitProperties`,
  `IPropertyRepository.ListByTenantAsync`, etc.) from the actual source
  file. Do not rely on grep-only verification.
- **Migration round-trip:** `dotnet ef migrations script` + Down + Up
  on a fresh DB before merging PR 1.
- **Test parity:** match the existing test conventions per file (e.g.,
  if `PropertiesPage.test.tsx` uses MSW for network mocks, the new tests
  must too — do not introduce a second mocking library).
- **`apps/docs` page:** **NOT in scope** for W#64. The W#64 surface is
  internal cockpit infrastructure; the public docs surface remains the
  team-switcher (ADR 0032). If a future workstream consolidates the
  switchers, a single docs page emerges then.

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w64-{slug}.md` in
  `/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`.
- Halt the workstream + add a note in `active-workstreams.md` W#64 row.
- `ScheduleWakeup 1800s`.

If COB completes PR 3 (PR 4 optional) + the PASS gate is met:

- Update `active-workstreams.md` (via the source `W64-*.md` file in
  `icm/_state/workstreams/`, not the ledger directly).
- Drop `cob-status-2026-05-XXTHH-MMZ-w64-built.md` to inbox.
- The downstream gate-clear is **WS-H (spouse co-ownership)**. XO will
  author the WS-H hand-off after W#64 is built (or in parallel with the
  W#64 build if priority queue is otherwise dry).

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-16):**

- `packages/blocks-properties/Models/Property.cs` (extended in PR 1) ✓
  Sealed record with `Id`, `TenantId`, `DisplayName`, `Address`, `Kind`,
  `CreatedAt`, etc. — no `EntityTag` yet.
- `packages/blocks-properties/Data/PropertyEntityConfiguration.cs` (extended in PR 1) ✓
  Existing indexes: `ix_properties_property_tenant_disposed`,
  `ix_properties_property_tenant_parcel`. New index added in PR 1:
  `ix_properties_property_tenant_entity_tag`.
- `packages/blocks-properties/Services/IPropertyRepository.cs` (extended in PR 1) ✓
  Existing methods: `GetByIdAsync`, `ListByTenantAsync(TenantId, bool, CancellationToken)`,
  `UpsertAsync`, `SoftDeleteAsync`. New overload added in PR 1:
  `ListByTenantAsync(TenantId, string? entityTag, bool, CancellationToken)`.
- `packages/blocks-properties/Services/InMemoryPropertyRepository.cs` (extended in PR 1) ✓
- `accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs` (extended in PR 2) ✓
  `HandleListPropertiesAsync` exists; new `HandleListEntitiesAsync` added in PR 2.
- `accelerators/bridge/Sunfish.Bridge.Data/Migrations/` (PR 1 adds migration) ✓
  Current latest migration: `20260423_Wave53A_TenantAuthSalt`.
- `apps/anchor-react/src/components/CompanySwitcher.tsx` (preserved; superseded by `EntitySwitcher` in PR 3) ✓
- `apps/anchor-react/src/stores/companyStore.ts` (preserved; wrapped by `useActiveEntity` in PR 3) ✓
- `apps/anchor-react/src/hooks/useProperties.ts` (rewritten in PR 3) ✓
- `apps/anchor-react/src/hooks/useLeases.ts` (entity-pass-through in PR 3) ✓
- `apps/anchor-react/src/cockpit/api.ts` (extended in PR 3) ✓
- `apps/anchor-react/src/api/erpnext.ts` (extended in PR 3 with optional `company` arg) ✓
- `apps/anchor-react/src/pages/PropertiesPage.tsx` (rewired in PR 3) ✓
- `apps/anchor-react/src/pages/LeasesPage.tsx` (entity-pass-through in PR 3) ✓
- `apps/anchor-react/src/pages/RentCollectionPage.tsx` (entity-pass-through in PR 3) ✓
- `apps/anchor-react/src/app.tsx` (renders `<EntitySwitcher />` instead of `<CompanySwitcher />` in PR 3) ✓
- `docs/adrs/0032-multi-team-anchor-workspace-switching.md` (Accepted; substrate referenced by docstrings in PR 3) ✓

**Introduced by this hand-off:**

- Field: `Property.EntityTag: string?`
- Index: `ix_properties_property_tenant_entity_tag` on `(TenantId, EntityTag)`
- Repository overload: `IPropertyRepository.ListByTenantAsync(TenantId, string?, bool, CancellationToken)`
- Migration: `AddEntityTagToProperty`
- DTO field: `PropertySelectorItemDto.EntityTag: string?`
- DTO: `EntityListDto(IReadOnlyList<string> Entities)`
- Endpoint: `GET /api/v1/cockpit/entities`
- Query parameter: `entity` on `GET /api/v1/cockpit/properties`
- React hook: `useActiveEntity` (in `apps/anchor-react/src/hooks/useActiveEntity.ts`)
- React component: `EntitySwitcher` (in `apps/anchor-react/src/components/EntitySwitcher.tsx`)
- React client: `getCockpitProperties({ entity? })` + `listCockpitEntities()` in `apps/anchor-react/src/cockpit/api.ts`
- LocalStorage key: `sunfish.cockpit.activeEntity`
- (PR 4, optional) `IErpnextPropertyImporter.EntityTag`-population logic

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies
each cited symbol by reading the actual file before declaring AP-21
clean. Do not rely on grep-only verification.

---

## Cross-cluster touches (future-direction; do not pre-introduce)

### `blocks-people-*` Party model

Long-term, `EntityTag` should reference a Party (LLC) entity rather
than a free-text tag. The migration path is documented in
`_shared/engineering/party-model-convention.md` §3 and tracked in
`icm/02_architecture/blocks-property-party-alignment-review.md`.

**For this hand-off:** keep `EntityTag: string?` simple. Do NOT
pre-introduce an FK to a Party that doesn't yet exist on this branch.

### `blocks-financial-*`

AR Invoice + Bill will carry a typed `LegalEntityId` (per the
financial-ledger schema design, Stage 02 §3.2) from day one — not the
string-tag shape. The two clusters converge when the Party-model
retrofit lands.

**For this hand-off:** no coupling. W#64 lives in `blocks-property-*`
only.

### `accelerators/anchor/` (MAUI shell)

The Anchor MAUI shell does not currently consume the React cockpit
endpoints — it has its own Blazor cockpit pages. W#64's filter does
not propagate to the MAUI shell automatically. If a future workstream
wants the MAUI cockpit to also filter by entity, it ships a Blazor
parity `SunfishEntitySwitcher.razor`.

**For this hand-off:** Anchor MAUI is out of scope.

---

## Workstream linkage

This hand-off ships **W#64** (single workstream; no sub-workstreams).

- **Upstream:** ADR 0032 (Accepted) — team-switcher substrate that this
  composes onto.
- **Sibling:** W#60 P2 (Built) — the React cockpit this hand-off
  extends. W#60 P3 (offline sync) is a future evolution that may
  supersede the ERPNext data path for LeasesPage / RentCollectionPage;
  W#64 is compatible with either data path.
- **Downstream (gate-clear):** **WS-H (spouse co-ownership)** — spouse
  capability grants require entity-by-entity scoping. WS-H is the next
  workstream up once W#64 ships.
- **Downstream (future):** Party-model retrofit (TBD W#7x) — migrates
  `EntityTag: string?` to `OwningPartyId: ID<Party>?`.

---

## Cross-references

- Source ratification: `coordination/inbox/xo-question-2026-05-16T16-05Z-w64-entity-switcher-option.md` (CO ratified Option A on 2026-05-16).
- Active-workstreams row: `icm/_state/active-workstreams.md` W#64.
- Substrate ADR: `docs/adrs/0032-multi-team-anchor-workspace-switching.md` (Accepted 2026-04-23).
- Domain spec (extended): `packages/blocks-properties/Models/Property.cs`.
- Future-direction reference: `_shared/engineering/party-model-convention.md` §3.
- Future-direction reference: `icm/02_architecture/blocks-property-party-alignment-review.md`.
- Sibling Stage 06 precedent: `icm/_state/handoffs/blocks-financial-ledger-chart-and-journal-stage06-handoff.md` (canonical format).
- W#60 P2 context: project memory `project_w60_erpnext_pivot_stack.md`.
- Canonical test properties (private): project memory `project_canonical_test_properties.md` (Acero / Bosco / Escola / Shirin).

---

**End of hand-off.**
