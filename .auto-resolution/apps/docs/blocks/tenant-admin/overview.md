---
uid: block-tenant-admin-overview
title: Tenant Admin — Overview
description: Introduction to the blocks-tenant-admin package — tenant profile, user membership, and bundle activation.
keywords:
  - tenant-admin
  - rbac
  - roles
  - invitations
  - bundle-activation
  - multi-tenancy
---

# Tenant Admin — Overview

## Overview

The `blocks-tenant-admin` package provides the shell-admin surface for tenant self-service: editing a tenant's profile, inviting users and assigning them coarse-grained roles, and activating business-case bundles at a specific edition. It ships a framework-agnostic service contract (`ITenantAdminService`), an in-memory implementation, an ADR-0015 entity module so its entities live alongside the rest of Sunfish in the shared Bridge `DbContext`, and three ready-to-use Blazor blocks.

## Package path

`packages/blocks-tenant-admin` — assembly `Sunfish.Blocks.TenantAdmin`.

## When to use it

- You want a minimal tenant-admin area in your app shell — profile editing, user list, invite flow, role assignment, bundle activation.
- You want coarse-grained tenant roles (`Owner`, `Admin`, `Manager`, `Member`, `Viewer`) without writing an RBAC engine.
- You are using `blocks-businesscases` and want a visual affordance for a tenant operator to choose which bundle + edition is active.

If you need fine-grained permissions or an identity provider integration, this block is a starting surface — a real RBAC engine is out of scope.

## Key entities

- **`TenantProfile`** — one profile per `TenantId`. Display name, contact email/phone, created-at timestamp, optional primary `BundleKey`.
- **`TenantUser`** — per-tenant projection of a user (the authoritative identity store is outside this block). Carries `Email`, optional `DisplayName`, a `TenantRole`, invited/accepted timestamps.
- **`TenantRole`** — coarse-grained enum: `Owner`, `Admin`, `Manager`, `Member`, `Viewer`.
- **`BundleActivation`** — records that a tenant activated a specific bundle at a specific edition. Soft-deleted via `DeactivatedAt` so audit history is retained.

All four entities implement `IMustHaveTenant` except `TenantRole` (which is an enum).

## Key services

- **`ITenantAdminService`** — core contract across profile, users, and bundle activation.
- **`InMemoryTenantAdminService`** — thread-safe in-memory implementation.
- **`TenantAdminEntityModule`** — `ISunfishEntityModule` that contributes EF Core configurations to the shared Bridge `DbContext` per ADR 0015.

## Key UI components

- **`TenantProfileBlock`** — minimal edit form for a tenant's profile (display name, contact email, contact phone).
- **`TenantUsersListBlock`** — displays the tenant's users (backed by `ITenantAdminService.ListTenantUsersAsync`).
- **`BundleActivationPanel`** — lists every bundle registered in `IBundleCatalog`, lets an operator pick an edition and activate, and shows the currently-active bundles for the tenant.

## DI wiring

```csharp
services.AddInMemoryTenantAdmin();
```

Registers `ITenantAdminService` (singleton → `InMemoryTenantAdminService`) and `TenantAdminEntityModule` as an `ISunfishEntityModule`. See [DI Wiring](di-wiring.md) for details.

## Relationship to other blocks

- `BundleActivation.BundleKey` matches `BusinessCaseBundleManifest.Key` from `blocks-businesscases`.
- `BundleActivation.Edition` matches a key from that manifest's `EditionMappings`.
- `TenantProfile.BundleKey` is a separate pointer — it's the tenant's primary/preferred bundle, which may or may not be the same as the activated bundles listed by `ListActiveBundlesAsync`.

The block does not verify that the `BundleKey` / `Edition` it receives exist in the catalog — that validation is up to the caller (typically the `BundleActivationPanel` pre-fetches them from `IBundleCatalog`).

## Status and deferred items

- RBAC is intentionally coarse — five roles, no permission-level customisation.
- Invitation acceptance flow (email delivery, token verification) is out of scope. `TenantUser.AcceptedAt` is set when the acceptance comes back into the service; delivery is a separate concern.
- `TenantProfileBlock` is deliberately minimal; role-aware edit affordances are a follow-up.

## Where things live in the package

| Path (under `packages/blocks-tenant-admin/`) | Purpose |
|---|---|
| `Models/TenantProfile.cs` | Per-tenant profile record. |
| `Models/TenantUser.cs` | Per-tenant user membership record. |
| `Models/TenantRole.cs` | Five-value coarse role enum. |
| `Models/BundleActivation.cs` | Soft-deletable bundle activation record. |
| `Services/ITenantAdminService.cs` | Framework-agnostic contract across all three domains. |
| `Services/InMemoryTenantAdminService.cs` | Thread-safe in-memory implementation. |
| `Services/*Request.cs` | Input DTOs (`UpdateTenantProfile`, `InviteTenantUser`, `ActivateBundle`). |
| `Data/*EntityConfiguration.cs` | Per-entity `IEntityTypeConfiguration<T>` classes. |
| `Data/TenantAdminEntityModule.cs` | ADR-0015 module. |
| `DependencyInjection/TenantAdminServiceCollectionExtensions.cs` | `AddInMemoryTenantAdmin` extension. |
| `TenantProfileBlock.razor` | Profile edit form. |
| `TenantUsersListBlock.razor` | User list view. |
| `BundleActivationPanel.razor` | Bundle activate/deactivate UI. |
| `tests/InMemoryTenantAdminServiceTests.cs` | Service behaviour. |
| `tests/TenantAdminEntityModuleTests.cs` | Module contract assertions. |
| `tests/BundleActivationPanelTests.cs` | bUnit component tests. |

## End-to-end example

```csharp
// Host wiring
builder.Services
    .AddSunfishFoundation()
    .AddSunfishBridge()
    .AddInMemoryBusinessCases()      // required for BundleActivationPanel
    .AddInMemoryTenantAdmin();

// Inside a request
var svc = sp.GetRequiredService<ITenantAdminService>();
var tenantId = ambientTenant.Id;

// 1. Seed the profile
await svc.UpdateTenantProfileAsync(new UpdateTenantProfileRequest
{
    TenantId     = tenantId,
    DisplayName  = "Acme Property LLC",
    ContactEmail = "admin@acme.example",
});

// 2. Invite a manager
var manager = await svc.InviteTenantUserAsync(new InviteTenantUserRequest
{
    TenantId = tenantId,
    Email    = "ops@acme.example",
    Role     = TenantRole.Manager,
});

// 3. Activate a bundle
var activation = await svc.ActivateBundleAsync(new ActivateBundleRequest
{
    TenantId  = tenantId,
    BundleKey = "sunfish.essentials",
    Edition   = "standard",
});
```

## ADRs in effect

- **ADR 0015 — Module entity registration.** `blocks-tenant-admin` is a canonical implementer.
- **ADR 0008 — Foundation multi-tenancy.** `TenantProfile`, `TenantUser`, `BundleActivation` implement `IMustHaveTenant`.
- **ADR 0007 — Bundle manifest schema.** `BundleActivation.BundleKey` and `Edition` reference keys from `BusinessCaseBundleManifest`.
- **ADR 0022 — Example catalog + docs taxonomy.** Governs this docs page set.

## Related

- [Tenant Profile](tenant-profile.md)
- [Bundle Activation](bundle-activation.md)
- [Entity Model](entity-model.md)
- [DI Wiring](di-wiring.md)
