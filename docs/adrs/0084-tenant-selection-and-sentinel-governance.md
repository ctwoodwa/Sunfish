---
id: 84
title: "TenantId Sentinel Governance + TenantSelection Introduction (W#1 WS-A)"
status: Accepted
date: 2026-05-05
tier: foundation
pipeline_variant: sunfish-feature-change
concern:
  - multi-tenancy
  - version-management
enables:
  - tenant-selection-query-scope
composes: []
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: []
---

# ADR 0084 — TenantId Sentinel Governance + TenantSelection Introduction (W#1 WS-A)

**Status:** Proposed
**Date:** 2026-05-05
**Resolves:** W#1 Stage 01 Discovery (`icm/01_discovery/output/2026-05-05_multi-tenancy-type-surface.md`);
intake `icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md`

---

## A0 Cited-symbol audit

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Assets.Common.TenantId` | Existing (modified: + `System` sentinel, `__` guard, `Default` → Obsolete) | yes |
| `Sunfish.Foundation.MultiTenancy.TenantSelection` | Introduced by this ADR | yes |
| `Sunfish.Foundation.MultiTenancy.TenantSelection.ForSingle` | Introduced by this ADR | yes |
| `Sunfish.Foundation.MultiTenancy.TenantSelection.ForMultiple` | Introduced by this ADR | yes |
| `Sunfish.Foundation.MultiTenancy.TenantSelection.AllAccessible` | Introduced by this ADR | yes |
| `Sunfish.Foundation.MultiTenancy.IMayHaveTenant` | Existing (marked `[Obsolete]`) | yes |
| `Sunfish.Foundation.Assets.Audit.NullAuditContextProvider` | Existing (migrated from `TenantId.Default` → `TenantId.System`) | yes |
| `Sunfish.Foundation.Assets.Audit.IAuditContextProvider` | Existing (unchanged contract) | yes |
| `Sunfish.Foundation.MultiTenancy.IMustHaveTenant` | Existing (unchanged) | yes |
| `SUNFISH_TENANTID001` | Introduced by this ADR (Phase 2 Roslyn analyzer) | yes |
| `packages/foundation/Assets/Common/TenantId.cs` | Existing file (modified) | yes |
| `packages/foundation-multitenancy/` | Existing package (new file added) | yes |

---

## Context

`TenantId` was introduced as a simple `readonly record struct` wrapping a `string Value`. It has one
static sentinel — `TenantId.Default = new("default")` — which carries at least three distinct semantic
uses: (1) "this operation runs in system/kernel context" (per `NullAuditContextProvider.GetTenant()`);
(2) "no explicit tenant was supplied" (per `DataImport.TargetTenantId` null-semantics); (3) informally,
"this entity pre-dates per-tenant tracking." The three readings conflict at the type level.

Compounding this, twelve production sites use `TenantId?` (nullable) with `null` meaning "any or all
tenants for this query" — conflating *record identity* with *query scope*. This conflation creates
authorization surface risk: a query with `TenantId? Tenant = null` silently means "all tenants the
caller can access," but the null check is easily forgotten.

`IMayHaveTenant` was defined in `foundation-multitenancy` as the interface for "possibly tenant-scoped"
entities. It has zero production implementations; the `IMustHaveTenant` pattern won instead (13
implementations). The vestigial interface adds confusion with no benefit.

Stage 01 Discovery (2026-05-05) resolved all 8 sub-items from the intake and identified a
**two-workstream** resolution:

- **WS-A (this ADR):** Non-breaking additions and obsolescence marks. Add `TenantId.System`
  sentinel, add `TenantSelection` discriminated union, mark `TenantId.Default` and
  `IMayHaveTenant` obsolete.
- **WS-B (separate api-change ADR):** Migrate three query-type sites
  (`AuditQuery.Tenant`, `EntityQuery.Tenant`, `DataExport.TenantId`) from `TenantId?` to
  `TenantSelection?`. Breaking change; gated on this ADR reaching Accepted.

---

## Decision drivers

- `TenantId.Default` has overloaded semantics that produce subtle authorization bugs if a
  "system" sentinel leaks into a tenant-scoped filter path
- No first-class multi-tenant query scope type exists; `TenantId? = null` as a proxy is
  invisible to the compiler's non-null safety analysis
- `IMayHaveTenant` is a dead interface (0 implementations) that creates API surface noise
- WS-A must be non-breaking: all existing code continues to compile without changes (obsolete
  warnings permitted; errors not permitted)
- `kernel-audit/AuditQuery.TenantId` (required, non-nullable, per ADR 0049 v0) must NOT
  change in this ADR — v0 per-tenant audit reads are intentionally single-tenant

---

## Considered options

### Option A — Sentinel only (no TenantSelection)

Add `TenantId.System`, mark `Default` obsolete. Do nothing about the 12 `TenantId?` query sites.

**Pro:** Minimal blast radius. **Con:** Query-scope problem persists indefinitely; WS-B
never has a migration target. **Rejected.**

### Option B — TenantSelection as class hierarchy [RECOMMENDED]

Abstract record + private constructor for closed discriminated union. Add alongside sentinel
clarification in one ADR.

**Pro:** Sealed union; pattern-matchable in C# 9+ `switch` expressions; private constructor
prevents external subclassing; factory helpers; implicit `TenantId → TenantSelection`
conversion for migration ergonomics. **Con:** Slightly more boilerplate than an enum approach.
**Chosen.**

### Option C — Enum + wrapper (TenantScope enum + TenantIds array)

```csharp
public enum TenantScope { Single, Multiple, AllAccessible }
public readonly record struct TenantSelection(TenantScope Scope, IReadOnlyList<TenantId> TenantIds);
```

**Pro:** Simple; no abstract class. **Con:** Scope + Ids can be mismatched (e.g., `Scope=Single,
Ids.Count=3`); enforcing invariants requires additional validation that the abstract record gives
for free via private constructor. **Rejected.**

---

## Decision

**Adopt Option B (abstract record discriminated union) alongside `TenantId.System` sentinel.**

### §1 TenantId changes

```csharp
// packages/foundation/Assets/Common/TenantId.cs
namespace Sunfish.Foundation.Assets.Common;

