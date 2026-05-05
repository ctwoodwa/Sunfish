# Multi-Tenancy Type Surface — Stage 01 Discovery

**Stage:** 01 Discovery
**Pipeline:** `sunfish-feature-change` (escalates to `sunfish-api-change` for query-type migration; see §6.3)
**Date:** 2026-05-05
**Author:** XO research session
**Status:** Discovery complete — Stage 02 Architecture (ADR authoring) is next
**Intake:** `icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md`
**Active workstream:** W#1 in `icm/_state/active-workstreams.md`

---

## 1. Executive Summary

Sunfish's multi-tenancy type surface has two interrelated problems: (1) a single overloaded sentinel
(`TenantId.Default`) carries three distinct semantics and a deprecated interface (`IMayHaveTenant`)
that was never implemented; (2) no first-class type exists for multi-tenant query scope, so 12
production sites use `TenantId?` with "null = all/any" semantics — conflating record-identity with
query-scope.

**Recommended resolution (two sequenced workstreams):**

| # | Workstream | Pipeline variant | Scope |
|---|---|---|---|
| A | **Sentinel clarification + TenantSelection introduction** | `sunfish-feature-change` | Add `TenantId.System` sentinel; add `TenantSelection` discriminated union; mark `IMayHaveTenant` obsolete. Non-breaking. |
| B | **Query-type migration** | `sunfish-api-change` | Migrate `AuditQuery.Tenant`, `EntityQuery.Tenant`, `DataExport.TenantId` from `TenantId?` → `TenantSelection?`. Breaking; gated on Workstream A landing. |

Workstream A is the Stage 02 ADR target for W#1. Workstream B becomes a separate W#1.1 api-change
workstream (filed after ADR acceptance). The kernel-audit Tier 2 retrofit (W#2's pending Tier 2
step: `AuditQuery.TenantId → TenantSelection`) depends on both A and B landing.

---

## 2. Research Questions (from intake)

The intake named 8 concrete sub-items:

| # | Sub-item | Status after discovery |
|---|---|---|
| M1 | `TenantId` reserved-sentinel namespace (prefix or other) | **Resolved** → `__`-prefix reserved; validated at construction |
| M2 | Sentinel set decision (`Guest`, `System`, others?) | **Resolved** → Add `System` now; `Guest` deferred (no use case) |
| M3 | `IMayHaveTenant` deprecation stance | **Resolved** → `[Obsolete]` immediately (0 implementations; safe) |
| M4 | `TenantId?` nullable migration plan (13 sites) | **Resolved** → See §4 site-by-site classification |
| M5 | `TenantId.Default` semantics clarification | **Resolved** → `Default` → `[Obsolete]`; `System` is canonical system context |
| M6 | `TenantSelection` value object shape | **Resolved** → discriminated union, see §5 |
| M7 | `TenantSelection.AllAccessible` v0 semantics | **Resolved** → "accessible now" (dashboard semantic); deferred siblings documented |
| M8 | Capability-graph time-awareness expectation | **Documented** → `AllAccessible` must honor macaroon `expires_at`; `IPolicyEvaluator` already does |

---

## 3. Current State of the Codebase

### 3.1 TenantId

**File:** `packages/foundation/Assets/Common/TenantId.cs`

```csharp
public readonly record struct TenantId(string Value)
{
    public override string ToString() => Value;
    public static implicit operator TenantId(string value) => new(value);
    public static implicit operator string(TenantId id) => id.Value;
    public static TenantId Default { get; } = new("default");
}
```

One sentinel only: `TenantId.Default = new("default")`. No `Guest`, `System`, or reserved-prefix
validation. Custom `TenantIdJsonConverter` included.

### 3.2 IMayHaveTenant — zero production implementations

**File:** `packages/foundation-multitenancy/ITenantScoped.cs:25-34`

```csharp
public interface IMayHaveTenant
{
    TenantId? TenantId { get; }
}
```

**Production implementations found:** 0. Referenced only in the XML doc of
`kernel-audit/AuditQuery.cs:16`. Safe to `[Obsolete]` immediately; removal in a follow-on
api-change after a deprecation window.

### 3.3 IMustHaveTenant — 13 production implementations

All 13 are `sealed record` types with a required `TenantId Tenant` property (non-nullable):

| Type | Package |
|---|---|
| `Equipment`, `EquipmentLifecycleEvent` | `blocks-property-equipment` |
| `Property` | `blocks-properties` |
| `BundleActivation`, `TenantUser`, `TenantProfile` | `blocks-tenant-admin` |
| `AuditRecord` | `kernel-audit` |
| `BundleActivationRecord` | `blocks-businesscases` |
| `UsageMeter`, `MeteredUsage`, `Subscription` | `blocks-subscriptions` |
| `WorkOrder` | `blocks-maintenance` |
| `PublicListing` | `blocks-public-listings` |

