using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PublicListings.Services;

/// <summary>
/// Single chokepoint for rendering listing data per redaction tier (ADR 0059
/// W#28 Phase 2). Implementations must structurally prevent un-redacted data
/// from leaking to lower tiers — callers MUST go through this interface
/// rather than reading the raw <see cref="PublicListing"/> from the
/// repository.
/// </summary>
public interface IListingRenderer
{
    /// <summary>
    /// Renders a listing for the given viewer tier; the result has all
    /// fields filtered + the address redacted per the listing's
    /// <see cref="RedactionPolicy"/>.
    /// </summary>
    /// <param name="tenant">Tenant the listing belongs to.</param>
    /// <param name="id">Listing id.</param>
    /// <param name="tier">Viewer's capability tier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Rendered projection, or <see langword="null"/> when the listing isn't found.</returns>
    Task<RenderedListing?> RenderForTierAsync(TenantId tenant, PublicListingId id, RedactionTier tier, CancellationToken ct);
}
