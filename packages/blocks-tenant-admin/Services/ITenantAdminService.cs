using Sunfish.Blocks.TenantAdmin.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TenantAdmin.Services;

/// <summary>
/// Contract for tenant-admin operations: profile maintenance, user membership/role
/// management, and bundle activation. Implementations may be in-memory (for
/// testing/demo) or persistence-backed (production).
/// </summary>
public interface ITenantAdminService
{
    // ---- Tenant profile ----

    /// <summary>Returns the profile for the given tenant, or null if none exists.</summary>
    ValueTask<TenantProfile?> GetTenantProfileAsync(TenantId tenantId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates the tenant's profile. When no profile exists, <paramref name="request"/>
    /// must include a <see cref="UpdateTenantProfileRequest.DisplayName"/>.
    /// </summary>
    ValueTask<TenantProfile> UpdateTenantProfileAsync(UpdateTenantProfileRequest request, CancellationToken ct = default);

    // ---- Tenant users ----

    /// <summary>Lists the users currently associated with <paramref name="tenantId"/>.</summary>
    ValueTask<IReadOnlyList<TenantUser>> ListTenantUsersAsync(TenantId tenantId, CancellationToken ct = default);

    /// <summary>Invites a new user to the tenant. The returned record is pending until accepted.</summary>
    ValueTask<TenantUser> InviteTenantUserAsync(InviteTenantUserRequest request, CancellationToken ct = default);

    /// <summary>Assigns a new role to an existing tenant-user.</summary>
    ValueTask<TenantUser> AssignRoleAsync(TenantId tenantId, TenantUserId userId, TenantRole role, CancellationToken ct = default);

    /// <summary>Removes a user from the tenant. Idempotent — returns true if a row was removed.</summary>
    ValueTask<bool> RemoveTenantUserAsync(TenantId tenantId, TenantUserId userId, CancellationToken ct = default);

    // ---- Bundle activation ----

    /// <summary>Activates a bundle (at a specific edition) for the tenant and returns the activation record.</summary>
    ValueTask<BundleActivation> ActivateBundleAsync(ActivateBundleRequest request, CancellationToken ct = default);

    /// <summary>Deactivates an active bundle for the tenant. Returns true if an active activation was found and deactivated.</summary>
    ValueTask<bool> DeactivateBundleAsync(TenantId tenantId, string bundleKey, CancellationToken ct = default);

    /// <summary>Lists the currently-active (non-deactivated) bundle activations for the tenant.</summary>
    ValueTask<IReadOnlyList<BundleActivation>> ListActiveBundlesAsync(TenantId tenantId, CancellationToken ct = default);
}
