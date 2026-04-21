using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.TenantAdmin.Models;

/// <summary>
/// A user associated with a tenant. The authoritative identity store lives outside
/// this block; <see cref="TenantUser"/> is the per-tenant projection that records
/// role assignment and invitation metadata.
/// </summary>
public sealed record TenantUser : IMustHaveTenant
{
    /// <summary>Unique identifier for this tenant-user record.</summary>
    public required TenantUserId Id { get; init; }

    /// <summary>The tenant this user belongs to.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>User's email — primary invitation address and display identity.</summary>
    public required string Email { get; init; }

    /// <summary>Optional display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The user's role within this tenant.</summary>
    public required TenantRole Role { get; init; }

    /// <summary>UTC timestamp when the user was invited.</summary>
    public required DateTime InvitedAt { get; init; }

    /// <summary>UTC timestamp when the user accepted the invitation, or null if still pending.</summary>
    public DateTime? AcceptedAt { get; init; }
}
