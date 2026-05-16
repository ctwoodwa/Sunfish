using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Properties.Services;

/// <summary>
/// Domain repository for <see cref="PropertyUnit"/>. Tenant-scoping is
/// mandatory on every call — the repository never returns units from other
/// tenants.
/// </summary>
public interface IPropertyUnitRepository
{
    /// <summary>
    /// Returns the unit with the given <paramref name="id"/>, or
    /// <c>null</c> if not found in the tenant's scope.
    /// </summary>
    Task<PropertyUnit?> GetByIdAsync(
        TenantId tenant, EntityId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all units belonging to the given property.
    /// </summary>
    Task<IReadOnlyList<PropertyUnit>> ListByPropertyAsync(
        TenantId tenant, PropertyId propertyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all units for the tenant across all properties.
    /// </summary>
    Task<IReadOnlyList<PropertyUnit>> ListByTenantAsync(
        TenantId tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the unit. Asserts that
    /// <see cref="PropertyUnit.TenantId"/> matches the caller's expected scope.
    /// </summary>
    Task UpsertAsync(PropertyUnit unit, CancellationToken cancellationToken = default);
}
