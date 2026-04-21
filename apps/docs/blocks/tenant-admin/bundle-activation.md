---
uid: block-tenant-admin-bundle-activation
title: Tenant Admin — Bundle Activation
description: BundleActivation, the activate/deactivate service surface, and the BundleActivationPanel Blazor block.
keywords:
  - bundle-activation
  - businesscases
  - edition-selection
  - soft-delete
  - bundle-catalog
---

# Tenant Admin — Bundle Activation

## Overview

Bundle activation is how a tenant opts in to a `blocks-businesscases` bundle at a specific edition. The tenant-admin block stores an immutable-ish activation record (soft-deleted via `DeactivatedAt` so history is preserved), exposes activate/deactivate/list methods on `ITenantAdminService`, and ships a `BundleActivationPanel` Blazor block that wires all of it together against the bundle catalog.

## BundleActivation

```csharp
public sealed record BundleActivation : IMustHaveTenant
{
    public required BundleActivationId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required string BundleKey { get; init; }
    public required string Edition { get; init; }
    public required DateTime ActivatedAt { get; init; }
    public DateTime? DeactivatedAt { get; init; }
}
```

- `BundleKey` — matches `BusinessCaseBundleManifest.Key` from `blocks-businesscases`.
- `Edition` — matches a key from the bundle manifest's `EditionMappings`.
- `DeactivatedAt` is `null` for active activations; setting it (via `DeactivateBundleAsync`) soft-deletes the row. Rows are not hard-deleted so audit trails can reconstruct who had what, when.

The block does not validate that the `BundleKey` or `Edition` actually exist in `IBundleCatalog` — that check is the caller's responsibility. The `BundleActivationPanel` block populates its UI from `IBundleCatalog.GetBundles()` so only real keys make it to the service.

## Service surface

```csharp
ValueTask<BundleActivation>              ActivateBundleAsync(ActivateBundleRequest request, CancellationToken ct = default);
ValueTask<bool>                          DeactivateBundleAsync(TenantId tenantId, string bundleKey, CancellationToken ct = default);
ValueTask<IReadOnlyList<BundleActivation>> ListActiveBundlesAsync(TenantId tenantId, CancellationToken ct = default);
```

### ActivateBundleAsync

```csharp
public sealed record ActivateBundleRequest
{
    public required TenantId TenantId { get; init; }
    public required string BundleKey { get; init; }
    public required string Edition { get; init; }
}
```

Creates a new `BundleActivation` with `ActivatedAt = DateTime.UtcNow` and `DeactivatedAt = null`. The service does not enforce a "one active activation per bundle key" invariant — consumers that need it must add their own check before calling.

### DeactivateBundleAsync

Sets `DeactivatedAt` on the active activation for the `(TenantId, BundleKey)` pair. Returns `true` when an active row was found and mutated; `false` when no active activation existed.

### ListActiveBundlesAsync

Returns activations where `DeactivatedAt == null` for the tenant. The list is not ordered; consumers who care about order should sort on `ActivatedAt`.

## BundleActivationPanel (Blazor)

```razor
<BundleActivationPanel TenantId="@tenantId" />
```

Injects `IBundleCatalog` and `ITenantAdminService`. Renders two sections:

1. **Available bundles** — every `BusinessCaseBundleManifest` returned by `IBundleCatalog.GetBundles()`. For each bundle:
   - Name, version (rendered as `v{Version}`).
   - A `<select>` populated from the bundle's `EditionMappings` keys. The panel tracks the operator's per-bundle selection in an in-component `Dictionary<string, string>`.
   - An **Activate** button that calls `ITenantAdminService.ActivateBundleAsync(...)` with the selected edition.
2. **Active bundles** — the current result of `ITenantAdminService.ListActiveBundlesAsync(TenantId)` rendered as a list of `BundleKey` + `Edition` pairs.

A status line reports the result of the last action ("Activated {key} ({edition})." or the exception message). No optimistic UI — the list reloads after each activation via `RefreshActiveAsync`.

### DOM hooks for testing

The component sets a few testing-friendly data attributes:

