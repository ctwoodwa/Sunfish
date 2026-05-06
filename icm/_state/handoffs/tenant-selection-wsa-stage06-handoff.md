# W#1 WS-A Stage 06 Hand-Off — TenantId Sentinel + TenantSelection Introduction (ADR 0084)

**Workstream:** W#1 WS-A  
**ADR:** [0084 — TenantId Sentinel Governance + TenantSelection Introduction](../../../docs/adrs/0084-tenant-selection-and-sentinel-governance.md)  
**Status:** `design-in-flight` — flip to `ready-to-build` when ADR 0084 status flips to Accepted  
**Packages modified:** `packages/foundation/` + `packages/foundation-multitenancy/`  
**Authored:** 2026-05-06 (XO research session; pre-authored pending CO acceptance of ADR 0084)  
**Build estimate:** ~3–4h / 1 PR  

---

## Critical context

**GATE:** ADR 0084 must be `status: Accepted` before starting. Verify:

```bash
grep "^status:" docs/adrs/0084-tenant-selection-and-sentinel-governance.md
# EXPECT: status: Accepted
```

If it says `Proposed`, STOP. Do NOT build. ADR acceptance is CO's action — a merged PR ≠ Accepted.

This workstream is **non-breaking**. It adds new symbols and marks two existing symbols
`[Obsolete]` — it does NOT remove them. WS-B (ADR 0085) is the breaking migration wave;
it gates on WS-A being built.

---

## Prerequisites verification checklist

```bash
# H1: TenantId is readonly record struct (NOT class — cannot subclass)
grep -n "readonly record struct TenantId\|public readonly record struct TenantId" \
  packages/foundation/Assets/Common/TenantId.cs
# EXPECT: one hit

# H2: TenantId.System does NOT yet exist (this phase introduces it)
grep -n "System" packages/foundation/Assets/Common/TenantId.cs
# EXPECT: zero hits for "static.*System\|TenantId.System"

# H3: TenantSelection.cs does NOT yet exist
find packages/foundation-multitenancy -name "TenantSelection.cs" | head -1
# EXPECT: empty

# H4: NullAuditContextProvider exists (this phase updates it)
grep -rn "NullAuditContextProvider" packages/foundation/Assets/Audit/ | head -3
# EXPECT: ≥1 hit

# H5: IMayHaveTenant exists (this phase marks it Obsolete)
grep -rn "IMayHaveTenant" packages/foundation-multitenancy/ | head -3
# EXPECT: ≥1 hit in ITenantScoped.cs
```

---

## Phase 1 — Foundation types (1 PR, ~3–4h)

**Gate:** All H1–H5 pass AND ADR 0084 status = Accepted.

### File 1: `packages/foundation/Assets/Common/TenantId.cs` — 3 edits

Per ADR 0084 §1.

**Edit 1 — Add `System` sentinel static property.** Add after the existing static property
block (keep alphabetical order if other static properties exist):

```csharp
/// <summary>
/// Sentinel representing the Sunfish system actor (background jobs, migrations,
/// server-side processes). Use instead of <c>TenantId.Default</c> for system context.
/// Per ADR 0084 §1.
/// </summary>
public static readonly TenantId System = CreateSentinel("__system__");
```

**Edit 2 — Add `__` reserved-prefix guard in constructor.** At the top of the
`TenantId(string value)` constructor body, before any existing guard:

```csharp
if (value.StartsWith("__", StringComparison.Ordinal))
    throw new ArgumentException(
        $"TenantId values may not start with '__' (received: '{value}'). " +
        "The '__' prefix is reserved for Sunfish system sentinels.",
        nameof(value));
```

**Edit 3 — Mark `Default` `[Obsolete]`.** On the `Default` static property:

```csharp
[Obsolete("Use TenantId.System for system/background-job context, or a real " +
          "tenant id for tenant-scoped operations. TenantId.Default will be " +
          "removed in WS-B (ADR 0085). Per ADR 0084 §1.")]
public static readonly TenantId Default = new("00000000-0000-0000-0000-000000000000");
```

