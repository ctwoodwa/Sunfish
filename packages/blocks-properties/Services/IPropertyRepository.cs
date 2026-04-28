using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Properties.Services;

/// <summary>
/// Domain repository for <see cref="Property"/>. First-slice surface: get,
/// list, upsert, soft-delete. Tenant-scoping is mandatory on every call —
/// the repository never returns properties from other tenants.
/// </summary>
public interface IPropertyRepository
{
    /// <summary>Returns the property with the given id, or <c>null</c> if not found in the tenant's scope.</summary>
    Task<Property?> GetByIdAsync(TenantId tenant, PropertyId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all properties owned by the tenant. By default excludes
    /// soft-deleted (<see cref="Property.DisposedAt"/> non-null) records;
    /// pass <paramref name="includeDisposed"/> to include them.
    /// </summary>
    Task<IReadOnlyList<Property>> ListByTenantAsync(TenantId tenant, bool includeDisposed = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the property. The repository asserts that
    /// <see cref="Property.TenantId"/> on the record matches the caller's
    /// expected scope (callers may not write into another tenant).
    /// </summary>
    Task UpsertAsync(Property property, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the property by stamping <see cref="Property.DisposedAt"/>
    /// and <see cref="Property.DisposalReason"/>. The record remains
    /// queryable via <see cref="ListByTenantAsync"/> with
    /// <c>includeDisposed: true</c>. No-op if the property is unknown to
    /// the tenant.
    /// </summary>
    Task SoftDeleteAsync(TenantId tenant, PropertyId id, string reason, DateTimeOffset disposedAt, CancellationToken cancellationToken = default);
}
