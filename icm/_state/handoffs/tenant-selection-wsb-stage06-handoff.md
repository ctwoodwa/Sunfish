# W#1 WS-B Stage 06 Hand-Off — TenantSelection Query Migration (ADR 0085)

**Workstream:** W#1 WS-B  
**ADR:** [0085 — TenantSelection Query Migration](../../../docs/adrs/0085-tenant-selection-query-migration.md)  
**Status:** `design-in-flight` — flip to `ready-to-build` when BOTH ADR 0084 AND ADR 0085
  status flip to Accepted **AND** WS-A has been built and merged.  
**Packages modified:** `packages/foundation/` + `packages/foundation-multitenancy/` +
  `packages/foundation-localfirst/` + `packages/foundation-assets-postgres/`  
**Authored:** 2026-05-06 (XO research session; pre-authored pending CO acceptance of ADR 0084 + 0085)  
**Build estimate:** ~3–4h / 1 PR  
**Pipeline variant:** `sunfish-api-change`

---

## Critical context

**GATE 1:** ADR 0084 must be `status: Accepted` before starting.
**GATE 2:** ADR 0085 must be `status: Accepted` before starting.
**GATE 3:** WS-A must be built (TenantSelection.cs must exist on origin/main).

```bash
grep "^status:" docs/adrs/0084-tenant-selection-and-sentinel-governance.md
# EXPECT: status: Accepted

grep "^status:" docs/adrs/0085-tenant-selection-query-migration.md
# EXPECT: status: Accepted

find packages/foundation-multitenancy -name "TenantSelection.cs" | head -1
# EXPECT: packages/foundation-multitenancy/TenantSelection.cs
```

If any gate fails, STOP. ADR acceptance is CO's action; a merged PR ≠ Accepted.

This workstream **is a breaking change** (`sunfish-api-change` pipeline variant). It migrates
three query-type properties from `TenantId?` to `TenantSelection?` and updates four consumer
implementations. However, the implicit cast `TenantId → TenantSelection` makes most call sites
source-compatible — see §4 of ADR 0085 for edge cases.

---

## Prerequisites verification checklist

```bash
# H1: AuditQuery.Tenant is TenantId? (not yet migrated)
grep -n "Tenant" packages/foundation/Assets/Audit/AuditQuery.cs
# EXPECT: TenantId? Tenant = null,

# H2: EntityQuery.Tenant is TenantId? (not yet migrated)
grep -n "Tenant" packages/foundation/Assets/Entities/EntityQuery.cs
# EXPECT: TenantId? Tenant = null,

# H3: ExportRequest.TenantId is TenantId? (not yet migrated; property named TenantId)
grep -n "TenantId\|Tenant" packages/foundation-localfirst/DataExport.cs | head -5
# EXPECT: TenantId? TenantId line (property name TenantId, not Tenant)

# H4: InMemoryAuditLog uses old Tenant pattern (line ~81)
grep -n "query\.Tenant" packages/foundation/Assets/Audit/InMemoryAuditLog.cs
# EXPECT: query.Tenant is not { } tenant || r.Tenant == tenant

# H5: InMemoryEntityStore uses old Tenant pattern (line ~385)
grep -n "query\.Tenant" packages/foundation/Assets/Entities/InMemoryEntityStore.cs
# EXPECT: query.Tenant is { } tenant && !string.Equals(record.Tenant.Value, tenant.Value, ...)

# H6: TenantSelection.Matches already present (WS-A pre-includes it)
grep -n "Matches" packages/foundation-multitenancy/TenantSelection.cs | head -3
# EXPECT: public bool Matches( ...
# If ZERO hits: add Matches per the §Matches specification below.

# H7: No serialization attributes on query types
grep -rn "JsonSerializ\|MessagePack\|Newtonsoft" \
  packages/foundation/Assets/ packages/foundation-localfirst/ --include="*.cs"
# EXPECT: zero hits — if any appear, write cob-question beacon; halt

# H8: No named-init call sites for ExportRequest.TenantId
grep -rn "ExportRequest" packages/ apps/ --include="*.cs" | grep "TenantId\s*="
# EXPECT: zero hits — if any appear, update those sites in this PR
```

---

## Pre-build scan — edge-case call sites

Run BEFORE editing any files. Capture the list; address all findings in this PR:

