using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.PublicListings.Audit;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.PublicListings.Services;

/// <summary>
/// In-memory <see cref="IListingRepository"/>. Thread-safe via
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>; not durable.
/// W#28 Phase 7: when a <see cref="PublicListingAuditEmitter"/> is
/// supplied, status transitions to / from
/// <see cref="PublicListingStatus.Published"/> emit
/// <see cref="AuditEventType.PublicListingPublished"/> /
/// <see cref="AuditEventType.PublicListingUnlisted"/> respectively.
/// </summary>
public sealed class InMemoryListingRepository : IListingRepository
{
    private readonly ConcurrentDictionary<(TenantId Tenant, PublicListingId Id), PublicListing> _byId = new();
    private readonly PublicListingAuditEmitter? _audit;
    private readonly TimeProvider _time;

    /// <summary>Creates the repository with audit emission disabled.</summary>
    public InMemoryListingRepository() : this(audit: null, time: null) { }

    /// <summary>Creates the repository with optional audit emission + clock.</summary>
    public InMemoryListingRepository(PublicListingAuditEmitter? audit, TimeProvider? time)
    {
        _audit = audit;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<PublicListing> UpsertAsync(PublicListing listing, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(listing);
        if (listing.Tenant == default)
        {
            throw new ArgumentException("PublicListing.Tenant is required.", nameof(listing));
        }

        _byId.TryGetValue((listing.Tenant, listing.Id), out var prior);
        _byId[(listing.Tenant, listing.Id)] = listing;

        if (_audit is not null)
        {
            var priorStatus = prior?.Status;
            var now = _time.GetUtcNow();
            // Published status entered (either fresh insert as Published or transitioned to Published).
            if (listing.Status == PublicListingStatus.Published && priorStatus != PublicListingStatus.Published)
            {
                await _audit.EmitAsync(
                    AuditEventType.PublicListingPublished,
                    PublicListingAuditPayloadFactory.PublicListingPublished(listing),
                    now,
                    ct).ConfigureAwait(false);
            }
            // Unlisted status entered after having been Published.
            else if (listing.Status == PublicListingStatus.Unlisted && priorStatus == PublicListingStatus.Published)
            {
                await _audit.EmitAsync(
                    AuditEventType.PublicListingUnlisted,
                    PublicListingAuditPayloadFactory.PublicListingUnlisted(listing),
                    now,
                    ct).ConfigureAwait(false);
            }
        }

        return listing;
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
