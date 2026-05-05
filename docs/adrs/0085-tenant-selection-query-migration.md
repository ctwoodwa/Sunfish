---
id: 85
title: TenantSelection Query Migration (W#1 WS-B)
status: Proposed
date: 2026-05-05
tier: foundation
pipeline_variant: sunfish-api-change
concern:
  - multi-tenancy
  - query
enables:
  - multi-tenant-audit-queries
  - multi-tenant-entity-queries
  - multi-tenant-export
composes:
  - 84
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: []
---

# ADR 0085 — TenantSelection Query Migration (W#1 WS-B)

**Status:** Proposed
**Date:** 2026-05-05
**Authors:** XO research session
**Pipeline variant:** `sunfish-api-change`
**Council posture:** standard adversarial (3-perspective)
**Resolves:** W#1 WS-B — gated on ADR 0084 Status: Accepted. Intake at
`icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md`. Stage 01 discovery at
`icm/01_discovery/output/2026-05-05_multi-tenancy-type-surface.md`.

---

## §A0 Cited-symbol audit

| Symbol / Path | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Assets.Audit.AuditQuery` | Existing — `packages/foundation/Assets/Audit/AuditQuery.cs` | yes |
| `Sunfish.Foundation.Assets.Entities.EntityQuery` | Existing — `packages/foundation/Assets/Entities/EntityQuery.cs` | yes |
| `Sunfish.Foundation.LocalFirst.ExportRequest` | Existing — `packages/foundation-localfirst/DataExport.cs` | yes |
| `Sunfish.Foundation.Assets.Common.TenantId` | Existing (ADR 0084 — sentinel + `TenantId.System`) | yes |
| `Sunfish.Foundation.MultiTenancy.TenantSelection` | **Introduced by ADR 0084** — `foundation-multitenancy` | yes per ADR 0084 Decision §2 |
| `TenantSelection.ForSingle` / `.ForMultiple` / `.AllAccessible` | **Introduced by ADR 0084** | yes per ADR 0084 Decision §2 |
| `packages/foundation/Assets/Audit/InMemoryAuditLog.cs` | Existing consumer | yes — `query.Tenant is not { } tenant` pattern |
| `packages/foundation-assets-postgres/Audit/PostgresAuditLog.cs` | Existing consumer | yes — `query.Tenant is { } tenant` pattern |
| `packages/foundation-assets-postgres/Entities/PostgresEntityStore.cs` | Existing consumer | yes — `query.Tenant is { } tenant` pattern |

---

## Status

Proposed. Pre-merge council pending.

---

## Context

ADR 0084 (W#1 WS-A) introduced `TenantId.System` sentinel, marked `TenantId.Default`
`[Obsolete]`, and added the `TenantSelection` discriminated union (`ForSingle` /
`ForMultiple` / `AllAccessible`) to `foundation-multitenancy`. The implicit
`TenantId → TenantSelection` cast was placed on `TenantSelection.cs` to allow gradual
migration.

Three query-type properties still hold `TenantId?` for their tenant filter:

| Type | Property | Package | Current type |
|---|---|---|---|
| `AuditQuery` | `Tenant` | `foundation` | `TenantId? = null` |
| `EntityQuery` | `Tenant` | `foundation` | `TenantId? = null` |
| `ExportRequest` | `TenantId` | `foundation-localfirst` | `TenantId? = null` |

These sites limit multi-tenant query expressiveness: callers cannot ask "give me records for
these three tenants" or "give me all accessible records" without issuing separate queries. The
`AllAccessible` case is the existing `null` sentinel but with explicit type-system encoding.

`kernel-audit/AuditQuery.TenantId` (non-nullable, per ADR 0049 v0) is **NOT** a migration
target.

---

## Decision drivers

1. **Query sites currently model `null` as "unfiltered."** `TenantSelection.AllAccessible`
   is the explicit type-system encoding of the same intent. Migration preserves semantics
   while enabling `ForMultiple`.
2. **Implicit cast makes this nearly source-compatible.** Any call site passing a
   `TenantId` to `Tenant = someId` continues to compile after the type change because
   `TenantSelection` defines `implicit operator TenantSelection(TenantId id)`.
3. **`AllAccessible` replaces `null` as the "no filter" sentinel.** Downstream
   implementations stop pattern-matching on null and instead pattern-match on the union.
4. **Existing in-memory implementations need updated match patterns.** The
   `is not { } tenant` pattern does not type-check against `TenantSelection?`; a
   `Matches(TenantId)` helper on `TenantSelection` avoids per-site switch expressions.

---

## Considered options

### Option A — Keep `TenantId?` on query types forever

Leave `TenantId? Tenant = null` in place; never add `ForMultiple` to queries.

- **Pro:** No migration cost.
- **Con:** `ForMultiple` cross-tenant queries require multiple round-trips or ad-hoc joins;
  closes off a data-access path that Phase 2 commercial use cases require.
- **Rejected.**

### Option B — `TenantSelection?` with `null` = `AllAccessible` [RECOMMENDED]

Migrate all three sites from `TenantId?` to `TenantSelection?`. Keep `null` as a valid
no-filter sentinel (backwards compat at serialization layer). Add `TenantSelection.Matches(TenantId)`
for in-memory implementations. Update Postgres implementations to use `IN` clauses for
`ForMultiple`.

- **Pro:** Full `ForSingle`/`ForMultiple`/`AllAccessible` expressiveness. Implicit cast
  means most call sites need zero changes. Implementations gain `ForMultiple` SQL path.
- **Con:** Consumers that previously checked `is { } tenant` with `.Value` must update
  their pattern match. The `null` = "no filter" semantic now has two representations
  (`null` and `AllAccessible`); `Matches` unifies them.
- **Accepted.**

### Option C — Non-nullable `TenantSelection` with `TenantSelection.All` as default

Remove `?`; use `TenantSelection.All` where callers formerly passed `null`.

- **Pro:** Explicit type at all sites; no null ambiguity.
- **Con:** All call sites that constructed `new AuditQuery()` or relied on default
  parameters break unless they update their constructors. Higher blast radius for minimal
  gain (the existing `null` = AllAccessible convention is already well-understood).
- **Rejected.**

---

## Decision

### 1. Migrate three query-type properties

**1.1 `AuditQuery.Tenant` — `packages/foundation/Assets/Audit/AuditQuery.cs`**

Before (ADR 0084 WS-A baseline):
```csharp
public sealed record AuditQuery(
    EntityId? Entity = null,
    ActorId? Actor = null,
    TenantId? Tenant = null,   // ← migrate
    DateTimeOffset? FromInclusive = null,
    DateTimeOffset? ToExclusive = null,
    Op? Op = null,
    int? Limit = null);
```

After:
```csharp
using Sunfish.Foundation.MultiTenancy;

public sealed record AuditQuery(
    EntityId? Entity = null,
    ActorId? Actor = null,
    TenantSelection? Tenant = null,   // null == AllAccessible
    DateTimeOffset? FromInclusive = null,
    DateTimeOffset? ToExclusive = null,
    Op? Op = null,
    int? Limit = null);
```

**1.2 `EntityQuery.Tenant` — `packages/foundation/Assets/Entities/EntityQuery.cs`**

Before:
```csharp
public sealed record EntityQuery(
    SchemaId? Schema = null,
    TenantId? Tenant = null,   // ← migrate
    DateTimeOffset? AsOf = null,
    bool IncludeDeleted = false,
    int? Limit = null);
```

After:
```csharp
using Sunfish.Foundation.MultiTenancy;

public sealed record EntityQuery(
    SchemaId? Schema = null,
    TenantSelection? Tenant = null,   // null == AllAccessible
    DateTimeOffset? AsOf = null,
    bool IncludeDeleted = false,
    int? Limit = null);
```

**1.3 `ExportRequest.TenantId` — `packages/foundation-localfirst/DataExport.cs`**

Before:
```csharp
public sealed record ExportRequest
{
    /// <summary>Tenant whose data is being exported; null for system-scope exports.</summary>
    public TenantId? TenantId { get; init; }
    ...
}
```

After:
```csharp
using Sunfish.Foundation.MultiTenancy;

public sealed record ExportRequest
{
    /// <summary>
    /// Tenant scope for this export. <see cref="TenantSelection.ForSingle"/> for a single
    /// tenant, <see cref="TenantSelection.ForMultiple"/> for a set, or null /
    /// <see cref="TenantSelection.AllAccessible"/> for a system-scope export.
    /// </summary>
    public TenantSelection? TenantId { get; init; }
    ...
}
```

> **Note.** The property name `TenantId` is retained despite the type change to avoid a
> second rename-churn on call sites that use the named init syntax. A future api-change ADR
> may rename it to `Tenant` for consistency with `AuditQuery`/`EntityQuery`.

---

### 2. Add `TenantSelection.Matches(TenantId)` to `foundation-multitenancy`

Add to `packages/foundation-multitenancy/TenantSelection.cs`:

```csharp
/// <summary>
/// Returns true if this selection includes <paramref name="id"/>.
/// <list type="bullet">
/// <item><see cref="ForSingle"/> — matches iff <c>Tenant == id</c>.</item>
/// <item><see cref="ForMultiple"/> — matches iff <c>Tenants.Contains(id)</c>.</item>
/// <item><see cref="AllAccessible"/> — always true.</item>
/// </list>
/// </summary>
public bool Matches(TenantId id) => this switch
{
    ForSingle s    => s.Tenant == id,
    ForMultiple m  => m.Tenants.Contains(id),
    AllAccessible  => true,
    _              => false,
};
```

This is a pure additive extension to `foundation-multitenancy` (no new package, no new
dependency). It ships as part of the same PR that migrates `AuditQuery` and `EntityQuery`
(or as a prerequisite PR).

---

### 3. Update consumer implementations

**3.1 `foundation/Assets/Audit/InMemoryAuditLog.cs` (§3.1)**

Before:
```csharp
.Where(r => query.Tenant is not { } tenant || r.Tenant == tenant)
```

After:
```csharp
.Where(r => query.Tenant is null || query.Tenant.Matches(r.Tenant))
```

**3.2 `foundation-assets-postgres/Audit/PostgresAuditLog.cs` (§3.2)**

Before:
```csharp
if (query.Tenant is { } tenant)
    q = q.Where(a => a.Tenant == tenant.Value);
```

After:
```csharp
if (query.Tenant is TenantSelection.ForSingle(var tenant))
    q = q.Where(a => a.Tenant == tenant.Value);
else if (query.Tenant is TenantSelection.ForMultiple { Tenants: var tenants })
{
    var values = tenants.Select(t => t.Value).ToArray();
    q = q.Where(a => values.Contains(a.Tenant));
}
// AllAccessible and null → no tenant filter; full scope
```

**3.3 `foundation-assets-postgres/Entities/PostgresEntityStore.cs` (§3.3)**

Same pattern as §3.2 above — replace `query.Tenant is { } tenant ... tenant.Value` with
ForSingle/ForMultiple/fallthrough pattern.

**3.4 Existing test call sites**

Tests constructing `new EntityQuery(Tenant: new TenantId("t1"))` continue to compile
without change because the implicit cast `TenantId → TenantSelection` is in scope. No
test-file edits required unless tests assert on the type of the Tenant parameter.

---

### §4 Breaking-change classification

| Surface | Classification | Reason |
|---|---|---|
| `AuditQuery.Tenant` type `TenantId?` → `TenantSelection?` | **Source-compatible** at most call sites (implicit cast) | Call sites passing a `TenantId` value continue to compile |
| `EntityQuery.Tenant` type | **Source-compatible** | Same |
| `ExportRequest.TenantId` type | **Source-compatible** | Same |
| `InMemoryAuditLog` / `PostgresAuditLog` / `PostgresEntityStore` | Internal implementation — not public ABI | No binary impact |
| `TenantSelection.Matches(TenantId)` | **Additive** | New method; no existing caller breaks |
| **Pre-v1 binary compat** | N/A | Repository has not shipped a NuGet binary; no binary compat halt |

**Source-incompatible edge cases:**

1. **Code that pattern-matches on `AuditQuery.Tenant` as `TenantId?`** — e.g.:
   ```csharp
   if (q.Tenant is { } id && id.Value == "abc") // ← id is now TenantSelection, not TenantId
   ```
   These sites must switch to `q.Tenant is TenantSelection.ForSingle(var id) && id.Value == "abc"`.
   Scan: `grep -rn "\.Tenant is { }" packages/ apps/` and `grep -rn "\.Tenant\.Value" packages/ apps/`.

2. **Serialized `AuditQuery` / `EntityQuery` / `ExportRequest` payloads** — if any call site
   serializes these records to JSON/Protobuf/MessagePack and deserializes them on a different
   assembly version, the discriminated-union shape breaks wire compatibility. In the current
   codebase these are in-process POCO types, not serialized across a wire boundary. COB
   should confirm no serialization usage before merging.

---

## Consequences

### Positive

- `AuditQuery` / `EntityQuery` / `ExportRequest` can now express "records for tenants A, B, and C"
  with a single query, avoiding N-query fan-out.
- `AllAccessible` is the explicit type-safe form of the former `null` sentinel; callers get
  a meaningful type at the filter site.
- Implicit cast means most existing call sites require zero edits.
- `ForMultiple` paves the way for Phase 2 multi-tenant data-access patterns (cross-tenant
  dashboards, system-scope audit queries).

### Negative

- Implementations that relied on `.Value` on the Tenant property must update their pattern
  match. Three implementations affected (InMemory, PostgresAudit, PostgresEntity).
- `ForMultiple` in PostgresAuditLog / PostgresEntityStore generates an `IN (...)` clause;
  very large tenant sets will generate large SQL; callers of `ForMultiple` should cap the
  tenant set in application logic.

---

## Revisit triggers

- A call site surfaces that serializes `AuditQuery` / `EntityQuery` / `ExportRequest` over
  a wire boundary. Amendment needed to specify versioned deserialization path.
- `ExportRequest.TenantId` rename to `Tenant` is desired for consistency. Defer to a
  separate api-change amendment.

---

## References

- **ADR 0084** — `TenantId` sentinel governance + `TenantSelection` introduction (WS-A)
- **Intake:** `icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md`
- **Stage 01 Discovery:** `icm/01_discovery/output/2026-05-05_multi-tenancy-type-surface.md`
- ADR 0049 — audit-trail substrate (`kernel-audit/AuditQuery.TenantId` is NOT a migration target)

---

## Implementation checklist (W#1 WS-B — sunfish-PM)

**Pre-build verification:**

```bash
# Confirm ADR 0084 is on origin/main (WS-A gate)
grep "Status: Accepted" docs/adrs/0084-tenant-selection-and-sentinel-governance.md

# Confirm TenantSelection is present in foundation-multitenancy
find packages/foundation-multitenancy -name "TenantSelection.cs" | head -1

# Scan for edge-case call sites (§4)
grep -rn "\.Tenant is { }" packages/ apps/ --include="*.cs"
grep -rn "\.Tenant\.Value" packages/ apps/ --include="*.cs"
grep -rn "\.TenantId\.Value" packages/ apps/ --include="*.cs" | grep -v "kernel-audit"
```

**Phase 1 (single PR):**
- [ ] Add `TenantSelection.Matches(TenantId)` to `foundation-multitenancy/TenantSelection.cs`
- [ ] Migrate `AuditQuery.Tenant` → `TenantSelection?`
- [ ] Migrate `EntityQuery.Tenant` → `TenantSelection?`
- [ ] Migrate `ExportRequest.TenantId` → `TenantSelection?`
- [ ] Update `InMemoryAuditLog.cs` to `query.Tenant.Matches(r.Tenant)` pattern
- [ ] Update `PostgresAuditLog.cs` to ForSingle/ForMultiple pattern
- [ ] Update `PostgresEntityStore.cs` to ForSingle/ForMultiple pattern
- [ ] Existing tests green (implicit cast covers `new TenantId("x")` call sites)
- [ ] New tests: `EntityQuery_ForMultiple_FiltersToSet`, `AuditQuery_AllAccessible_NoFilter`
- [ ] Pre-merge council complete; no Blocking findings