public readonly record struct TenantId
{
    private const string ReservedPrefix = "__";

    // init accessor (not get-only) required to enable the CreateSentinel private factory below.
    // External misuse via 'new TenantId { Value = "__x__" }' is caught by the
    // SUNFISH_TENANTID001 Roslyn analyzer (Phase 2 of this ADR).
    public string Value { get; init; }

    public TenantId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        // Literal string — do NOT reference TenantId.System.Value here (static-init cycle).
        if (value.StartsWith(ReservedPrefix, StringComparison.Ordinal) &&
            value != "__system__")
            throw new ArgumentException(
                $"TenantId values starting with '{ReservedPrefix}' are reserved for " +
                $"Sunfish-managed sentinels. Received: '{value}'.",
                nameof(value));
        Value = value;
    }

    public override string ToString() => Value;

    public static implicit operator TenantId(string value) => new(value);
    public static implicit operator string(TenantId id) => id.Value;

    public static TenantId System { get; } = CreateSentinel("__system__");

    [Obsolete("Ambiguous sentinel. Use TenantId.System for system/kernel context, or " +
              "TenantId? (nullable) for 'no explicit tenant' semantics. " +
              "Will be removed in the WS-B api-change wave. See ADR 0084.")]
    public static TenantId Default { get; } = CreateSentinel("default");

    private static TenantId CreateSentinel(string value) => new() { Value = value };
}
```

### §2 TenantSelection type (new file)

```csharp
// packages/foundation-multitenancy/TenantSelection.cs
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Sunfish.Foundation.MultiTenancy;

/// <summary>
/// Discriminated union expressing the tenant scope of a query or bulk operation.
/// Distinct from <see cref="TenantId"/> (record identity): a stored record always has
/// exactly one <c>TenantId</c>; a query may span zero, one, or many tenants.
/// </summary>
/// <remarks>
/// Expansion of <see cref="AllAccessible"/> is performed by the authorization layer
/// (e.g., <c>IPolicyEvaluator</c>); <c>TenantSelection</c> itself is a value object —
/// it does not resolve tenants and has no dependencies.
/// </remarks>
public abstract record TenantSelection
{
    private TenantSelection() { }

    /// <summary>Query scope is exactly one tenant.</summary>
    public sealed record ForSingle(TenantId Tenant) : TenantSelection;

    /// <summary>
    /// Query scope is an explicit non-empty ordered set of tenants.
    /// Empty list is not valid (use <see cref="AllAccessible"/> for unrestricted scope).
    /// </summary>
    public sealed record ForMultiple : TenantSelection
    {
        public ImmutableArray<TenantId> Tenants { get; }

        public ForMultiple(IEnumerable<TenantId> tenants)
        {
            var arr = tenants?.ToImmutableArray()
                      ?? throw new ArgumentNullException(nameof(tenants));
            if (arr.IsEmpty)
                throw new ArgumentException(
                    "ForMultiple requires at least one tenant. Use TenantSelection.All " +
                    "for unrestricted scope.", nameof(tenants));
            Tenants = arr;
        }

