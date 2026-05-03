using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PublicListings.Services;

/// <summary>CRUD contract for <see cref="PublicListing"/>s.</summary>
public interface IListingRepository
{
    /// <summary>Persist a new or updated listing. Tenant scoping is enforced from <see cref="PublicListing.Tenant"/>.</summary>
    Task<PublicListing> UpsertAsync(PublicListing listing, CancellationToken ct);

    /// <summary>Read a listing by id; returns null when not present in the given tenant.</summary>
    Task<PublicListing?> GetAsync(TenantId tenant, PublicListingId id, CancellationToken ct);

    /// <summary>Read a listing by tenant-scoped slug; returns null when not present.</summary>
    Task<PublicListing?> GetBySlugAsync(TenantId tenant, string slug, CancellationToken ct);

    /// <summary>Streams all listings for the tenant, oldest first.</summary>
    IAsyncEnumerable<PublicListing> ListAsync(TenantId tenant, CancellationToken ct);
}
