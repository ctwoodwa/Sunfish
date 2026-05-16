---
uid: block-cockpit-permissions
title: Owner Cockpit — Permissions matrix
description: Role × area access matrix for the owner cockpit (W#29 cluster OQ1).
keywords:
  - sunfish
  - cockpit
  - permissions
  - rbac
  - roles
---

# Owner Cockpit — Permissions matrix

`CockpitPermissions` is duplicated as a static class in `accelerators/anchor/Cockpit/CockpitPermissions.cs` and `accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitPermissions.cs`. Both copies must stay in sync; comments in each file reference the mirror.

## Roles

| Role constant | Description |
|---|---|
| `owner` | Property owner (BDFL on the LLC) — full cockpit |
| `spouse` | Spouse co-owner — full cockpit |
| `bookkeeper` | Read property-ops data; receipts (Phase 3); CSV export on reports |
| `tax-advisor` | Summary property read; vendor 1099 list; reports export |
| `contractor` | Own work orders / own profile / own assigned inspections only |
| `leaseholder` | Own unit + own lease + own work orders + own receipts |
| `prospect` | Own application in the leasing pipeline |

## Areas

`CockpitPermissions.Area` enum:

`Properties`, `Equipment`, `Leases`, `LeasingPipeline`, `WorkOrders`, `Vendors`, `Inspections`, `Receipts`, `Reports`.

## Access tiers

`CockpitPermissions.Access` enum: `None`, `ReadOwn`, `Read`, `Full`, `Export`.

## Matrix

| Role | Properties | Equipment | Leases | Leasing Pipeline | Work Orders | Vendors | Inspections | Receipts (Ph3) | Reports (Ph3) |
|---|---|---|---|---|---|---|---|---|---|
| `owner` | Full | Full | Full | Full | Full | Full | Full | Full | Full |
| `spouse` | Full | Full | Full | Full | Full | Full | Full | Full | Full |
| `bookkeeper` | Read | Read | Read | None | Read | Read | Read | Full | Export |
| `tax-advisor` | Read | None | None | None | None | Read | None | Export | Full |
| `contractor` | None | ReadOwn | None | None | ReadOwn | ReadOwn | ReadOwn | None | None |
| `leaseholder` | ReadOwn | None | ReadOwn | None | ReadOwn | None | None | ReadOwn | None |
| `prospect` | None | None | None | ReadOwn | None | None | None | None | None |

## Enforcement

### Route guard (PR 1)

`CockpitPolicy` allows entry only for `owner` and `spouse`:

```csharp
options.AddPolicy(CockpitEndpoints.CockpitPolicyName, policy =>
{
    policy.RequireAuthenticatedUser();
    policy.RequireAssertion(ctx =>
    {
        var role = ctx.User.FindFirst("role")?.Value
                ?? ctx.User.FindFirst(ClaimTypes.Role)?.Value;
        return CockpitPermissions.CanEnterCockpit(role);
    });
});
```

Other roles receive `401 Unauthorized` (unauthenticated) or `403 Forbidden` (wrong role) when they hit a `/api/v1/cockpit/*` route.

### Page-level rendering (Phase 3)

When Phase 3 enables `bookkeeper` / `tax-advisor` / etc., page-level reads call `CockpitPermissions.Resolve(role, area)` to choose between rendering the full surface, a `Read`-tier (no edits, no exports), an `Export`-only view, etc.

The `ReadOwn` tier requires the consumer to scope the query by the caller's identity (e.g., `contractor` sees only WOs `AssignedVendorId == myVendorId`); the matrix encodes the policy, the consumer enforces the scoping.

## See also

- [Owner cockpit overview](xref:block-cockpit-overview)
- W#29 hand-off: `icm/_state/handoffs/property-owner-cockpit-stage06-handoff.md`