        public bool Equals(ForMultiple? other) =>
            other is not null && Tenants.SequenceEqual(other.Tenants);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (var t in Tenants) hc.Add(t);
            return hc.ToHashCode();
        }
    }

    /// <summary>
    /// Query scope is all tenants the current principal can access
    /// <em>at request wall-clock time</em> (macaroon <c>expires_at</c> caveats honored).
    /// This is the v0 "accessible now" (dashboard) semantic.
    /// "Accessible at record-creation time" and "ever accessible" are deferred to
    /// future ADR amendments when the authorization layer matures.
    /// </summary>
    public sealed record AllAccessible : TenantSelection;

    // ---- Factory helpers ----

    /// <summary>Scope for exactly one tenant.</summary>
    public static TenantSelection Of(TenantId tenant) => new ForSingle(tenant);

    /// <summary>
    /// Scope for the provided set. Collapses to <see cref="ForSingle"/> when given
    /// exactly one tenant; uses <see cref="ForMultiple"/> for two or more.
    /// </summary>
    public static TenantSelection Of(params TenantId[] tenants)
    {
        if (tenants is null || tenants.Length == 0)
            throw new ArgumentException("At least one tenant is required.", nameof(tenants));
        return tenants.Length == 1
            ? new ForSingle(tenants[0])
            : new ForMultiple(tenants);
    }

    /// <summary>Scope for all tenants accessible to the current principal.</summary>
    public static readonly TenantSelection All = new AllAccessible();

    // Lives on TenantSelection (not TenantId) to avoid a circular package dependency:
    // foundation-multitenancy → foundation (fine); foundation → foundation-multitenancy
    // (would be circular). C# allows user-defined conversions on the target type.
    public static implicit operator TenantSelection(TenantId id) => Of(id);
}
```

### §3 IMayHaveTenant obsolescence

```csharp
// packages/foundation-multitenancy/ITenantScoped.cs — existing interface
[Obsolete("IMayHaveTenant has zero production implementations. Use TenantId? directly " +
          "on types where tenant identity can be absent, or TenantSelection for multi-tenant " +
          "query scope. This interface will be removed in a future api-change wave. " +
          "See ADR 0084.")]
public interface IMayHaveTenant
{
    TenantId? TenantId { get; }
}
```

### §4 NullAuditContextProvider migration

```csharp
// packages/foundation/Assets/Audit/IAuditContextProvider.cs
// NullAuditContextProvider is co-located in this file alongside the IAuditContextProvider
// interface definition (Sunfish convention: null-object + interface in one file).
public sealed class NullAuditContextProvider : IAuditContextProvider
{
    public static NullAuditContextProvider Instance { get; } = new();
    public ActorId GetActor() => ActorId.System;
#pragma warning disable CS0618  // suppress Obsolete warning for Default during migration
    public TenantId GetTenant() => TenantId.System;   // was TenantId.Default
#pragma warning restore CS0618
}
```

> The `#pragma` suppression is NOT needed for `TenantId.System` — only for any code that still
> references `TenantId.Default` before WS-B lands. The pragma is shown here for clarity; actual
> implementation uses `TenantId.System` directly (no suppression needed).

---

## Consequences

### Positive

- `TenantId.System` semantics are unambiguous: system/kernel context only
- `IMayHaveTenant` obsolescence clears dead API surface; zero migration cost (0 implementations)
- `TenantSelection` gives WS-B a migration target and future multi-tenant query paths a
  first-class type with compiler visibility
- Implicit `TenantId → TenantSelection` cast lets WS-B call sites migrate one-by-one without
  boilerplate: `query.Tenant = someTenantId` compiles without change after WS-B migrates
  the property type

### Negative

- `TenantId.Default` emits `[Obsolete]` warnings immediately; projects with
  `TreatWarningsAsErrors` need a `#pragma` suppression until their WS-B migration
- `TenantSelection.ForMultiple` ordering is caller-determined (no de-duplication); duplicate
  tenants are caller's responsibility (documented)

### Trust impact / Security & privacy

- **Reserved prefix guard** prevents user-supplied `TenantId` values from colliding with
  Sunfish-managed sentinels. This is a correctness guard, not a security boundary: `TenantId`
  values are not access-control tokens.
