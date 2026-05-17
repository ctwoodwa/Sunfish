# Hand-off — W#64 blocks-properties EntityTag (entity-switcher Option A)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build` — gated on CO confirming Option A via `xo-question-2026-05-16T16-05Z-w64-entity-switcher-option.md`
**Workstream:** W#64 — ERPNext Company ↔ Sunfish Team entity-switcher
**Spec source:** `icm/_state/active-workstreams.md` W#64 row + ADR 0032 (multi-team workspace switching)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~4–6h sunfish-PM (2 PRs, ~10–14 tests)
**PR count:** 2 PRs
**Pre-merge council:** NOT required (non-breaking additive field; no financial invariants). Standard COB self-audit.

---

## Context

CO's 6 rental properties span 6 separate LLCs (Acero Properties LLC, Bosco Properties LLC,
Escola Properties LLC, Shirin Properties LLC, etc. — see canonical test properties in project
memory). Currently all 6 properties are co-mingled under a single `TenantId` in `blocks-properties`,
making it impossible for the cockpit to filter or switch "which LLC am I viewing?"

W#64 adds `EntityTag: string?` — an opaque label CO assigns when creating/editing a property
(e.g. `"Acero Properties LLC"`, `"Bosco Properties LLC"`) — and wires a cockpit UI switcher over
it. ADR 0032's `TeamContext` / `IActiveTeamAccessor` substrate is already built; this hand-off
adds the data field and uses the existing switching pattern.

**Option A (this hand-off) vs Option B vs Option C:**
- **Option A (this):** EntityTag on Property; works offline; no ERPNext dependency
- **Option B:** ERPNext Company list; loses offline capability; superseded by Path II (ADR 0088)
- **Option C:** Defer to P3 sync engine; delays WS-H (spouse capability grants)

With Path II (ADR 0088) in force, Option B is now irrelevant. Option A aligns naturally
with the native blocks-* architecture.

**Downstream:** WS-H (spouse co-ownership) depends on this — spouse needs capability grants
per entity-tag; no workaround until EntityTag exists.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Confirm CO approved Option A** — check `xo-question-2026-05-16T16-05Z-w64-entity-switcher-option.md`
   has been archived OR CO has replied. If still open, **STOP** and drop a `cob-question-*` beacon.

2. **Verify blocks-properties package is current.**
   ```bash
   ls packages/blocks-properties/Models/Property.cs
   ```
   Expected: exists; `EntityTag` field NOT present yet.

3. **Check open PRs touching blocks-properties.**
   ```bash
   gh pr list --state open | grep "blocks-properties"
   ```
   Expected: none. If found, coordinate with XO before proceeding.

4. **Check EFCore migration baseline.**
   ```bash
   ls packages/blocks-properties/Persistence/Migrations/ | tail -5
   ```
   Note the latest migration timestamp prefix; your new migration must use a higher timestamp.

---

## PR 1 — `EntityTag` on `Property` + repository filter + migration

**Branch:** `cob/blocks-properties-entity-tag-field`
**Commit subject:** `feat(blocks-properties): add EntityTag to Property for entity-switcher (W#64 Option A)`
**Estimated effort:** ~2–3h
**Depends on:** pre-build checklist passed

### Scope

**`packages/blocks-properties/Models/Property.cs`** — add one field:

```csharp
/// <summary>
/// Optional entity (LLC / company) label for the entity-switcher.
/// CO assigns this when creating or editing a property (e.g. "Acero Properties LLC").
/// Null means the property has not been tagged (visible in all entity contexts).
/// </summary>
public string? EntityTag { get; init; }
```

Position: after `DisposalReason`, before closing brace.

**`packages/blocks-properties/Contracts/IPropertyRepository.cs`** — add method:

```csharp
/// <summary>Returns properties whose <c>EntityTag</c> matches the given value (case-insensitive).</summary>
Task<IReadOnlyList<Property>> ListByEntityTagAsync(
    TenantId tenantId,
    string entityTag,
    CancellationToken cancellationToken = default);

/// <summary>Returns all distinct EntityTag values for this tenant (sorted; nulls excluded).</summary>
Task<IReadOnlyList<string>> ListEntityTagsAsync(
    TenantId tenantId,
    CancellationToken cancellationToken = default);
```

**`packages/blocks-properties/Persistence/Configurations/PropertyConfiguration.cs`** (EFCore config):

```csharp
builder.Property(p => p.EntityTag)
    .HasColumnName("entity_tag")
    .HasMaxLength(200)
    .IsRequired(false);

builder.HasIndex(p => new { p.TenantId, p.EntityTag })
    .HasDatabaseName("IX_properties_tenant_entity_tag");
```

**EFCore migration** (`packages/blocks-properties/Persistence/Migrations/`):

```sql
ALTER TABLE "properties" ADD COLUMN "entity_tag" TEXT NULL;
CREATE INDEX "IX_properties_tenant_entity_tag" ON "properties" ("tenant_id", "entity_tag");
```

Migration class name: `AddEntityTagToProperty`. Use `dotnet ef migrations add AddEntityTagToProperty`.

**`packages/blocks-properties/InMemory/InMemoryPropertyRepository.cs`** — implement both new methods:

