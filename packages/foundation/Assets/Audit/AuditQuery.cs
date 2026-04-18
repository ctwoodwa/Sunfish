using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Audit;

/// <summary>Filter criteria for <see cref="IAuditLog.QueryAsync"/>.</summary>
public sealed record AuditQuery(
    EntityId? Entity = null,
    ActorId? Actor = null,
    TenantId? Tenant = null,
    DateTimeOffset? FromInclusive = null,
    DateTimeOffset? ToExclusive = null,
    Op? Op = null,
    int? Limit = null);