- **`init` accessor + `SUNFISH_TENANTID001` (Phase 2):** `TenantId.Value` uses `init` (not
  `get`-only) to support the `CreateSentinel` factory. This means `new TenantId { Value =
  "__system__" }` compiles but bypasses the constructor guard. Phase 2 ships the
  `SUNFISH_TENANTID001` Roslyn analyzer (Error severity) to flag this object-initializer
  pattern outside `TenantId.cs`. Until Phase 2 ships, the bypass is documented; no legitimate
  caller sets `Value` directly — they use the string constructor or implicit cast.
- **`TenantSelection.AllAccessible` is a scope declaration, not a capability grant.** The
  authorization layer (`IPolicyEvaluator` + macaroon caveats) is responsible for expanding
  `AllAccessible` to the concrete tenant set the principal can access. If the authorization
  layer is misconfigured, `AllAccessible` could surface unauthorized data. This risk already
  exists with `TenantId? = null` in today's code; `TenantSelection` makes the scope explicit
  and analyzer-checkable in the future.

---

## Compatibility plan

**Non-breaking.** No existing callers are required to change.

- `TenantId.Default` emits `[Obsolete]` warnings. Not removed until WS-B api-change wave.
- `IMayHaveTenant` emits `[Obsolete]` warnings. Not removed until a future api-change wave.
- `TenantId(string value)` constructor: values NOT starting with `"__"` are unchanged.
  Applications that construct `TenantId` from user-supplied strings are unaffected.
- All 13 `IMustHaveTenant` implementations are unchanged.
- All 9 Group B `TenantId?` sites (record-identity nullable) are unchanged.

**Packages touched (additions / modifications only):**

| Package | Change |
|---|---|
| `packages/foundation/` | `TenantId.cs`: + `System` sentinel, + `__` guard, `Default` → Obsolete |
| `packages/foundation-multitenancy/` | New `TenantSelection.cs` (includes `TenantId → TenantSelection` implicit cast on target type); `ITenantScoped.cs`: `IMayHaveTenant` → Obsolete |
| `packages/foundation/Assets/Audit/` | `NullAuditContextProvider.GetTenant()` → `TenantId.System` |

---

## Implementation checklist

**Phase 1 — foundation types (1 PR)**

- [ ] Verify `TenantId` is `readonly record struct` (not class); use private factory `CreateSentinel(string)` for reserved values
- [ ] Add `TenantId.System = CreateSentinel("__system__")` static property with XML doc
- [ ] Add `__` reserved-prefix guard in `TenantId(string value)` constructor; throw `ArgumentException` with message including the received value
- [ ] Mark `TenantId.Default` `[Obsolete(...)]` per §1 message text
- [ ] Add implicit `operator TenantSelection(TenantId id)` to `TenantSelection.cs` per §2 (lives on target type to avoid circular `foundation → foundation-multitenancy` dependency; both files ship in one PR)
- [ ] Create `packages/foundation-multitenancy/TenantSelection.cs` per §2 exact spec
- [ ] Mark `IMayHaveTenant` `[Obsolete(...)]` per §3 message text in `ITenantScoped.cs`
- [ ] Migrate `NullAuditContextProvider.GetTenant()` to return `TenantId.System` per §4

**Phase 1 tests**

- [ ] Unit: `new TenantId("__custom__")` → `ArgumentException`
- [ ] Unit: `new TenantId("regular-tenant")` → no throw
- [ ] Unit: `TenantId.System.Value == "__system__"` → true
- [ ] Unit: `TenantSelection.Of(tenantId)` returns `ForSingle`
- [ ] Unit: `TenantSelection.Of(t1, t2)` returns `ForMultiple` with 2 tenants
- [ ] Unit: `TenantSelection.Of(t1, t1)` returns `ForMultiple` (no de-dup; duplicate allowed; documented)
- [ ] Unit: two `ForMultiple` instances constructed with the same tenant list compare equal (`Equals` + `GetHashCode` parity via `SequenceEqual`)
- [ ] Unit: `new TenantSelection.ForMultiple(new List<TenantId>())` → `ArgumentException`
- [ ] Unit: `TenantSelection.Of()` (empty params) → `ArgumentException`
- [ ] Unit: `(TenantSelection)tenantId` implicit cast → `ForSingle`
- [ ] Unit: `NullAuditContextProvider.Instance.GetTenant() == TenantId.System` → true
- [ ] `dotnet build` across all packages: 0 errors; only `[Obsolete]` warnings on `TenantId.Default` + `IMayHaveTenant` references (expected — document which packages emit warnings in PR description)
- [ ] Pre-merge 4-subagent council (adversarial + security + WCAG/a11y + Pedantic Lawyer)

**Phase 2 (future — WS-B api-change ADR)**

