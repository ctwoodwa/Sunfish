using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Assets.Audit;

/// <summary>Filter criteria for <see cref="IAuditLog.QueryAsync"/>.</summary>
public sealed record AuditQuery(
    EntityId? Entity = null,
    ActorId? Actor = null,
    TenantSelection? Tenant = null,   // null == AllAccessible (no tenant filter)
    DateTimeOffset? FromInclusive = null,
    DateTimeOffset? ToExclusive = null,
    Op? Op = null,
    int? Limit = null);
