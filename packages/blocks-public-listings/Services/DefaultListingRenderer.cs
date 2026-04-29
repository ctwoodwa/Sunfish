using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PublicListings.Services;

/// <summary>
/// Default <see cref="IListingRenderer"/> implementation. Reads the raw
/// listing via <see cref="IListingRepository"/> + applies tier-based
/// redaction structurally before returning the projection.
/// </summary>
public sealed class DefaultListingRenderer : IListingRenderer
{
    private readonly IListingRepository _repository;

    /// <summary>Creates the renderer bound to a repository.</summary>
    public DefaultListingRenderer(IListingRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<RenderedListing?> RenderForTierAsync(TenantId tenant, PublicListingId id, RedactionTier tier, CancellationToken ct)
    {
        var listing = await _repository.GetAsync(tenant, id, ct).ConfigureAwait(false);
        if (listing is null)
        {
            return null;
        }

        var photos = listing.Photos
            .Where(p => TierAllows(tier, p.MinimumTier))
            .ToList()
            .AsReadOnly();

        var displayAddress = RenderAddress(listing, tier);
        var askingRent = ResolveAskingRent(listing, tier);

        return new RenderedListing
        {
            Id = listing.Id,
            DisplayAddress = displayAddress,
            Headline = listing.Headline,
            DescriptionMarkdown = listing.Description,
            Photos = photos,
            AskingRent = askingRent,
            ServedAtTier = tier,
        };
    }

    private static bool TierAllows(RedactionTier viewer, RedactionTier minimum) =>
        // Higher viewer tier subsumes lower minimum tiers: applicant > prospect > anonymous.
        (int)viewer >= (int)minimum;

    private static string RenderAddress(PublicListing listing, RedactionTier tier)
    {
        var policyAllows = listing.Redaction.Address;

        // Effective precision is min(viewer-tier-allows, policy-allows).
        var effective = (tier, policyAllows) switch
        {
            (RedactionTier.Applicant, AddressRedactionLevel.FullAddress) => AddressRedactionLevel.FullAddress,
            (RedactionTier.Applicant, var level) => level,                  // policy may still hide for applicants
            (RedactionTier.Prospect, AddressRedactionLevel.FullAddress) => AddressRedactionLevel.BlockNumber,
            (RedactionTier.Prospect, AddressRedactionLevel.BlockNumber) => AddressRedactionLevel.BlockNumber,
            (RedactionTier.Prospect, _) => AddressRedactionLevel.NeighborhoodOnly,
            (RedactionTier.Anonymous, _) => AddressRedactionLevel.NeighborhoodOnly,
            _ => AddressRedactionLevel.NeighborhoodOnly,
        };

        // Phase 2 stub: the listing's raw address isn't stored on the entity yet
        // (Property entity holds it; cross-block lookup is W#28 Phase 5+).
        // Render a tier-appropriate placeholder so the chokepoint is structurally
        // correct even before the Property cross-reference lands.
        return effective switch
        {
            AddressRedactionLevel.FullAddress => $"[full address: listing {listing.Slug}]",
            AddressRedactionLevel.BlockNumber => $"[block: listing {listing.Slug}]",
            _ => $"[neighborhood: listing {listing.Slug}]",
        };
    }

    private static Sunfish.Foundation.Integrations.Payments.Money? ResolveAskingRent(PublicListing listing, RedactionTier tier)
    {
        // Anonymous tier never sees rent.
        if (tier == RedactionTier.Anonymous) return null;

        // Prospect tier sees rent only when redaction policy opts in.
        if (tier == RedactionTier.Prospect && !listing.Redaction.IncludeFinancialsForProspect) return null;

        // Applicant tier always sees rent.
        return listing.AskingRent;
    }
}