`IMustHaveTenant` is working correctly; no changes needed to the interface or its 13 implementations.

### 3.4 TenantSelection — not yet defined

No `TenantSelection` type exists anywhere in the codebase. This is a pure addition.

### 3.5 TenantId? — 12 production use sites

Classified by semantics:

#### Group A — Query filter scope (→ migrate to `TenantSelection?`)

| File | Context | Semantics of null |
|---|---|---|
| `foundation/Assets/Audit/AuditQuery.cs:9` | `TenantId? Tenant = null` (record param) | No tenant filter (all accessible) |
| `foundation/Assets/Entities/EntityQuery.cs:8` | `TenantId? Tenant = null` (record param) | No tenant filter (all accessible) |
| `foundation-localfirst/DataExport.cs:25` | `TenantId? TenantId { get; init; }` | System-scope export (all tenants) |

These three sites conflate record-identity with query-scope. `null` here means "query across multiple
tenants" — exactly the multi-tenant query semantics that `TenantSelection` is designed to express.
**Migration path: Workstream B (api-change).** Signature change: `TenantId?` → `TenantSelection?`.

#### Group B — Record-identity / metadata (keep as `TenantId?`)

| File | Context | Semantics of null | Recommendation |
|---|---|---|---|
| `foundation-localfirst/DataImport.cs:9` | `TargetTenantId? TenantId` | Null = "import into system-default tenant" | Keep nullable; clarify via XML doc |
| `foundation-integrations/ISyncCursorStore.cs:14` | `GetAsync(TenantId? tenantId, ...)` | Null = system-level cursor | Keep nullable; clarify via XML doc |
| `foundation-integrations/InMemorySyncCursorStore.cs:14,30` | Same | Same | Keep nullable |
| `foundation-integrations/SyncCursor.cs:16` | `TenantId? TenantId { get; init; }` | Null = system-level cursor | Keep nullable |
| `foundation-integrations/WebhookEventEnvelope.cs:28` | `TenantId? TenantId { get; init; }` | Null = tenant indeterminate | Keep nullable |
| `foundation-featuremanagement/FeatureEvaluationContext.cs:13` | `TenantId? TenantId { get; init; }` | Null = not tenant-bound | Keep nullable |
| `blocks-public-listings/...CapabilityVerifier.cs:126` | Local variable in `ParseCaveats` | Result of conditional parse | Keep as-is (local var) |

These sites use `TenantId?` to mean "this record/event is not scoped to a specific tenant" —
which IS a record-identity concept, not a query-scope concept. `null` here is correct; no change.
XML doc improvements may help clarify intent.

### 3.6 Two AuditQuery types

Both exist simultaneously:

| Type | File | `TenantId` field |
|---|---|---|
| `Sunfish.Foundation.Assets.Audit.AuditQuery` | `foundation/Assets/Audit/AuditQuery.cs` | `TenantId? Tenant = null` — **Group A above** |
| `Sunfish.Kernel.Audit.AuditQuery` | `kernel-audit/AuditQuery.cs` | `TenantId TenantId` — **required; non-nullable** |

The kernel-audit variant deliberately requires a non-nullable `TenantId`: "Required. Audit reads
are tenant-scoped — there is no cross-tenant audit query in v0" (per ADR 0049 v0 design).
This is correct and should NOT change. The Tier 2 retrofit mentioned in W#2 applies ONLY to the
foundation `AuditQuery`, not the kernel variant.

### 3.7 NullAuditContextProvider

```csharp
public sealed class NullAuditContextProvider : IAuditContextProvider
{
    public static NullAuditContextProvider Instance { get; } = new();
    public ActorId GetActor() => ActorId.System;
    public TenantId GetTenant() => TenantId.Default;
}
```

Returns `TenantId.Default` as the system-context sentinel. After Workstream A, this should return
`TenantId.System` instead. `TenantId.Default` becomes `[Obsolete]` in Workstream A; `NullAuditContextProvider`
migration is part of the same PR (a find-replace; non-breaking since `Default` still compiles during
the deprecation window).

---

## 4. Site-by-Site Migration Plan

### Workstream A changes (non-breaking):

1. `TenantId.cs` — add `TenantId.System = new("__system__")`; add reserved-prefix validation;
   mark `TenantId.Default` `[Obsolete("Use TenantId.System for system/kernel context.")]`
2. `ITenantScoped.cs` — mark `IMayHaveTenant` `[Obsolete("No production implementations exist. Use TenantId? directly.")]`
3. New file `foundation-multitenancy/TenantSelection.cs` — discriminated union (see §5)
4. `IAuditContextProvider.cs` / `NullAuditContextProvider.cs` — migrate `TenantId.Default` → `TenantId.System`