- [ ] Migrate `foundation/Assets/Audit/AuditQuery.Tenant` from `TenantId?` to `TenantSelection?`
- [ ] Migrate `foundation/Assets/Entities/EntityQuery.Tenant` from `TenantId?` to `TenantSelection?`
- [ ] Migrate `foundation-localfirst/DataExport.TenantId` from `TenantId?` to `TenantSelection?`
- [ ] Remove `TenantId.Default`
- [ ] Remove `IMayHaveTenant`
- [ ] Update any `#pragma CS0618` suppressions introduced during Phase 1 deprecation window

---

## Open questions

**OQ-1: `TenantId` struct inheritance** — `TenantId` is a `readonly record struct`; C# structs
cannot be subclassed. The reserved-sentinel bypass must use a different mechanism than the
`TenantId_Sentinel` derived-type sketch in §1. Recommended implementation: private static
`CreateSentinel(string value)` factory that initializes via `new TenantId { Value = value }` using
the auto-generated record copy constructor (sets fields directly, bypassing the parameterized
constructor guard). COB should verify this compiles without reflection hacks.

**OQ-2: `TenantId.Default` warning scope** — The `[Obsolete]` attribute emits CS0618 warnings at
every reference site. The discovery found no known `TenantId.Default` references outside
`NullAuditContextProvider` (which Phase 1 migrates) and test code. COB should run a grep pass
(`grep -rn "TenantId\.Default" packages/`) after migration and list any remaining sites in the
PR description; each becomes a WS-B migration ticket.

**OQ-3: `TenantSelection` JSON serialization** — `TenantSelection` is an abstract record with
discriminated cases. It is NOT a DTO today — it is used at the application/query layer.
If it ever needs JSON round-tripping (e.g., query parameters in Bridge HTTP endpoints),
a custom `JsonConverter` will be needed. **Deferred until a concrete HTTP endpoint needs
`TenantSelection` as a query param.** Do NOT ship a converter in Phase 1.

---

## Revisit triggers

- `TenantSelection.AllAccessible` "at record-creation time" sibling needed (regulatory-investigation query pattern surfaces in a real use case)
- WS-B api-change wave ships — triggers `TenantId.Default` + `IMayHaveTenant` removal
- `IPolicyEvaluator` gets a formal contract surface (own ADR) — update §2 doc comment to cite it

---

## Pre-acceptance audit

- [x] **AHA pass.** Considered enum-based approach (Option C) and sentinel-only (Option A); both rejected with rationale.
- [x] **FAILED conditions / kill triggers.** Kill: `new TenantId("__foo__")` must throw; implicit cast must not introduce ambiguity in overloaded call sites (verify before merge).
- [x] **Rollback strategy.** Revert the `TenantId.cs` and `TenantSelection.cs` changes; no migration needed (non-breaking addition).
- [x] **Confidence level.** HIGH — Stage 01 Discovery surveyed all 12 `TenantId?` sites and verified `IMayHaveTenant` has 0 implementations.
- [x] **Cited-symbol verification.** All symbols in §A0 verified against actual file paths (Explore subagent pass, 2026-05-05).
- [x] **Anti-pattern scan.** AP-21 (cited-symbol drift) mitigated by §A0. No AP-12 (timeline fantasy): Phase 1 is a single PR. No AP-1 (unvalidated assumptions): all assumptions verified in Stage 01 Discovery.
- [x] **Revisit triggers.** Named above (§Revisit triggers).
- [x] **Cold Start Test.** Implementation checklist is file-by-file with exact method names and expected test outcomes.
- [x] **Sources cited.** Stage 01 Discovery doc; intake 2026-04-28; ADR 0049 (kernel-audit v0 per-tenant decision).

---

## References

### Predecessor ADRs

- [ADR 0049](./0049-kernel-audit-v0.md) — kernel-audit v0 per-tenant scoping decision; explains why `kernel-audit/AuditQuery.TenantId` stays non-nullable

### Discovery and intake

- `icm/01_discovery/output/2026-05-05_multi-tenancy-type-surface.md` — Stage 01 findings (verified all 12 `TenantId?` sites; confirmed 0 `IMayHaveTenant` implementations)
- `icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md` — original intake (8 sub-items; all resolved by this ADR + WS-B)

### Existing code

- `packages/foundation/Assets/Common/TenantId.cs` — current `TenantId` definition
- `packages/foundation-multitenancy/ITenantScoped.cs` — `IMayHaveTenant` + `IMustHaveTenant`
- `packages/foundation/Assets/Audit/IAuditContextProvider.cs` — `NullAuditContextProvider`
