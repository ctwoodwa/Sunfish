---
uid: block-tenant-admin-tenant-profile
title: Tenant Admin — Tenant Profile
description: TenantProfile, TenantUser, TenantRole, and the profile and users surfaces on ITenantAdminService.
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

## Related

- [Overview](overview.md)
- [Bundle Activation](bundle-activation.md)
- [Entity Model](entity-model.md)