```bash
# Edge case 1: code that pattern-matches Tenant as TenantId? (not as TenantSelection)
grep -rn "\.Tenant is { }\|query\.Tenant\." packages/ apps/ --include="*.cs"

# Edge case 2: nullable TenantId? local variables assigned to Tenant
# (implicit cast doesn't lift through Nullable<TenantId>)
grep -rn "Tenant:\s*maybe\|Tenant:\s*opt\|Tenant:\s*[a-z]\+TenantId\?" packages/ apps/ --include="*.cs"

# Edge case 3: .Tenant.Value (property access on old TenantId type)
grep -rn "\.Tenant\.Value\b" packages/ apps/ --include="*.cs"

# Edge case 4: TenantId.Default used in tests with Tenant parameter
grep -rn "TenantId\.Default" packages/ apps/ --include="*.cs" | grep -i "tenant"
```

Document all findings in the PR description.

---

## Phase 1 — Query migration (1 PR, ~3–4h)

**Gate:** All H1–H8 pass AND all three ADR/WS-A gates pass.

---

### File 1: `packages/foundation-multitenancy/TenantSelection.cs` — verify/add Matches

**Check first:** WS-A hand-off pre-includes `Matches` to avoid a second PR. If H6 returned
≥1 hit, `Matches` is already present — **skip this edit entirely**.

If H6 returned zero hits (WS-A did not ship `Matches`), add the following method inside the
`TenantSelection` abstract record body, after the factory methods:

```csharp
/// <summary>
/// Returns true if this selection includes <paramref name="tenantId"/>.
/// Per ADR 0085 §2.
/// </summary>
public bool Matches(TenantId tenantId) => this switch
{
    ForSingle s    => s.TenantId == tenantId,
    ForMultiple m  => m.TenantIds.Contains(tenantId),
    AllAccessible  => true,
    _              => throw new InvalidOperationException(
                         $"Unknown TenantSelection case: {GetType().Name}. " +
                         "A new variant was added without updating Matches."),
};
```

> **Property-name note.** `ForSingle` exposes `.TenantId` (not `.Tenant`); `ForMultiple`
> exposes `.TenantIds` (not `.Tenants`). These names come from WS-A — do NOT use `.Tenant`
> or `.Tenants` which appear in ADR 0085 §2 as an earlier draft; the hand-off names are canonical.

---

### File 2: `packages/foundation/Assets/Audit/AuditQuery.cs` — migrate Tenant type

Per ADR 0085 §1.1.

**Edit 1 — Add using directive.** At the top of the file, add (if not already present):

```csharp
using Sunfish.Foundation.MultiTenancy;
```

**Edit 2 — Change `Tenant` parameter type.** In the `AuditQuery` record constructor:

```csharp
// Before:
TenantId? Tenant = null,

// After:
TenantSelection? Tenant = null,   // null == AllAccessible (no tenant filter)
```

---

### File 3: `packages/foundation/Assets/Entities/EntityQuery.cs` — migrate Tenant type

Per ADR 0085 §1.2.

**Edit 1 — Add using directive:**

```csharp
using Sunfish.Foundation.MultiTenancy;
```

**Edit 2 — Change `Tenant` parameter type:**

```csharp
// Before:
TenantId? Tenant = null,

// After:
TenantSelection? Tenant = null,   // null == AllAccessible (no tenant filter)
```

---

### File 4: `packages/foundation-localfirst/DataExport.cs` — rename + migrate ExportRequest

Per ADR 0085 §1.3. Two changes: property type AND property name.

**Edit 1 — Add using directive:**

```csharp
using Sunfish.Foundation.MultiTenancy;
```

**Edit 2 — Replace property.**

```csharp
// Before:
/// <summary>Tenant whose data is being exported; null for system-scope exports.</summary>
public TenantId? TenantId { get; init; }

// After:
/// <summary>
/// Tenant scope for this export. <see cref="TenantSelection.ForSingle"/> for a single
/// tenant, <see cref="TenantSelection.ForMultiple"/> for a set, or null /
/// <see cref="TenantSelection.AllAccessible"/> for a system-scope export.
/// Per ADR 0085 §1.3.
/// </summary>
public TenantSelection? Tenant { get; init; }
```

> **Rename justification (ADR 0085 OQ-2):** Zero production callers at migration time.
> `TenantId` as a property name would be a type-lie after migration. Consistency with
> `AuditQuery.Tenant` / `EntityQuery.Tenant`. Rename applied in this ADR, not deferred.

---

### File 5: `packages/foundation/Assets/Audit/InMemoryAuditLog.cs` — update match pattern

Per ADR 0085 §3.1. Line ~81.

```csharp
// Before:
.Where(r => query.Tenant is not { } tenant || r.Tenant == tenant)

// After:
.Where(r => query.Tenant is null || query.Tenant.Matches(r.Tenant))
```

---

### File 6: `packages/foundation/Assets/Entities/InMemoryEntityStore.cs` — update match pattern

