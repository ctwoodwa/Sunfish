using System.Collections.Concurrent;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Properties.Services;

/// <summary>
/// In-memory <see cref="IPropertyUnitRepository"/> for development,
/// testing, and kitchen-sink demos. Replace with a persistence-backed
/// implementation in production hosts.
/// </summary>
public sealed class InMemoryPropertyUnitRepository : IPropertyUnitRepository
{
    private readonly ConcurrentDictionary<(TenantId, EntityId), PropertyUnit> _store = new();

    /// <inheritdoc />
    public Task<PropertyUnit?> GetByIdAsync(
        TenantId tenant, EntityId id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((tenant, id), out var unit);
        return Task.FromResult(unit);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PropertyUnit>> ListByPropertyAsync(
        TenantId tenant, PropertyId propertyId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PropertyUnit> result = _store
            .Where(kvp => kvp.Key.Item1.Equals(tenant)
                       && kvp.Value.PropertyId.Equals(propertyId))
            .Select(kvp => kvp.Value)
            .ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PropertyUnit>> ListByTenantAsync(
        TenantId tenant, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PropertyUnit> result = _store
            .Where(kvp => kvp.Key.Item1.Equals(tenant))
            .Select(kvp => kvp.Value)
            .ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task UpsertAsync(PropertyUnit unit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unit);
        _store[(unit.TenantId, unit.Id)] = unit;
        return Task.CompletedTask;
    }
}