**Edit 4 — Add `CreateSentinel` private factory.** Per ADR 0084 §1 OQ-1: C# `readonly record
struct` cannot be subclassed; use private static factory that bypasses the constructor guard:

```csharp
private static TenantId CreateSentinel(string value) => new TenantId { Value = value };
```

`Value` must be `get; init;` for this to compile. Verify the existing record uses
`public string Value { get; init; }` (not `get;` only). If it is `get;` only, change to
`get; init;` — this is source-compatible (init-only setters are invisible to callers
after construction).

---

### File 2: `packages/foundation-multitenancy/TenantSelection.cs` — new file

Per ADR 0084 §2. **Lives in `foundation-multitenancy`** (NOT `foundation`) to avoid circular
`foundation → foundation-multitenancy` dependency. The implicit cast operator lives here
on the target type.

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.MultiTenancy;

/// <summary>
/// Discriminated union expressing a multi-tenant query scope.
/// Per ADR 0084 §2.
/// </summary>
/// <remarks>
/// Use <see cref="Of(TenantId)"/> / <see cref="Of(IEnumerable{TenantId})"/> factory
/// overloads. Direct case construction is allowed but prefer the factories for
/// forward-compat.
/// <para>
/// JSON serialization is NOT supported in Phase 1 — <c>TenantSelection</c> is an
/// application/query-layer type, not a DTO. If a Bridge HTTP endpoint ever needs
/// <c>TenantSelection</c> as a query param, a custom <c>JsonConverter</c> will be
/// authored in a follow-up ADR (deferred per ADR 0084 OQ-3).
/// </para>
/// </remarks>
public abstract record TenantSelection
{
    private TenantSelection() { }

    /// <summary>Exactly one tenant in scope.</summary>
    public sealed record ForSingle(TenantId TenantId) : TenantSelection;

    /// <summary>
    /// An explicit set of tenants in scope (≥1 member). Empty set throws at construction.
    /// </summary>
    public sealed record ForMultiple(ImmutableArray<TenantId> TenantIds) : TenantSelection
    {
        public ForMultiple(IEnumerable<TenantId> tenantIds) : this(tenantIds.ToImmutableArray())
        {
            if (TenantIds.Length == 0)
                throw new ArgumentException(
                    "ForMultiple requires at least one TenantId.", nameof(tenantIds));
        }

        public virtual bool Equals(ForMultiple? other) =>
            other is not null && TenantIds.SequenceEqual(other.TenantIds);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var id in TenantIds) hash.Add(id);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// All tenants accessible to the requesting actor (platform-admin queries).
    /// Caller is responsible for verifying the actor holds the required capability
    /// before constructing this instance.
    /// </summary>
    public sealed record AllAccessible : TenantSelection;

    /// <summary>
    /// Factory: single tenant. Prefer over <c>new ForSingle(tenantId)</c> for
    /// forward-compat.
    /// </summary>
    public static TenantSelection Of(TenantId tenantId) => new ForSingle(tenantId);

    /// <summary>
    /// Factory: explicit set of tenants (≥1). Throws if empty.
    /// </summary>
    public static TenantSelection Of(IEnumerable<TenantId> tenantIds)
    {
        var arr = tenantIds.ToImmutableArray();
        return arr.Length == 1
            ? new ForSingle(arr[0])
            : new ForMultiple(arr);
    }

    /// <summary>
    /// Convenience varargs overload. Throws if zero arguments.
    /// </summary>
    public static TenantSelection Of(params TenantId[] tenantIds)
    {
        if (tenantIds.Length == 0)
            throw new ArgumentException("At least one TenantId required.", nameof(tenantIds));
        return Of((IEnumerable<TenantId>)tenantIds);
    }

    /// <summary>
    /// Implicit cast from <see cref="TenantId"/> to <see cref="TenantSelection"/>
    /// (produces <see cref="ForSingle"/>). Lives on the target type to avoid a circular
    /// <c>foundation → foundation-multitenancy</c> package dependency.
    /// </summary>
    public static implicit operator TenantSelection(TenantId id) => new ForSingle(id);

    /// <summary>
    /// Returns true if this selection includes <paramref name="tenantId"/>.
    /// Used by in-memory stores and query engines; SQL implementations use
    /// the ForSingle/ForMultiple structural match + parameter array.
    /// Added in WS-B (ADR 0085) — stub provided here as forward-compat.
    /// </summary>
    public bool Matches(TenantId tenantId) => this switch
    {
        ForSingle s => s.TenantId == tenantId,
        ForMultiple m => m.TenantIds.Contains(tenantId),
        AllAccessible => true,
        _ => throw new InvalidOperationException(
            $"Unknown TenantSelection case: {GetType().Name}")
    };
}
```

**Note on `Matches`:** ADR 0085 §Phase1 adds `Matches` as a new method. Including it here
in WS-A avoids a second PR touching the same file. If pre-including `Matches` causes a
council finding, split it out — but the ADR explicitly planned it as a WS-A convenience.

---

### File 3: `packages/foundation-multitenancy/ITenantScoped.cs` — mark `IMayHaveTenant` Obsolete

Per ADR 0084 §3. Find the `IMayHaveTenant` interface and add `[Obsolete]`:

```csharp
[Obsolete("IMayHaveTenant has no active implementors. Use ITenantScoped<T> for " +
          "entities or TenantSelection for query scope. Will be removed in WS-B " +
          "(ADR 0085). Per ADR 0084 §3.")]
public interface IMayHaveTenant { ... }
```

---

### File 4: `packages/foundation/Assets/Audit/IAuditContextProvider.cs` — migrate NullAuditContextProvider