```csharp
public Task<IReadOnlyList<Property>> ListByEntityTagAsync(
    TenantId tenantId, string entityTag, CancellationToken ct = default) =>
    Task.FromResult<IReadOnlyList<Property>>(
        _store.Values
            .Where(p => p.TenantId == tenantId &&
                        string.Equals(p.EntityTag, entityTag, StringComparison.OrdinalIgnoreCase))
            .ToList());

public Task<IReadOnlyList<string>> ListEntityTagsAsync(
    TenantId tenantId, CancellationToken ct = default) =>
    Task.FromResult<IReadOnlyList<string>>(
        _store.Values
            .Where(p => p.TenantId == tenantId && p.EntityTag is not null)
            .Select(p => p.EntityTag!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToList());
```

### Tests — PR 1 (`packages/blocks-properties/tests/`)

File: `EntityTagTests.cs`

```
PropertyRepository_ListByEntityTag_ReturnsMatchingProperties
PropertyRepository_ListByEntityTag_IsCaseInsensitive
PropertyRepository_ListByEntityTag_ExcludesOtherTenants
PropertyRepository_ListByEntityTag_ReturnsEmpty_WhenNoMatch
PropertyRepository_ListEntityTags_ReturnsDistinctSortedTags
PropertyRepository_ListEntityTags_ExcludesNullEntityTags
```

6 tests. All prior blocks-properties tests still pass.

### Halt conditions (PR 1)

- **H1:** EFCore migration conflicts with existing migration state → file `cob-question-*`.
- **H2:** `IPropertyRepository` is implemented in more than 2 places (InMemory + Postgres) →
  both must be updated; if a third appears, halt and list them in a `cob-question-*`.

---

## PR 2 — Cockpit entity-switcher UI

**Branch:** `cob/blocks-properties-entity-switcher-ui`
**Commit subject:** `feat(bridge,anchor-react): entity-switcher cockpit dropdown per W#64 Option A`
**Estimated effort:** ~2–3h
**Depends on:** PR 1 merged

### Scope

**New Bridge endpoint** (or extend existing cockpit endpoint):

```
GET /api/v1/cockpit/entity-tags
→ 200 { entityTags: string[] }      // sorted; calls IPropertyRepository.ListEntityTagsAsync
```

Add to Bridge `CockpitController` (or `PropertiesController`). Requires `IPropertyRepository`
already injected in that controller family.

**Anchor React UI** (`apps/anchor-react/src/`):

1. `useEntityTags()` hook — calls `GET /api/v1/cockpit/entity-tags` via TanStack Query; caches 5m.
2. `EntitySwitcher` component — `<Select>` dropdown rendering `entityTags` + an "All entities" sentinel.
   When selection changes: writes to `useEntityStore()` (Zustand) `activeEntityTag: string | null`.
3. Wire `EntitySwitcher` into the cockpit nav bar (alongside the existing team-switcher area).
4. Update `useProperties()` hook — when `activeEntityTag` is set, pass `entityTag` as a query
   param to `GET /api/v1/cockpit/properties?entityTag={tag}`. Bridge filters via
   `IPropertyRepository.ListByEntityTagAsync`.

**Blazor Anchor / Bridge cockpit** (optional — implement if the Blazor cockpit is the primary path):

- `EntitySwitcherComponent.razor` — mirrors the React dropdown but using `IActiveTeamAccessor`
  pattern from ADR 0032 substrate. Stores selection in `ISessionStore` (per-session).

**Note on ADR 0032 integration:** ADR 0032 built `TeamContext` + `IActiveTeamAccessor` for
switching between workspace "teams". `EntityTag` in CO's scenario maps to an LLC. Use the
`IActiveTeamAccessor` session context to store `ActiveEntityTag` if the team-context substrate
can carry arbitrary string metadata. If not, a simple Zustand / ISessionStore slot suffices
(not worth refactoring ADR 0032 for this).

### Tests — PR 2

File: `EntitySwitcherIntegrationTests.cs` (or `.spec.tsx` for React)

```
GET /api/v1/cockpit/entity-tags — returns sorted distinct tags
GET /api/v1/cockpit/properties?entityTag=Acero — filters by tag (case-insensitive)
GET /api/v1/cockpit/properties — no entityTag param → returns all (existing behaviour)
GET /api/v1/cockpit/properties?entityTag=Nonexistent — returns empty list, 200
```

4–8 tests depending on Blazor/React split.

### Halt conditions (PR 2)

- **H1:** `IPropertyRepository.ListByEntityTagAsync` is not yet available (PR 1 not merged) → STOP.
- **H2:** The Blazor cockpit controller path differs from the React path → implement only one;
  flag for parity work in a follow-on PR; do NOT block on parity.
- **H3:** `IActiveTeamAccessor` cannot carry `EntityTag` without an ADR 0032 amendment →
  use Zustand / ISessionStore instead; do NOT amend ADR 0032 here.

---

## After both PRs merge

1. File `cob-status-*` with PR numbers.
2. XO will flip W#64 ledger row to `built`.
3. XO will author WS-H (spouse co-ownership) hand-off (now unblocked).