### Workstream B changes (breaking; separate api-change ADR):

1. `foundation/Assets/Audit/AuditQuery.cs` — `TenantId? Tenant` → `TenantSelection? Tenant`
2. `foundation/Assets/Entities/EntityQuery.cs` — `TenantId? Tenant` → `TenantSelection? Tenant`
3. `foundation-localfirst/DataExport.cs` — `TenantId? TenantId` → `TenantSelection? Scope`
4. Update all callers (test code + any production consumers) of the above three
5. Update `kernel-audit` Tier 2 (W#2 deferred action): no change to `kernel-audit/AuditQuery.cs`;
   the Tier 2 retrofit is about making the kernel audit surface filter-composable with
   `TenantSelection` at the *query aggregation* layer above the kernel — not changing the kernel
   record itself

---

## 5. TenantSelection Type Design

### Shape

```csharp
// packages/foundation-multitenancy/TenantSelection.cs
// Namespace: Sunfish.Foundation.MultiTenancy

/// <summary>
/// Discriminated union expressing the tenant scope of a query or operation.
/// Distinct from TenantId (record identity): a record has exactly one TenantId;
/// a query may span zero, one, or many tenants.
/// </summary>
public abstract record TenantSelection
{
    private TenantSelection() { }

    /// <summary>Scope is exactly one tenant.</summary>
    public sealed record ForSingle(TenantId Tenant) : TenantSelection;

    /// <summary>Scope is an explicit set of tenants.</summary>
    public sealed record ForMultiple(IReadOnlyList<TenantId> Tenants) : TenantSelection;

    /// <summary>
    /// Scope is all tenants the current principal can access right now
    /// (macaroon expires_at caveats honored; time-point = request wall-clock).
    /// NOT "ever accessible" or "accessible at record creation time."
    /// </summary>
    public sealed record AllAccessible : TenantSelection;

    // Factory helpers
    public static TenantSelection Of(TenantId tenant) => new ForSingle(tenant);
    public static TenantSelection Of(params TenantId[] tenants) =>
        tenants.Length == 1 ? new ForSingle(tenants[0]) : new ForMultiple(tenants.ToList());
    public static readonly TenantSelection All = new AllAccessible();
}
```

### Design decisions baked in

| Decision | Choice | Rationale |
|---|---|---|
| Abstract record + private constructor | Pattern: closed discriminated union | Prevents external subclassing; exhaustive switch possible |
| `ForSingle` / `ForMultiple` / `AllAccessible` case names | Prefixed with `For` | Disambiguates from factory methods (`Of(...)`) and the `All` constant |
| `AllAccessible` semantics = "now" | v0 = dashboard semantic | Most common case; "at-record-creation" and "ever-accessible" siblings deferred |
| `IReadOnlyList<TenantId>` (not `IReadOnlySet`) | Ordered list | Preserves sort stability; uniqueness is caller's responsibility (same as `params TenantId[]` factory) |
| `params TenantId[]` factory | Collapses Single/Multiple at call site | Ergonomic; `Of(t1)` → `ForSingle`, `Of(t1, t2)` → `ForMultiple` |
| Home package | `foundation-multitenancy` | Alongside `ITenantScoped`, `TenantMetadata`, `ITenantCatalog` |

### Deferred siblings (document only, not implement)

- `TenantSelection.AtPointInTime(DateTimeOffset)` — "accessible at a specific wall-clock time" for regulatory-investigation queries
- `TenantSelection.ForGrantedBy(PrincipalId)` — tenants accessible via capability grants from a specific principal
- Both require the authorization service; deferred to Phase 2 when the macaroon / capability-graph layer matures

---

## 6. Architecture Decisions Needed (Stage 02 ADR)

The discovery resolves all 8 intake sub-items. Stage 02 should author a single ADR covering:

### 6.1 Workstream A ADR decisions (non-breaking)

- D1: Add `TenantId.System = new("__system__")` sentinel
- D2: Reserve `"__"` prefix for Sunfish-managed sentinels; validate in `TenantId(string Value)` constructor
- D3: Mark `TenantId.Default` `[Obsolete]`; update `NullAuditContextProvider` to use `TenantId.System`
- D4: Mark `IMayHaveTenant` `[Obsolete]`
- D5: Add `TenantSelection` discriminated union per §5 shape
- D6: `TenantSelection.AllAccessible` v0 semantics = "accessible at request wall-clock" (honor macaroon TTLs)

### 6.2 Workstream B decisions (separate api-change ADR)

- D7: Migrate `foundation/Assets/Audit/AuditQuery.Tenant` from `TenantId?` → `TenantSelection?`
- D8: Migrate `foundation/Assets/Entities/EntityQuery.Tenant` from `TenantId?` → `TenantSelection?`
- D9: Migrate `foundation-localfirst/DataExport.TenantId` from `TenantId?` → `TenantSelection?` (rename to `Scope`)
- D10: kernel-audit Tier 2 retrofit approach — see §6.2a

#### 6.2a Kernel-audit Tier 2 note

The W#2 ledger says "Tier 2 (`AuditQuery.TenantId → TenantSelection`) remains blocked on workstream #1's M2."
**This framing is slightly misleading** based on discovery findings:

- `kernel-audit/AuditQuery.TenantId` (non-nullable) deliberately requires single-tenant scoping per ADR 0049 v0.
  Changing this kernel type to `TenantSelection` would be a significant semantic change — kernel audit reads
  are intentionally per-tenant. The v0 design decision is sound.
- The retrofit is actually in `foundation/Assets/Audit/AuditQuery.Tenant` (Workstream B D7 above) — the
  foundation-layer query object that wraps/composes with the kernel.
- **Recommendation:** Close W#2 Tier 2 retrofit scope as "rename: foundation AuditQuery.Tenant migration
  (Workstream B D7), not kernel-audit AuditQuery.TenantId migration."

### 6.3 Pipeline routing

| Workstream | Variant | ADR target | Gate |
|---|---|---|---|
| A | `sunfish-feature-change` | New ADR (next available number after 0083) | No prerequisites |
| B | `sunfish-api-change` | Separate ADR amendment | Workstream A ADR Accepted |

---

## 7. Open Questions

**OQ-1: `__` prefix validation** — Should the reserved-prefix check be a compile-time analyzer
(Roslyn), a constructor `throw`, or XML-documented convention only? Discovery recommendation:
constructor `throw` (fail fast; sentinels are known at startup); no analyzer needed given
the already-established pattern in `foundation-wayfinder-analyzers`.

**OQ-2: `TenantId.Default` deprecation window** — Standard Sunfish deprecation is "1 version warning
→ remove in next breaking change release." Since Sunfish is pre-v1, the window can be short (one
major API-change wave). Recommendation: remove in the same Workstream B api-change PR that
migrates the 3 query sites.