- `li.sf-bundle-activation-panel__item[data-bundle-key]` — one per available bundle.
- `li.sf-bundle-activation-panel__active-item[data-activation-id]` — one per active activation.

These are exercised by `BundleActivationPanelTests` in the package's tests folder.

## Typical workflow

```csharp
// Operator clicks "Activate" on the Essentials bundle at the Standard edition:
var activation = await svc.ActivateBundleAsync(new ActivateBundleRequest
{
    TenantId = currentTenant,
    BundleKey = "sunfish.essentials",
    Edition = "standard",
});

// Operator deactivates it later:
var removed = await svc.DeactivateBundleAsync(currentTenant, "sunfish.essentials");
Debug.Assert(removed == true);
```

## Audit and history

Because deactivation is a soft-delete, a full audit trail of "who had what bundle and edition active, when" is recoverable from the table. The block does not currently expose an "inactive activations" query — consumers that need the history can project directly off the persisted rows or extend the service.

Example audit projection:

```csharp
var history = await dbContext.Set<BundleActivation>()
    .Where(a => a.TenantId == tenantId)
    .OrderBy(a => a.ActivatedAt)
    .Select(a => new
    {
        a.BundleKey,
        a.Edition,
        a.ActivatedAt,
        a.DeactivatedAt,
        DurationDays = (a.DeactivatedAt ?? DateTime.UtcNow).Subtract(a.ActivatedAt).TotalDays,
    })
    .ToListAsync(ct);
```

This lightweight projection is enough to feed a timeline UI showing which bundles a tenant has used across time.

## Changing editions without losing history

The most common workflow — "move from standard to enterprise on the Essentials bundle" — is two service calls:

```csharp
await svc.DeactivateBundleAsync(tenantId, "sunfish.essentials");
await svc.ActivateBundleAsync(new ActivateBundleRequest
{
    TenantId  = tenantId,
    BundleKey = "sunfish.essentials",
    Edition   = "enterprise",
});
```

The first call marks the `standard` row with `DeactivatedAt`; the second creates a new `enterprise` row with a fresh `ActivatedAt`. The audit trail shows the transition clearly.

A hypothetical "change edition in place" operation was considered and rejected because it would lose the timestamp of the tier change — the audit value of the two-row pattern is higher than the minor ergonomics win.

## Concurrency

The in-memory service locks around the activation list per tenant while reading and writing, so a concurrent `ActivateBundleAsync` + `DeactivateBundleAsync` pair on the same tenant serialises. This prevents a race where both a deactivate-then-reactivate pattern and a parallel activate land simultaneously and leave two active rows for the same `BundleKey`. A persistence-backed implementation must provide equivalent guarantees (unique partial index on `(TenantId, BundleKey, DeactivatedAt IS NULL)` is the typical postgres answer).

## BundleActivationPanelTests

`tests/BundleActivationPanelTests.cs` uses bUnit to assert:

- The available-bundles list renders one `<li data-bundle-key="...">` per bundle in the catalog.
- Choosing an edition and clicking Activate calls `ITenantAdminService.ActivateBundleAsync` with the expected request.
- The active-bundles list refreshes after activation.
- Error status messages surface on `Exception` from the service.

These fixtures are a useful template if you extend the panel with your own affordances.

## Relationship to `blocks-businesscases`

The bundle activation block is the tenant-admin-side counterpart to `blocks-businesscases`. The catalog side defines *what is available* (bundles, their editions, their routes, their capabilities); the tenant-admin side records *what the tenant chose*. Neither block depends on the other at the service level — only the `BundleActivationPanel` UI block reaches across to read `IBundleCatalog`.

If you want to show only bundles a specific tenant is eligible for (e.g. by SKU), filter `IBundleCatalog.GetBundles()` upstream of the panel or wrap `IBundleCatalog` in a tenant-aware decorator.

## Related

- [Overview](overview.md)
- [Tenant Profile](tenant-profile.md)
- [Entity Model](entity-model.md)
- ADR 0007 — `docs/adrs/0007-bundle-manifest-schema.md`
