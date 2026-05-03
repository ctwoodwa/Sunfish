namespace Sunfish.Blocks.TenantAdmin.Models;

/// <summary>
/// Coarse-grained tenant-admin roles. A full RBAC engine is out of scope for this block —
/// these five values provide the shell-admin surface for tenant-user management. Naming
/// matches <c>_shared/engineering/bridge-data-audit.md</c> §Recommendation 4.
/// </summary>
public enum TenantRole
{
    /// <summary>Tenant owner — full control, billing, cannot be removed by peers.</summary>
    Owner = 0,

    /// <summary>Tenant admin — full control except billing and ownership transfer.</summary>
    Admin = 1,

    /// <summary>Operational manager — day-to-day elevated permissions.</summary>
    Manager = 2,

    /// <summary>Regular contributor — standard user.</summary>
    Member = 3,

    /// <summary>Read-only access.</summary>
    Viewer = 4,
}
