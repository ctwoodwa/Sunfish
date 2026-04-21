---
uid: block-tenant-admin-tenant-profile
title: Tenant Admin — Tenant Profile
description: TenantProfile, TenantUser, TenantRole, and the profile and users surfaces on ITenantAdminService.
keywords:
  - tenant-profile
  - tenant-user
  - tenant-role
  - rbac-coarse
  - invitation
---

# Tenant Admin — Tenant Profile

## Overview

The profile half of `blocks-tenant-admin` covers the tenant's own presentation metadata and the list of users who can act on its behalf. This page walks the entities, the role model, and the profile/user methods on `ITenantAdminService`.

## TenantProfile

```csharp
public sealed record TenantProfile : IMustHaveTenant
{
    public required TenantId TenantId { get; init; }
    public required string DisplayName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? BundleKey { get; init; }
}
```

One profile per `TenantId`. `BundleKey` is the *primary* bundle pointer — distinct from the list of currently-active bundles returned by `ListActiveBundlesAsync`. A tenant may have multiple bundles activated and still designate one as primary (e.g. the one that drives navigation or branding).

## TenantUser

```csharp
public sealed record TenantUser : IMustHaveTenant
{
    public required TenantUserId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public required TenantRole Role { get; init; }
    public required DateTime InvitedAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
}
```

`TenantUser` is the per-tenant projection of a user. The authoritative identity store (Auth0, Entra, custom) lives outside this block; the tenant-admin service only records the membership, role, and invitation timestamps.

`AcceptedAt` being `null` means the invitation is still pending. The block does not define how the acceptance flows back — email tokens, SSO trust, or direct API call are all valid paths. Consumers call the service once they have confirmation that the user accepted.

## TenantRole

```csharp
public enum TenantRole
{
    Owner = 0,
    Admin = 1,
    Manager = 2,
    Member = 3,
    Viewer = 4,
}
```

The role set is coarse by design. Per the code comment, a full RBAC engine is out of scope; these five values provide the shell-admin surface. The naming matches the Bridge data audit recommendation (`_shared/engineering/bridge-data-audit.md` §Recommendation 4).

Semantic guidance:

- **Owner** — full control including billing; cannot be removed by peers. There is no enforced "exactly one owner" rule at the service layer; consumers may enforce it.
- **Admin** — full control except billing and ownership transfer.
- **Manager** — operational, day-to-day elevated access.
- **Member** — regular contributor.
- **Viewer** — read-only.

## Profile methods on ITenantAdminService

```csharp
ValueTask<TenantProfile?> GetTenantProfileAsync(TenantId tenantId, CancellationToken ct = default);
ValueTask<TenantProfile>  UpdateTenantProfileAsync(UpdateTenantProfileRequest request, CancellationToken ct = default);
```

`UpdateTenantProfileAsync` is create-or-update. When no profile exists, `DisplayName` on the request is required. For existing profiles, `null` fields on `UpdateTenantProfileRequest` mean "leave unchanged".

```csharp
public sealed record UpdateTenantProfileRequest
{
    public required TenantId TenantId { get; init; }
    public string? DisplayName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? BundleKey { get; init; }
}
```

## User methods on ITenantAdminService

```csharp
ValueTask<IReadOnlyList<TenantUser>> ListTenantUsersAsync(TenantId tenantId, CancellationToken ct = default);
ValueTask<TenantUser>                InviteTenantUserAsync(InviteTenantUserRequest request, CancellationToken ct = default);
ValueTask<TenantUser>                AssignRoleAsync(TenantId tenantId, TenantUserId userId, TenantRole role, CancellationToken ct = default);
ValueTask<bool>                      RemoveTenantUserAsync(TenantId tenantId, TenantUserId userId, CancellationToken ct = default);
```

- `InviteTenantUserAsync` returns a `TenantUser` with `AcceptedAt = null` (the invitation is pending).
- `AssignRoleAsync` replaces the role wholesale — there is no partial permission grant.
- `RemoveTenantUserAsync` is idempotent — returns `true` when a row was actually removed, `false` when the user was already absent.

```csharp
public sealed record InviteTenantUserRequest
{
    public required TenantId TenantId { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public TenantRole Role { get; init; } = TenantRole.Member;
}
```

## TenantProfileBlock (Blazor)

```razor
<TenantProfileBlock TenantId="@tenantId" />
```

- Injects `ITenantAdminService`.
- On init, loads the profile via `GetTenantProfileAsync`. When none exists, seeds an empty `TenantProfile` with `DisplayName = ""` and `CreatedAt = DateTime.UtcNow`.
- Binds display name, contact email, and contact phone to the form.
- `Save` calls `UpdateTenantProfileAsync` and updates a status line ("Saved." or the exception message).

The block is deliberately minimal. Role-aware edit affordances (disable the form for non-Admins, hide billing-related fields, etc.) are a follow-up.

## TenantUsersListBlock (Blazor)

```razor
<TenantUsersListBlock TenantId="@tenantId" />
```

- Injects `ITenantAdminService`.
- On init, calls `ListTenantUsersAsync(TenantId)` and renders the result as a table with columns for email, display name, role, invited-at, and accepted-at.
- Does not ship invite/remove/edit affordances. A follow-up may add a row action menu.

The block sets `data-user-id` on each row for bUnit-friendly testing.

## Idempotency notes on the service

- **`UpdateTenantProfileAsync`** — create-or-update. First call creates with the required `DisplayName`; later calls update. Fields left `null` on the request stay unchanged.
- **`InviteTenantUserAsync`** — not idempotent; re-inviting the same email creates a second `TenantUser` row with a new `TenantUserId`. Consumers that want "upsert-by-email" semantics should check `ListTenantUsersAsync` first.
- **`AssignRoleAsync`** — idempotent; assigning the same role returns the same record unchanged.
- **`RemoveTenantUserAsync`** — idempotent; returns `false` when the user was already absent.

## Common patterns

**Bootstrapping the first owner** — `InviteTenantUserAsync` with `Role = TenantRole.Owner`, then set `AcceptedAt` to the current UTC time via a direct `UpdateTenantUserAsync` call (not shown on the public surface today — use an implementation-specific seed path).

**Checking whether the current user is privileged** — `ListTenantUsersAsync(tenantId)` and find the user whose email matches the authenticated principal's email; inspect their `Role`.

**Transferring ownership** — `AssignRoleAsync` the outgoing owner to `Admin`, then `AssignRoleAsync` the incoming owner to `Owner`. The service does not enforce an "exactly one owner" invariant; your policy layer may.

## Mapping TenantRole to policy requirements

A common pattern is to tie `TenantRole` values to ASP.NET Core authorization policies:

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("TenantOwner", p => p.RequireRole(TenantRole.Owner.ToString()));
    options.AddPolicy("TenantAdmin", p => p.RequireRole(TenantRole.Owner.ToString(), TenantRole.Admin.ToString()));
    options.AddPolicy("TenantMember", p => p.RequireAssertion(ctx =>
        Enum.TryParse<TenantRole>(ctx.User.FindFirstValue("tenant_role"), out var role)
        && role <= TenantRole.Member));
});
```

The `Role <= Member` trick works because the enum values are ordered from most-privileged to least (`Owner = 0 … Viewer = 4`); a caller with `Member` (3) passes the check, a `Viewer` (4) does not.

## Related

- [Overview](overview.md)
- [Bundle Activation](bundle-activation.md)
- [Entity Model](entity-model.md)
