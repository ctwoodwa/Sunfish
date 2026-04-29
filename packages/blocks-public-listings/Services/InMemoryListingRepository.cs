using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PublicListings.Services;

/// <summary>
/// In-memory <see cref="IListingRepository"/>. Thread-safe via
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>; not durable.
/// </summary>
public sealed class InMemoryListingRepository : IListingRepository
{
    private readonly ConcurrentDictionary<(TenantId Tenant, PublicListingId Id), PublicListing> _byId = new();

    /// <inheritdoc />
    public Task<PublicListing> UpsertAsync(PublicListing listing, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(listing);
        if (listing.Tenant == default)
        {
            throw new ArgumentException("PublicListing.Tenant is required.", nameof(listing));
        }
        _byId[(listing.Tenant, listing.Id)] = listing;
        return Task.FromResult(listing);
    }

    /// <inheritdoc />
    public Task<PublicListing?> GetAsync(TenantId tenant, PublicListingId id, CancellationToken ct)
    {
        _byId.TryGetValue((tenant, id), out var listing);
        return Task.FromResult<PublicListing?>(listing);
    }

    /// <inheritdoc />
    public Task<PublicListing?> GetBySlugAsync(TenantId tenant, string slug, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(slug);
        var match = _byId.Values.FirstOrDefault(l => l.Tenant == tenant && l.Slug == slug);
        return Task.FromResult<PublicListing?>(match);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PublicListing> ListAsync(TenantId tenant, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var listing in _byId.Values.Where(l => l.Tenant == tenant).OrderBy(l => l.CreatedAt))
        {
            ct.ThrowIfCancellationRequested();
            yield return listing;
            await Task.Yield();
        }
    }
}
