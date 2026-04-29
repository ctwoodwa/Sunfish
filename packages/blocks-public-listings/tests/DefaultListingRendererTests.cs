using System.Threading;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Xunit;

namespace Sunfish.Blocks.PublicListings.Tests;

public sealed class DefaultListingRendererTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static PublicListing NewListing(
        AddressRedactionLevel addressPolicy = AddressRedactionLevel.FullAddress,
        bool financialsForProspect = false,
        Money? rent = null) => new()
    {
        Id = PublicListingId.NewId(),
        Tenant = Tenant,
        Property = new PropertyId("prop-1"),
        Status = PublicListingStatus.Published,
        Headline = "Charming 2-bedroom",
        Description = "Body text",
        AskingRent = rent ?? Money.Usd(2000m),
        ShowingAvailability = new ShowingAvailability { Kind = ShowingAvailabilityKind.ByAppointment },
        Redaction = new RedactionPolicy
        {
            Address = addressPolicy,
            IncludeFinancialsForProspect = financialsForProspect,
            IncludeAssetInventoryForApplicant = false,
        },
        Slug = "test-listing",
        CreatedAt = DateTimeOffset.UtcNow,
        Photos = new[]
        {
            new ListingPhotoRef { Id = ListingPhotoRefId.NewId(), BlobRef = "b/anon", OrderIndex = 0, AltText = "Front", MinimumTier = RedactionTier.Anonymous },
            new ListingPhotoRef { Id = ListingPhotoRefId.NewId(), BlobRef = "b/prosp", OrderIndex = 1, AltText = "Kitchen", MinimumTier = RedactionTier.Prospect },
            new ListingPhotoRef { Id = ListingPhotoRefId.NewId(), BlobRef = "b/appl", OrderIndex = 2, AltText = "Bathroom", MinimumTier = RedactionTier.Applicant },
        },
    };

    private static async Task<DefaultListingRenderer> NewRendererWithListing(PublicListing listing)
    {
        var repo = new InMemoryListingRepository();
        await repo.UpsertAsync(listing, Ct);
        return new DefaultListingRenderer(repo);
    }

    [Fact]
    public async Task Anonymous_PhotosFiltered_ToAnonymousMinimumOnly()
    {
        var listing = NewListing();
        var renderer = await NewRendererWithListing(listing);

        var rendered = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Anonymous, Ct);
        Assert.NotNull(rendered);
        Assert.Single(rendered!.Photos);
        Assert.Equal("b/anon", rendered.Photos[0].BlobRef);
    }

    [Fact]
    public async Task Prospect_PhotosFiltered_ToAnonymousAndProspect()
    {
        var listing = NewListing();
        var renderer = await NewRendererWithListing(listing);

        var rendered = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Prospect, Ct);
        Assert.NotNull(rendered);
        Assert.Equal(2, rendered!.Photos.Count);
    }

    [Fact]
    public async Task Applicant_PhotosFiltered_ToAllTiers()
    {
        var listing = NewListing();
        var renderer = await NewRendererWithListing(listing);

        var rendered = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Applicant, Ct);
        Assert.NotNull(rendered);
        Assert.Equal(3, rendered!.Photos.Count);
    }

    [Fact]
    public async Task Anonymous_AddressIsNeighborhoodOnly_RegardlessOfPolicy()
    {
        var listing = NewListing(addressPolicy: AddressRedactionLevel.FullAddress);
        var renderer = await NewRendererWithListing(listing);

        var rendered = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Anonymous, Ct);
        Assert.Contains("[neighborhood:", rendered!.DisplayAddress);
    }

    [Fact]
    public async Task Prospect_AddressIsBlockNumber_WhenPolicyAllows()
    {
        var listing = NewListing(addressPolicy: AddressRedactionLevel.FullAddress);
        var renderer = await NewRendererWithListing(listing);

        var rendered = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Prospect, Ct);
        Assert.Contains("[block:", rendered!.DisplayAddress);
    }

    [Fact]
    public async Task Applicant_AddressIsFull_WhenPolicyAllows()
    {
        var listing = NewListing(addressPolicy: AddressRedactionLevel.FullAddress);
        var renderer = await NewRendererWithListing(listing);

        var rendered = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Applicant, Ct);
        Assert.Contains("[full address:", rendered!.DisplayAddress);
    }

    [Fact]
    public async Task PolicyOverridesViewerTier_AddressNeverExpandedBeyondPolicy()
    {
        // Policy says NeighborhoodOnly; even Applicant tier can't see more.
        var listing = NewListing(addressPolicy: AddressRedactionLevel.NeighborhoodOnly);
        var renderer = await NewRendererWithListing(listing);

        var rendered = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Applicant, Ct);
        Assert.Contains("[neighborhood:", rendered!.DisplayAddress);
    }

    [Fact]
    public async Task Anonymous_AskingRent_IsAlwaysHidden()
    {
        var listing = NewListing();
        var renderer = await NewRendererWithListing(listing);

        var rendered = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Anonymous, Ct);
        Assert.Null(rendered!.AskingRent);
    }

    [Fact]
    public async Task Prospect_AskingRent_HiddenByDefault_ShownWhenPolicyOptIn()
    {
        var hiddenListing = NewListing(financialsForProspect: false);
        var hiddenRenderer = await NewRendererWithListing(hiddenListing);
        var hiddenRendered = await hiddenRenderer.RenderForTierAsync(Tenant, hiddenListing.Id, RedactionTier.Prospect, Ct);
        Assert.Null(hiddenRendered!.AskingRent);

        var shownListing = NewListing(financialsForProspect: true);
        var shownRenderer = await NewRendererWithListing(shownListing);
        var shownRendered = await shownRenderer.RenderForTierAsync(Tenant, shownListing.Id, RedactionTier.Prospect, Ct);
        Assert.NotNull(shownRendered!.AskingRent);
    }

    [Fact]
    public async Task Applicant_AskingRent_AlwaysShown()
    {
        var listing = NewListing(financialsForProspect: false);
        var renderer = await NewRendererWithListing(listing);

        var rendered = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Applicant, Ct);
        Assert.NotNull(rendered!.AskingRent);
        Assert.Equal(2000m, rendered.AskingRent!.Value.Amount);
    }

    [Fact]
    public async Task UnknownListing_ReturnsNull()
    {
        var repo = new InMemoryListingRepository();
        var renderer = new DefaultListingRenderer(repo);

        var rendered = await renderer.RenderForTierAsync(Tenant, PublicListingId.NewId(), RedactionTier.Anonymous, Ct);
        Assert.Null(rendered);
    }

    [Fact]
    public async Task ServedAtTier_EchoesRequestedTier()
    {
        var listing = NewListing();
        var renderer = await NewRendererWithListing(listing);

        var anon = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Anonymous, Ct);
        var pros = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Prospect, Ct);
        var appl = await renderer.RenderForTierAsync(Tenant, listing.Id, RedactionTier.Applicant, Ct);

        Assert.Equal(RedactionTier.Anonymous, anon!.ServedAtTier);
        Assert.Equal(RedactionTier.Prospect, pros!.ServedAtTier);
        Assert.Equal(RedactionTier.Applicant, appl!.ServedAtTier);
    }
}
