---
id: 85
title: TenantSelection Query Migration (W#1 WS-B)
status: Accepted
date: 2026-05-05
tier: foundation
pipeline_variant: sunfish-api-change
concern:
  - multi-tenancy
  - persistence
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
| `packages/foundation/Assets/Audit/InMemoryAuditLog.cs` | Existing consumer | yes — `query.Tenant is not { } tenant` pattern (line 81) |
| `packages/foundation/Assets/Entities/InMemoryEntityStore.cs` | Existing consumer | yes — `query.Tenant is { } tenant && ... tenant.Value` pattern (line 385) — **must be updated in §3.4** |
| `packages/foundation-assets-postgres/Audit/PostgresAuditLog.cs` | Existing consumer | yes — `query.Tenant is { } tenant` pattern (line 110) |
| `packages/foundation-assets-postgres/Entities/PostgresEntityStore.cs` | Existing consumer | yes — `query.Tenant is { } tenant` pattern (line 359) |
| `Sunfish.Kernel.Audit.AuditQuery` | **Explicitly NOT migrated** — `packages/kernel-audit/AuditQuery.cs` — required, non-nullable `TenantId TenantId` per ADR 0049 v0 | yes — confirmed non-nullable; excluded by design |

---

## Status

Proposed. Pre-merge council complete (adversarial + API-design + migration-safety; amendments applied 2026-05-05). Pending CO acceptance.

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
| `ExportRequest` | `Tenant` (renamed from `TenantId` — see §1.3) | `foundation-localfirst` | `TenantId? = null` |

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
- **Con (language-level blocker):** `TenantSelection.All` is `static readonly`, not `const`
  — it cannot appear as an optional-parameter default in C#. `TenantSelection Tenant = TenantSelection.All`
  is a compile error. This is the hard rejection; the blast-radius argument is secondary.
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

**1.3 `ExportRequest.Tenant` — `packages/foundation-localfirst/DataExport.cs`**

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
    public TenantSelection? Tenant { get; init; }
    ...
}
```

> **Rename rationale.** Renaming `TenantId → Tenant` is applied in this ADR (not deferred)
> because (a) `ExportRequest` has zero production callers at time of migration, (b) the
> property name `TenantId` would become a type-lie after migration (type is `TenantSelection?`,
> not a `TenantId`), and (c) consistency with `AuditQuery.Tenant` / `EntityQuery.Tenant`
> eliminates the source of future reader confusion.
>
> **`DataImport.TargetTenantId` is explicitly deferred.** `packages/foundation-localfirst/DataImport.cs`
> has `TenantId? TargetTenantId { get; init; }` — the symmetric import counterpart.
> It is deferred because import targets exactly one tenant by design (importing to multiple
> tenants simultaneously is not a supported use case), so `ForMultiple` semantics are
> inappropriate. It retains `TenantId?` until a future ADR addresses the import contract.

---

### 2. Add `TenantSelection.Matches(TenantId)` to `foundation-multitenancy`

Add to `packages/foundation-multitenancy/TenantSelection.cs`:

```csharp
/// <summary>
/// Returns true if this selection includes <paramref name="id"/>.
/// <list type="bullet">
/// <item><see cref="ForSingle"/> — matches iff <c>Tenant == id</c>.</item>
/// <item><see cref="ForMultiple"/> — matches iff <c>Tenants.Contains(id)</c>. An empty
///     <see cref="ForMultiple"/> matches no tenant (empty set returns false).</item>
/// <item><see cref="AllAccessible"/> — always true.</item>
/// </list>
/// </summary>
public bool Matches(TenantId id) => this switch
{
    ForSingle s    => s.Tenant == id,
    ForMultiple m  => m.Tenants.Contains(id),
    AllAccessible  => true,
    _              => throw new InvalidOperationException(
                         $"Unknown TenantSelection variant: {GetType().Name}. " +
                         "This indicates a new variant was added without updating Matches."),
};
```

> **`ForMultiple` empty-set semantics.** An empty `ForMultiple` (zero tenants) returns
> `false` for every tenant — meaning "no match." This is consistent with set-membership
> semantics (membership in the empty set is always false). Note that `ForMultiple`'s
> constructor in ADR 0084 §2 validates non-empty (`if (arr.IsEmpty) throw`), so an empty
> `ForMultiple` cannot be constructed via the public constructor; only `ImmutableArray.Empty`
> direct construction could produce one. The `false` result is a safe fallback. For Postgres
> implementations with an empty `tenants` array, short-circuit to "no rows" rather than
> emitting `WHERE ... IN ()` (SQL syntax error in some dialects):
>
> ```csharp
> else if (query.Tenant is TenantSelection.ForMultiple { Tenants: var tenants })
> {
>     if (tenants.IsEmpty)
>     {
>         return AsyncEnumerable.Empty<AuditRecord>(); // or equivalent for IQueryable: q = q.Where(_ => false)
>     }
>     var values = tenants.Select(t => t.Value).ToArray();
>     q = q.Where(a => values.Contains(a.Tenant));
> }
> ```

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

**3.4 `foundation/Assets/Entities/InMemoryEntityStore.cs` (§3.4)**

Before (line 385):
```csharp
if (query.Tenant is { } tenant && !string.Equals(record.Tenant.Value, tenant.Value, StringComparison.Ordinal))
    continue;
