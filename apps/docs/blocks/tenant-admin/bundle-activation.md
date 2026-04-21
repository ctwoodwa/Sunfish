---
uid: block-tenant-admin-bundle-activation
title: Tenant Admin — Bundle Activation
description: BundleActivation, the activate/deactivate service surface, and the BundleActivationPanel Blazor block.
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

## Related

- [Overview](overview.md)
- [Tenant Profile](tenant-profile.md)
- [Entity Model](entity-model.md)
