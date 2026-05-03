using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.MultiTenancy;

/// <summary>
/// Marks an entity as scoped to a specific tenant. Persistence adapters use
/// this to apply tenant filters, enforce isolation, and surface per-tenant
/// indexes.
/// </summary>
public interface ITenantScoped
{
    /// <summary>The tenant that owns this entity.</summary>
    TenantId TenantId { get; }
}

/// <summary>
/// Narrower marker: the tenant value must be populated before the entity can
/// be persisted. Persistence adapters reject writes where <see cref="ITenantScoped.TenantId"/>
/// is the default.
/// </summary>
public interface IMustHaveTenant : ITenantScoped
{
}

/// <summary>
/// Entities that may be tenant-scoped but sometimes represent system-level or
/// cross-tenant records. Persistence adapters apply tenant filters only when
/// <see cref="TenantId"/> is non-null.
/// </summary>
public interface IMayHaveTenant
{
    /// <summary>The tenant that owns this entity, or null for system-level records.</summary>
    TenantId? TenantId { get; }
}