```

After:
```csharp
if (query.Tenant is not null && !query.Tenant.Matches(record.Tenant))
    continue;
```

**3.5 Existing test call sites**

Tests constructing `new EntityQuery(Tenant: new TenantId("t1"))` continue to compile
without change because the implicit cast `TenantId → TenantSelection` is in scope. No
test-file edits required unless tests assert on the type of the Tenant parameter.

> **`TenantId.Default` obsolete warning.** Any test passing `Tenant: TenantId.Default` will
> emit a warning after ADR 0084 marks it `[Obsolete]`. These should be updated to
> `new TenantId("test")` or `TenantId.System` as appropriate in the same PR.

---

### §4 Breaking-change classification

| Surface | Classification | Reason |
|---|---|---|
| `AuditQuery.Tenant` type `TenantId?` → `TenantSelection?` | **Mostly source-compatible** — non-null `TenantId` call sites unchanged; see edge cases below | Call sites passing a `TenantId` value compile via implicit cast |
| `EntityQuery.Tenant` type | **Mostly source-compatible** | Same |
| `ExportRequest.TenantId` → `ExportRequest.Tenant` (type + rename) | **Source-compatible** — zero production callers at migration time | Both type and name change in single ADR |
| `InMemoryAuditLog` / `PostgresAuditLog` / `PostgresEntityStore` / `InMemoryEntityStore` | Internal implementation — not public ABI | No binary impact |
| `TenantSelection.Matches(TenantId)` | **Additive** | New method; no existing caller breaks |
| **Pre-v1 binary compat** | N/A | Repository has not shipped a NuGet binary; no binary compat halt |

**Source-incompatible edge cases:**

1. **Code that pattern-matches on `.Tenant` as `TenantId?`** — e.g.:
   ```csharp
   if (q.Tenant is { } id && id.Value == "abc") // ← id is now TenantSelection, not TenantId
   ```
   These sites must switch to `q.Tenant is TenantSelection.ForSingle(var id) && id.Value == "abc"`.
   Scan: `grep -rn "\.Tenant is { }\|query\.Tenant\." packages/ apps/ --include="*.cs"` and
   `grep -rn "\.Tenant\.Value\b" packages/ apps/ --include="*.cs"`.

2. **Call sites passing `TenantId?` (nullable)** — the implicit cast `TenantId → TenantSelection`
   is on `TenantId`, not on `TenantId?` (`Nullable<TenantId>`). C# does not auto-lift user-defined
   conversions through `Nullable<T>` when the target type is a reference type. Any call site with
   a `TenantId?` variable must be updated to `maybeId is { } id ? (TenantSelection)id : null`
   or `maybeId.HasValue ? TenantSelection.Of(maybeId.Value) : null`. Scan:
   `grep -rn "Tenant:\s*maybe\|Tenant:\s*opt\|Tenant:\s*[a-z]*TenantId\?" packages/ apps/ --include="*.cs"`.

3. **Serialized `AuditQuery` / `EntityQuery` / `ExportRequest` payloads** — in the current
   codebase these are in-process POCO types, not serialized across a wire boundary (verified:
   no `[JsonConverter]` / `MessagePack` / `JsonSerialize` attributes on any of the three).
   COB should confirm before merging: `grep -rn "JsonSerializ\|MessagePack\|Newtonsoft" packages/foundation/Assets/ packages/foundation-localfirst/ --include="*.cs"`.

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
  match. **Four** implementations affected: `InMemoryAuditLog`, `InMemoryEntityStore`,
  `PostgresAuditLog`, `PostgresEntityStore`.
- `ForMultiple` in PostgresAuditLog / PostgresEntityStore generates an `IN (...)`
  clause (Npgsql translates `string[].Contains` to `= ANY(array)`). Very large tenant
  sets generate long queries; cap `ForMultiple.Tenants.Length` at ≤ 1000 in application
  logic; batch or use `AllAccessible` + post-filter for larger sets.
- `null` and `AllAccessible` are semantically equivalent ("no tenant filter") but not
  reference-equal. Callers that compare two `AuditQuery` instances may see unexpected
  inequality when one uses `null` and the other uses `AllAccessible`. Use `Matches` for
  filter-site comparisons rather than equality on the selection object.
- `ExportRequest.TenantId` is renamed to `Tenant`; any call site using named-init syntax
  `new ExportRequest { TenantId = ... }` must be updated (verified: zero production call
  sites at migration time).

---

## Revisit triggers

- A call site surfaces that serializes `AuditQuery` / `EntityQuery` / `ExportRequest` over
  a wire boundary. Amendment needed to specify versioned deserialization path.
- `DataImport.TargetTenantId` (in `foundation-localfirst`) is **not** migrated: import
  targets exactly one tenant; `ForMultiple` semantics are inappropriate. If a multi-tenant
  import use case emerges, a follow-up api-change ADR can address it.
- When `IStandingOrderEventStream.Subscribe` is wired in host registrations (ADR 0065-A1),
  any cache keyed on `TenantSelection` equality may see `null` and `AllAccessible` as
  distinct keys. Normalize to `AllAccessible` at cache-population time.

---

## Open questions (resolved)

| ID | Question | Disposition |
|---|---|---|
| OQ-1 | Should `DataImport.TargetTenantId` be migrated in this ADR? | Resolved: NO — import targets exactly one tenant; `ForMultiple` semantics inappropriate. Deferred; see §1.3 note. |
| OQ-2 | Should `ExportRequest.TenantId` be renamed in this ADR? | Resolved: YES — zero production callers; type-lie avoidance; consistency with `AuditQuery`/`EntityQuery`. |
| OQ-3 | `_ => false` vs `_ => throw` in `Matches` switch? | Resolved: `_ => throw new InvalidOperationException(...)` — silent false on an unknown variant is the worst default for a tenant filter predicate. |
| OQ-4 | Empty `ForMultiple` — semantics? | Resolved: `false` (set-membership; empty set has no members). Constructor validates non-empty; empty path is a defensive case only. |

---

## Council disposition

| Finding | Severity | Resolution |
|---|---|---|
| B-1 — `InMemoryEntityStore.cs:385` missing from §3 + §A0 | Blocking | §3.4 added; §A0 row added |
| B-2 — `_ => false` arm silently mis-classifies future variants | Blocking | Changed to `_ => throw new InvalidOperationException(...)` |
| B-3 — `DataImport.TargetTenantId` deferral undocumented | Blocking | §1.3 deferral note added + OQ-1 resolved |
| B-3 (adv) — Nullable `TenantId?` call sites not covered by implicit cast | Blocking | §4 reclassified as "mostly source-compatible"; edge-case 2 added |
| B-3 (api) — `ExportRequest.TenantId` rename deferred without justification | Blocking | Rename applied in §1.3; OQ-2 resolved |
| B-5 — `ForMultiple` empty-set semantics undefined | Blocking | §2 note added + OQ-4 resolved; Postgres empty-set guard shown in §2 |
| NB-2 (api) — Option C rejection rationale weak | NB | Option C re-stated with C# constant limitation |
| NB-4 (adv) — `kernel-audit/AuditQuery` absent from §A0 | NB | §A0 NOT-migrated row added |
| NB-5 (adv) — `kernel/TypeForwards.cs` re-export note | NB | Accepted; no §A0 change needed (type-forward is opaque to shape) |
| NB-6 (adv) — `TenantId.Default` obsolete warning in tests | NB | §3.5 note added |
| NB-7 (mig) — Pre-build scan commands incomplete | NB | §4 edge-case 1 + implementation checklist scan commands updated |

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

# Scan for ALL edge-case call sites (§4 edge cases 1–2)
grep -rn "\.Tenant is { }\|query\.Tenant\." packages/ apps/ --include="*.cs"
grep -rn "\.Tenant\.Value\b" packages/ apps/ --include="*.cs"
grep -rn "\.TenantId\.Value" packages/ apps/ --include="*.cs" | grep -v "kernel-audit"

# Confirm no serialization of the three query types
grep -rn "JsonSerializ\|MessagePack\|Newtonsoft" \
  packages/foundation/Assets/ packages/foundation-localfirst/ --include="*.cs"

# Confirm ExportRequest has no named-init call sites for TenantId
grep -rn "ExportRequest" packages/ apps/ --include="*.cs" | grep "TenantId\s*="
```

