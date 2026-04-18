using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>Filter criteria for <see cref="IEntityStore.QueryAsync"/>.</summary>
public sealed record EntityQuery(
    SchemaId? Schema = null,
    TenantId? Tenant = null,
    DateTimeOffset? AsOf = null,
    bool IncludeDeleted = false,
    int? Limit = null);