Per ADR 0084 §4. Find `NullAuditContextProvider` (likely nested class or concrete impl):

```csharp
// Before:
public TenantId GetTenant() => TenantId.Default;

// After:
public TenantId GetTenant() => TenantId.System;
```

---

### Add `ProjectReference` if needed

`packages/foundation-multitenancy/Sunfish.Foundation.MultiTenancy.csproj` must reference
`packages/foundation/` to access `TenantId`. Verify it exists:

```bash
grep -l "foundation" packages/foundation-multitenancy/*.csproj
```

If the reference is missing, add:
```xml
<ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
```

---

### Tests to write (in `packages/foundation/tests/` or `packages/foundation-multitenancy/tests/`)

Per ADR 0084 §Implementation checklist. 11 unit tests:

1. `new TenantId("__custom__")` → throws `ArgumentException`
2. `new TenantId("regular-tenant")` → no throw
3. `TenantId.System.Value == "__system__"` → true
4. `TenantSelection.Of(tenantId)` → `ForSingle` with correct `TenantId`
5. `TenantSelection.Of(t1, t2)` → `ForMultiple` with 2 tenants
6. `TenantSelection.Of(t1, t1)` → `ForMultiple` (no de-dup — documented behavior)
7. Two `ForMultiple` instances with same tenant list → `Equals` true + same `GetHashCode`
8. `new TenantSelection.ForMultiple(new List<TenantId>())` → `ArgumentException`
9. `TenantSelection.Of()` (empty params) → `ArgumentException`
10. `(TenantSelection)tenantId` implicit cast → `ForSingle`
11. `NullAuditContextProvider.Instance.GetTenant() == TenantId.System` → true

---

## Acceptance criteria

- [ ] `grep "TenantId.System" packages/foundation/Assets/Common/TenantId.cs` returns the new property
- [ ] `find packages/foundation-multitenancy -name "TenantSelection.cs"` returns ≥1
- [ ] `grep "\[Obsolete" packages/foundation/Assets/Common/TenantId.cs` marks `Default`
- [ ] `grep "\[Obsolete" packages/foundation-multitenancy/ITenantScoped.cs` marks `IMayHaveTenant`
- [ ] `grep "TenantId.System" packages/foundation/Assets/Audit/IAuditContextProvider.cs` shows migration
- [ ] All 11 new unit tests pass
- [ ] `dotnet build` across all packages: 0 errors; CS0618 warnings appear ONLY on `TenantId.Default`
  and `IMayHaveTenant` existing call sites — document these sites in the PR description
- [ ] Pre-merge 4-subagent council (adversarial + security + WCAG/a11y + Pedantic Lawyer)
  per ADR 0084 §Implementation checklist

---

## Halt conditions

| # | Condition | Action |
|---|---|---|
| H1 | `TenantId` is a class (not struct) | Write `cob-question` beacon; halt — structural assumption violated |
| H2 | `TenantId.Value` is `get;` only (not `get; init;`) | Change to `get; init;` — source-compat; document in PR |
| H3 | `CreateSentinel` doesn't compile (CLR or Roslyn version) | Try `with { Value = value }` syntax; if both fail write beacon |
| H4 | `TenantId.System` / `TenantId.Default` cause static-init cycle | Check static field ordering; `System` must be declared BEFORE `Default` (or vice versa — neither depends on the other so order shouldn't matter; if it does, write beacon) |
| H5 | Council returns Blocking-severity findings | Apply mechanical; non-mechanical → beacon + halt auto-merge |
| H6 | `IMayHaveTenant` has ≥1 concrete implementor on origin/main | Write `cob-question` beacon — scope must expand to cover migration; halt |

---

## §A0 — cited-symbol audit (verified 2026-05-06)

**Negative-existence (introduced by this hand-off):**
- `TenantId.System` — `grep "static.*System" packages/foundation/Assets/Common/TenantId.cs` = ZERO ✓
- `TenantSelection` — `find packages/foundation-multitenancy -name "TenantSelection.cs"` = empty ✓
- `__` guard in `TenantId(string)` constructor = ZERO ✓

**Positive-existence (existing symbols this hand-off edits):**
- `TenantId` at `packages/foundation/Assets/Common/TenantId.cs` ✓
- `TenantId.Default` — currently active; being marked Obsolete ✓
- `ITenantScoped.cs` + `IMayHaveTenant` at `packages/foundation-multitenancy/` ✓
- `IAuditContextProvider.cs` + `NullAuditContextProvider` at `packages/foundation/Assets/Audit/` ✓

---

## What this unblocks

- **WS-B (ADR 0085)** — `TenantSelection.Matches(TenantId)` method + query-layer migrations.
  `TenantSelection` must exist on origin/main before WS-B can build.
- **W#46 Phase 1 TODO** — `TenantId.System` swap for current `TenantId.Default` fallback
  in `DefaultPermissionResolver` (noted in ledger as deferred TODO item).
