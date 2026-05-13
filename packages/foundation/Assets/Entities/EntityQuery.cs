using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>Filter criteria for <see cref="IEntityStore.QueryAsync"/>.</summary>
public sealed record EntityQuery(
    SchemaId? Schema = null,
    TenantSelection? Tenant = null,   // null = system-scope (sentinels visible). Use TenantSelection.All for admin-scope (sentinels excluded).
    DateTimeOffset? AsOf = null,
    bool IncludeDeleted = false,
    int? Limit = null);