Per ADR 0085 §3.4. Line ~385.

```csharp
// Before:
if (query.Tenant is { } tenant && !string.Equals(record.Tenant.Value, tenant.Value, StringComparison.Ordinal))
    continue;

// After:
if (query.Tenant is not null && !query.Tenant.Matches(record.Tenant))
    continue;
```

---

### File 7: `packages/foundation-assets-postgres/Audit/PostgresAuditLog.cs` — update to ForSingle/ForMultiple pattern

Per ADR 0085 §3.2. Line ~110.

**Add using directive** (if not already present):
```csharp
using Sunfish.Foundation.MultiTenancy;
```

**Replace the Tenant filter block:**

```csharp
// Before:
if (query.Tenant is { } tenant)
    q = q.Where(a => a.Tenant == tenant.Value);

// After:
if (query.Tenant is TenantSelection.ForSingle(var tenant))
    q = q.Where(a => a.Tenant == tenant.Value);
else if (query.Tenant is TenantSelection.ForMultiple { TenantIds: var tenants })
{
    if (tenants.IsEmpty)
        return AsyncEnumerable.Empty<AuditRecord>();   // empty ForMultiple = no rows
    var values = tenants.Select(t => t.Value).ToArray();
    q = q.Where(a => values.Contains(a.Tenant));
}
// AllAccessible and null → no tenant filter; full scope
```

> **Npgsql note.** `string[].Contains` translates to `= ANY(@p)` in Npgsql EFCore — not
> a raw `IN` clause. This is safe for large arrays, but application logic should still cap
> `ForMultiple.TenantIds.Length` at ≤ 1000 to avoid oversized parameters.

---

### File 8: `packages/foundation-assets-postgres/Entities/PostgresEntityStore.cs` — update to ForSingle/ForMultiple pattern

Per ADR 0085 §3.3. Line ~359. Same pattern as File 7.

**Add using directive** (if not already present):
```csharp
using Sunfish.Foundation.MultiTenancy;
```

**Replace the Tenant filter block:**

```csharp
// Before:
if (query.Tenant is { } tenant)
    q = q.Where(e => e.Tenant == tenant.Value);

// After:
if (query.Tenant is TenantSelection.ForSingle(var tenant))
    q = q.Where(e => e.Tenant == tenant.Value);
else if (query.Tenant is TenantSelection.ForMultiple { TenantIds: var tenants })
{
    if (tenants.IsEmpty)
        return AsyncEnumerable.Empty<EntityRecord>();   // empty ForMultiple = no rows
    var values = tenants.Select(t => t.Value).ToArray();
    q = q.Where(e => values.Contains(e.Tenant));
}
// AllAccessible and null → no tenant filter
```

> **Return type note.** Adjust the empty-ForMultiple return type to match the actual method
> return type in `PostgresEntityStore`. If the method returns `IAsyncEnumerable<TEntity>`,
> use `AsyncEnumerable.Empty<TEntity>()`. If it returns `IQueryable<T>`, use
> `q = q.Where(_ => false)` instead (SQL short-circuit).

---

### Tests to write

Per ADR 0085 §Implementation checklist. Minimum 4 new tests (add to existing test projects):

1. `EntityQuery_ForMultiple_FiltersToSet` — `InMemoryEntityStore` returns only records for
   the tenants in `ForMultiple`, not others
2. `AuditQuery_AllAccessible_NoFilter` — `InMemoryAuditLog` returns records for all tenants
   when `Tenant = TenantSelection.AllAccessible`
3. `InMemoryEntityStore_ForMultiple_FiltersToSet` — explicit entity-store test for
   `ForMultiple` behavior
4. `TenantSelection_Matches_ForMultiple_Empty_ReturnsFalse` — an empty `ImmutableArray`
   direct-constructed `ForMultiple` (bypassing the constructor guard) returns `false` from
   `Matches` for any tenant

**Also update any existing tests** using `TenantId.Default` as a Tenant value — per ADR 0084,
`Default` is now `[Obsolete]`. Replace with `new TenantId("test-tenant")`.

---

## Acceptance criteria

