using System.Collections.Generic;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Tenant-defined display labels for the closed
/// <see cref="ShipRole"/> taxonomy per ADR 0077 §1.1. Tenants present role
/// names in their own vocabulary (e.g., <c>"Property Manager"</c> for
/// <see cref="ShipRole.DivisionOfficer"/>); labels are display-only and
/// never enter the permission-resolution algorithm. A label collision is
/// harmless — the underlying <see cref="ShipRole"/> enum value is the
/// resolution key.
/// </summary>
public interface IShipRoleRegistry
{
    /// <summary>
    /// Register a tenant-specific display label for <paramref name="baseRole"/>.
    /// Idempotent — re-registering the same triple is a no-op; registering a
    /// different label for the same <paramref name="baseRole"/> overwrites
    /// the prior entry.
    /// </summary>
    /// <param name="baseRole">Closed-enum role per ADR 0077 §1.</param>
    /// <param name="tenantLabel">Display label (localized at adapter boundary).</param>
    /// <param name="scope">
    /// Optional scope restriction (e.g., <c>"property:building-7"</c>) for
    /// composite tenant-defined roles. Null when the label applies to the
    /// base role unrestricted.
    /// </param>
    void AssignLabel(ShipRole baseRole, string tenantLabel, ScopeRestriction? scope);

    /// <summary>
    /// Returns all registered labels for <paramref name="baseRole"/>. Empty
    /// when no tenant-specific labels exist (the role uses its enum-name
    /// display fallback).
    /// </summary>
    IReadOnlyList<ShipRoleLabel> LabelsFor(ShipRole baseRole);
}

/// <summary>
/// A registered tenant label per <see cref="IShipRoleRegistry.AssignLabel"/>.
/// </summary>
/// <param name="BaseRole">Closed-enum role.</param>
/// <param name="TenantLabel">Display label.</param>
/// <param name="Scope">Optional scope restriction; null when unrestricted.</param>
public sealed record ShipRoleLabel(
    ShipRole BaseRole,
    string TenantLabel,
    ScopeRestriction? Scope);

/// <summary>
/// Optional scope restriction on a tenant-defined role label per ADR 0077
/// §1.1. Display-only — never enters the permission-resolution algorithm.
/// </summary>
/// <param name="ScopeKey">Free-form scope identifier (e.g., <c>"property:building-7"</c>).</param>
public sealed record ScopeRestriction(string ScopeKey);
