using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>Filter criteria for <see cref="IEntityStore.QueryAsync"/>.</summary>
public sealed record EntityQuery(
    SchemaId? Schema = null,
    TenantSelection? Tenant = null,   // null == AllAccessible (no tenant filter)
    DateTimeOffset? AsOf = null,
    bool IncludeDeleted = false,
    int? Limit = null);
