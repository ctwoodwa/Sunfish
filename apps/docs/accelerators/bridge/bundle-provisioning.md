---
uid: accelerator-bridge-bundle-provisioning
title: Bridge — Bundle Provisioning
description: How a tenant activates a business-case bundle in Bridge, which services drive activation and entitlement resolution, and how resolved entitlements surface in the shell.
---

# Bridge — Bundle Provisioning

## Overview

A **business-case bundle** is a versioned grouping of `blocks-*` modules,
feature defaults, and edition mappings that a tenant can activate. Bundle
composition semantics are formalized in ADR 0007 (Bundle Manifest Schema);
this page documents how Bridge surfaces that model operationally.

Two packages drive bundle provisioning in Bridge:

- **`packages/blocks-tenant-admin`** — owns tenant profile, tenant users,
  and the mutation side of bundle activation.
  `ITenantAdminService.ActivateBundleAsync` records an activation with a
  `TenantId`, `BundleKey`, and `Edition`.
- **`packages/blocks-businesscases`** — owns the bundle catalog
  (`IBundleCatalog`) and the entitlement resolver (`IBusinessCaseService`
  → `TenantEntitlementSnapshot`).

## Registration

Bundle provisioning is wired in `Sunfish.Bridge/Program.cs` via two DI
extensions:

```csharp
using Sunfish.Blocks.TenantAdmin.DependencyInjection;
using Sunfish.Blocks.BusinessCases.DependencyInjection;

builder.Services.AddInMemoryTenantAdmin();
builder.Services.AddInMemoryBusinessCases();
```

`AddInMemoryBusinessCases()` replaces the no-op `IEntitlementResolver` with
a real resolver backed by the in-memory bundle catalog. In a production
deployment both extensions will be swapped for EF-backed / DAB-backed
implementations; the `ITenantAdminService` and `IBusinessCaseService`
contracts stay the same.

## Activation flow

The operator-facing activation experience lives at **`/account/bundles`**
(page: `Sunfish.Bridge.Client/Pages/Account/Bundles.razor`) under the
Workspace settings group. It composes two blocks from the two packages
above:

```razor
@page "/account/bundles"
@layout AccountLayout
@using Sunfish.Blocks.TenantAdmin
@using Sunfish.Blocks.BusinessCases

<SettingsPageHeader Title="Bundles &amp; entitlements"
                    Description="Activate business-case bundles for this tenant and review the resulting module and feature entitlements." />

<SettingsSection Title="Bundle activation">
    <BundleActivationPanel />
</SettingsSection>

<SettingsSection Title="Resolved entitlements">
    <EntitlementSnapshotBlock />
</SettingsSection>
```

The flow at a glance:

1. **Catalog query** — `BundleActivationPanel` injects `IBundleCatalog` and
   lists every registered bundle with its `Name`, `Version`, and edition
   keys drawn from `Bundle.EditionMappings`.
2. **Edition selection** — the operator picks an edition for the target
   bundle via a `<select>` on each row.
3. **Activate** — the operator clicks **Activate**. The panel calls
   `ITenantAdminService.ActivateBundleAsync(new ActivateBundleRequest { TenantId, BundleKey, Edition })`
   and refreshes the active-bundles list.
4. **Entitlement resolution** — on the page below,
   `EntitlementSnapshotBlock` injects `IBusinessCaseService` and calls
   `GetSnapshotAsync(tenantId)`. The returned `TenantEntitlementSnapshot`
   renders the active bundle key, active edition, active modules, and a
   table of resolved feature-default values.

## What a snapshot contains

```csharp
public sealed record TenantEntitlementSnapshot(
    TenantId TenantId,
    string? ActiveBundleKey,
    string? ActiveEdition,
    IReadOnlyList<string> ActiveModules,
    IReadOnlyDictionary<string, string> ResolvedFeatureValues,
    DateTimeOffset ResolvedAt);
```

- **ActiveBundleKey / ActiveEdition** — what the tenant is on.
- **ActiveModules** — which `blocks-*` module keys are in scope for this
  tenant, derived from the bundle manifest's `Modules` list filtered by
  the edition's mapping.
- **ResolvedFeatureValues** — the flattened, edition-specific feature
  defaults. Downstream consumers (feature-flag checks, UI gates) read
  from this map.
- **ResolvedAt** — when the resolver computed the snapshot. Useful for
  observability and cache invalidation.

The snapshot is the **contract** between bundle activation and feature-flag
enforcement. `blocks-*` code should never inspect a `BundleActivation`
record directly; it reads `ResolvedFeatureValues` off the snapshot via
the `Foundation.FeatureManagement` abstraction.

## Why two services?

Activation and resolution are split deliberately:

- `ITenantAdminService` is a **mutation** service. It writes activations
  and emits domain events; it has no opinion about what an activation
  means.
- `IBusinessCaseService` is a **query** service. It reads the bundle
  catalog + the current activations + the edition's feature mappings and
  produces a snapshot; it never writes.

This mirrors the ADR 0007 split between manifest authoring and manifest
interpretation, and it keeps the tenant-admin UI decoupled from the
entitlement resolution engine — the resolver can be replaced (in-memory,
EF, DAB, a capability registry) without touching the admin surface.

## Demo bundles

Out of the box, `AddInMemoryBusinessCases()` registers the Sunfish
reference bundles so Bridge boots with a working catalog. The five
reference bundles tracked on the roadmap (ADR 0006):

- Property Management
- Asset Management
- Project Management
- Facility Operations
- Acquisition / Underwriting

Each bundle manifest pins its `Modules`, `Version`, and
`EditionMappings`. Landed module coverage is partial and tracked in
`packages/blocks-*` and `packages/foundation-catalog`.

## Testing

The package tests exercise the end-to-end path:

- `packages/blocks-tenant-admin/tests/BundleActivationPanelTests.cs` —
  renders the panel against a fake `IBundleCatalog` and asserts the
  activation call.
- `packages/blocks-tenant-admin/tests/InMemoryTenantAdminServiceTests.cs`
  — verifies activation state.
- `packages/blocks-businesscases/tests/InMemoryBundleProvisioningServiceTests.cs`
  — verifies snapshot resolution shape.

## Related ADRs

- **ADR 0006** — Shell vs. bundle split.
- **ADR 0007** — Bundle Manifest Schema.
- **ADR 0009** — Foundation.FeatureManagement (how resolved feature values
  are consumed downstream).
- **ADR 0015** — Module-Entity Registration Pattern (how a `blocks-*`
  module's entities flow into the Bridge DbContext when the bundle
  activates).