- [ ] `grep -n "TenantSelection" packages/foundation/Assets/Audit/AuditQuery.cs` shows migration
- [ ] `grep -n "TenantSelection" packages/foundation/Assets/Entities/EntityQuery.cs` shows migration
- [ ] `grep -n "TenantSelection" packages/foundation-localfirst/DataExport.cs` shows migration
- [ ] `grep -n "ExportRequest" packages/ -r --include="*.cs" | grep "TenantId\s*="` returns zero
- [ ] `grep -n "query\.Tenant.Matches\|query\.Tenant is null" packages/foundation/Assets/Audit/InMemoryAuditLog.cs` shows update
- [ ] `grep -n "query\.Tenant.Matches" packages/foundation/Assets/Entities/InMemoryEntityStore.cs` shows update
- [ ] `grep -n "ForSingle\|ForMultiple" packages/foundation-assets-postgres/Audit/PostgresAuditLog.cs` shows update
- [ ] `grep -n "ForSingle\|ForMultiple" packages/foundation-assets-postgres/Entities/PostgresEntityStore.cs` shows update
- [ ] All 4 new unit tests pass
- [ ] `dotnet build` across all packages: 0 errors; CS0618 warnings appear ONLY on `TenantId.Default`
  call sites that weren't addressed — document any remaining in the PR description
- [ ] Pre-merge 3-subagent council (adversarial + migration-safety + api-design)
  per ADR 0085 §Council disposition

---

## Halt conditions

| # | Condition | Action |
|---|---|---|
| H1 | ADR 0084 or ADR 0085 not `status: Accepted` | STOP. Do not build. ADR acceptance is CO's action. |
| H2 | `TenantSelection.cs` absent (WS-A not yet merged) | STOP. WS-B has a hard dependency on WS-A being on origin/main. |
| H3 | `Matches` property names differ from WS-A (`s.TenantId` / `m.TenantIds`) | Halt. This hand-off's §File 1 uses WS-A canonical names. If WS-A shipped different names, adjust accordingly and note the discrepancy in the PR. Do NOT use ADR 0085 §2 draft names (`s.Tenant` / `m.Tenants`). |
| H4 | Serialization attributes found on query types (H7 pre-build check) | Write `cob-question` beacon; halt — scope must expand to cover serialization migration. |
| H5 | Unexpected call sites found in edge-case scan | Document all in PR description. Apply changes in this PR. If blast radius is large (>10 files beyond the 8 listed), write `cob-question` beacon and wait for XO guidance. |
| H6 | `PostgresEntityStore` returns `IQueryable<T>` (not `IAsyncEnumerable<T>`) | Use `q = q.Where(_ => false)` instead of `AsyncEnumerable.Empty<T>()` for empty ForMultiple guard. Both are correct; choose the one that matches the actual return type. |
| H7 | Council returns Blocking-severity findings | Apply mechanical amendments; non-mechanical → beacon + halt auto-merge |

---

## §A0 — cited-symbol audit (verified 2026-05-06)

**Positive-existence (existing symbols this hand-off modifies):**
- `AuditQuery` at `packages/foundation/Assets/Audit/AuditQuery.cs` ✓
- `AuditQuery.Tenant : TenantId?` currently `TenantId? Tenant = null,` (line 9) ✓
- `EntityQuery` at `packages/foundation/Assets/Entities/EntityQuery.cs` ✓
- `EntityQuery.Tenant : TenantId?` currently `TenantId? Tenant = null,` (line 8) ✓
- `ExportRequest.TenantId : TenantId?` at `packages/foundation-localfirst/DataExport.cs` (line 25) ✓
- `InMemoryAuditLog` pattern at line 81: `query.Tenant is not { } tenant || r.Tenant == tenant` ✓
- `InMemoryEntityStore` pattern at line 385: `query.Tenant is { } tenant && !string.Equals(...)` ✓
- `PostgresAuditLog` pattern at line 110: `if (query.Tenant is { } tenant)` ✓
- `PostgresEntityStore` pattern at line 359: `if (query.Tenant is { } tenant)` ✓

**Dependency (introduced by WS-A — must exist before building WS-B):**
- `TenantSelection` at `packages/foundation-multitenancy/TenantSelection.cs` — introduced by WS-A ✓ per ADR 0084 §2
- `TenantSelection.Matches(TenantId)` — introduced by WS-A (pre-included); verify H6

**Explicitly NOT migrated (per ADR 0085 §A0):**
- `kernel-audit/AuditQuery.TenantId` — non-nullable `TenantId TenantId` per ADR 0049 v0; excluded by design

---

## What this unblocks

- **Phase 2 multi-tenant queries** — `ForMultiple` path now available in `AuditQuery` /
  `EntityQuery` / `ExportRequest`; foundation for cross-tenant dashboards + bulk exports.
- **W#46 Phase 3 cache invalidation** — `DefaultPermissionResolver` can cache keyed on
  `TenantSelection` (once `IStandingOrderEventStream` lands via W#57).
- **W#37 Phase 2** — tenant security policy queries that need `ForMultiple` audit scans.