**Phase 1 (single PR):**
- [ ] Add `TenantSelection.Matches(TenantId)` to `foundation-multitenancy/TenantSelection.cs`
  (with `_ => throw new InvalidOperationException(...)`)
- [ ] Migrate `AuditQuery.Tenant` → `TenantSelection?` (§1.1)
- [ ] Migrate `EntityQuery.Tenant` → `TenantSelection?` (§1.2)
- [ ] Rename + migrate `ExportRequest.TenantId → Tenant : TenantSelection?` (§1.3)
- [ ] Update `InMemoryAuditLog.cs` to `query.Tenant is null || query.Tenant.Matches(r.Tenant)` (§3.1)
- [ ] Update `InMemoryEntityStore.cs` to `query.Tenant is not null && !query.Tenant.Matches(record.Tenant)` (§3.4)
- [ ] Update `PostgresAuditLog.cs` to ForSingle/ForMultiple/empty-set-guard pattern (§3.2)
- [ ] Update `PostgresEntityStore.cs` to ForSingle/ForMultiple/empty-set-guard pattern (§3.3)
- [ ] Any tests with `TenantId.Default` updated to `new TenantId("test")` (§3.5)
- [ ] Existing tests green (implicit cast covers `new TenantId("x")` call sites)
- [ ] New tests: `EntityQuery_ForMultiple_FiltersToSet`, `AuditQuery_AllAccessible_NoFilter`,
  `InMemoryEntityStore_ForMultiple_FiltersToSet`, `TenantSelection_Matches_ForMultiple_Empty_ReturnsFalse`
- [ ] Pre-merge council complete; no Blocking findings

## Implementation note (2026-05-13 — XO ruling xo-ruling-2026-05-13T03-15Z-w1-wsb-tenantselection-layer)

`TenantSelection` physically lives in the `foundation` package at
`packages/foundation/MultiTenancy/TenantSelection.cs` while retaining the
`Sunfish.Foundation.MultiTenancy` namespace. This resolves the circular
dependency that would arise from `foundation` importing
`foundation-multitenancy` (which already depends on `foundation`).
`foundation-multitenancy` references `foundation` via a ProjectReference, so
all consumers of `TenantSelection` who already depend on `foundation` (directly
or transitively) see it without any change to their `using` statements.
