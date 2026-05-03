using System.Collections.Concurrent;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Properties.Services;

/// <summary>
/// Thread-safe in-memory <see cref="IPropertyRepository"/> for tests, demos,
/// and kitchen-sink scenarios. Persistence-backed implementations live behind
/// the same interface in their respective accelerator hosts (Bridge, Anchor).
/// </summary>
public sealed class InMemoryPropertyRepository : IPropertyRepository
{
    private readonly ConcurrentDictionary<(TenantId Tenant, PropertyId Id), Property> _store = new();

    /// <inheritdoc />
    public Task<Property?> GetByIdAsync(TenantId tenant, PropertyId id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((tenant, id), out var property);
        return Task.FromResult(property);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Property>> ListByTenantAsync(TenantId tenant, bool includeDisposed = false, CancellationToken cancellationToken = default)
    {
        var query = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant))
            .Select(kvp => kvp.Value);

        if (!includeDisposed)
        {
            query = query.Where(p => p.DisposedAt is null);
        }

        IReadOnlyList<Property> result = query.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task UpsertAsync(Property property, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(property);
        _store[(property.TenantId, property.Id)] = property;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SoftDeleteAsync(TenantId tenant, PropertyId id, string reason, DateTimeOffset disposedAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (_store.TryGetValue((tenant, id), out var existing))
        {
            var disposed = existing with { DisposedAt = disposedAt, DisposalReason = reason };
            _store[(tenant, id)] = disposed;
        }

        return Task.CompletedTask;
    }
}