**OQ-3: `ForMultiple` ordering invariant** — Should `TenantSelection.ForMultiple.Tenants` de-duplicate
at construction? Recommendation: no — caller responsibility; document it. De-duplication is an
`IReadOnlySet<TenantId>` concern and adds complexity without a clear use case today.

**OQ-4: Implicit conversion `TenantId → TenantSelection`** — Should `TenantId` gain an implicit
cast to `TenantSelection.ForSingle`? Recommendation: **yes**, as a convenience on `TenantId`:
`public static implicit operator TenantSelection(TenantId id) => TenantSelection.Of(id);`
This makes call sites that pass a single `TenantId` to a `TenantSelection?` parameter work without
changes — a migration-ergonomics win.

---

## 8. Done Conditions (Stage 01 complete when)

- [x] All `TenantId?` sites catalogued and classified
- [x] `IMayHaveTenant` implementation count verified (0 — safe to deprecate)
- [x] `TenantSelection` shape designed with discriminated union
- [x] Kernel-audit Tier 2 scope clarified
- [x] Breaking vs. non-breaking split identified and justified
- [x] Stage 02 ADR decision list compiled (§6)
- [x] Pipeline routing confirmed (feature-change + api-change)

**Exit:** Discovery complete. XO proceeds to Stage 02 Architecture — author ADR for Workstream A
(`TenantId.System` + `TenantSelection` introduction). Workstream B (query migration) is
filed as a follow-on api-change workstream after Workstream A ADR reaches Accepted.

---

## 9. References

| Source | Key fact |
|---|---|
| `foundation/Assets/Common/TenantId.cs` | `TenantId` definition; `Default` sentinel only |
| `foundation-multitenancy/ITenantScoped.cs` | `IMayHaveTenant` + `IMustHaveTenant` + `ITenantScoped` |
| `foundation-multitenancy/TenantMetadata.cs` | 7-field record; `TenantStatus` 4-value enum |
| `kernel-audit/AuditQuery.cs` | Required non-nullable `TenantId TenantId` — v0 per-tenant design |
| `foundation/Assets/Audit/AuditQuery.cs` | Optional `TenantId? Tenant` — Group A migration target |
| `foundation/Assets/Entities/EntityQuery.cs` | Optional `TenantId? Tenant` — Group A migration target |
| `foundation-localfirst/DataExport.cs` | `TenantId? TenantId` — Group A migration target |
| `foundation/Assets/Audit/IAuditContextProvider.cs:23-33` | `NullAuditContextProvider.GetTenant() => TenantId.Default` |
| ADR 0049 | Kernel-audit v0 per-tenant scope decision |
| Intake (`2026-04-28`) | 8 sub-items; widened from sentinel-only to combined TenantSelection |
