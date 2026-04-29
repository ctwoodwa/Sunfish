using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PublicListings.Data;
using Sunfish.Blocks.PublicListings.DependencyInjection;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Persistence;
using Xunit;

namespace Sunfish.Blocks.PublicListings.Tests;

public sealed class InMemoryListingRepositoryTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static PublicListing NewListing(string slug = "123-main-st", PublicListingStatus status = PublicListingStatus.Draft) => new()
    {
        Id = PublicListingId.NewId(),
        Tenant = Tenant,
        Property = new PropertyId("prop-1"),
        Status = status,
        Headline = "Charming 2-bedroom in West End",
        Description = "Beautiful unit with hardwood floors",
        AskingRent = Money.Usd(2000m),
        ShowingAvailability = new ShowingAvailability { Kind = ShowingAvailabilityKind.ByAppointment },
        Redaction = new RedactionPolicy
        {
            Address = AddressRedactionLevel.NeighborhoodOnly,
            IncludeFinancialsForProspect = false,
            IncludeAssetInventoryForApplicant = false,
        },
        Slug = slug,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Upsert_PersistsListing_RoundTripsViaGet()
    {
        var repo = new InMemoryListingRepository();
        var listing = NewListing();
        await repo.UpsertAsync(listing, Ct);

        var fetched = await repo.GetAsync(Tenant, listing.Id, Ct);
        Assert.NotNull(fetched);
        Assert.Equal(listing.Id, fetched!.Id);
        Assert.Equal("Charming 2-bedroom in West End", fetched.Headline);
    }

    [Fact]
    public async Task Get_ReturnsNullForUnknown()
    {
        var repo = new InMemoryListingRepository();
        var fetched = await repo.GetAsync(Tenant, PublicListingId.NewId(), Ct);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task Get_IsTenantScoped()
    {
        var repo = new InMemoryListingRepository();
        var listing = NewListing();
        await repo.UpsertAsync(listing, Ct);

        var crossTenant = await repo.GetAsync(new TenantId("tenant-b"), listing.Id, Ct);
        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task GetBySlug_FindsListing()
    {
        var repo = new InMemoryListingRepository();
        var listing = NewListing(slug: "456-elm-st");
        await repo.UpsertAsync(listing, Ct);

        var fetched = await repo.GetBySlugAsync(Tenant, "456-elm-st", Ct);
        Assert.NotNull(fetched);
        Assert.Equal(listing.Id, fetched!.Id);
    }

    [Fact]
    public async Task GetBySlug_IsTenantScoped()
    {
        var repo = new InMemoryListingRepository();
        var listing = NewListing(slug: "456-elm-st");
        await repo.UpsertAsync(listing, Ct);

        var fetched = await repo.GetBySlugAsync(new TenantId("tenant-b"), "456-elm-st", Ct);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task List_ReturnsTenantScopedListings_OldestFirst()
    {
        var repo = new InMemoryListingRepository();
        var older = NewListing(slug: "a") with { CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero) };
        var newer = NewListing(slug: "b") with { CreatedAt = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero) };
        await repo.UpsertAsync(newer, Ct);
        await repo.UpsertAsync(older, Ct);

        var items = new List<PublicListing>();
        await foreach (var l in repo.ListAsync(Tenant, Ct)) items.Add(l);

        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].Slug);
        Assert.Equal("b", items[1].Slug);
    }

    [Fact]
    public async Task Upsert_RejectsDefaultTenant()
    {
        var repo = new InMemoryListingRepository();
        var listing = NewListing() with { Tenant = default };
        await Assert.ThrowsAsync<ArgumentException>(() => repo.UpsertAsync(listing, Ct));
    }

    [Fact]
    public void DI_RegistersRepositoryAndEntityModule()
    {
        var sp = new ServiceCollection().AddInMemoryPublicListings().BuildServiceProvider();
        Assert.IsType<InMemoryListingRepository>(sp.GetRequiredService<IListingRepository>());
        var modules = sp.GetServices<ISunfishEntityModule>().ToList();
        Assert.Contains(modules, m => m is PublicListingsEntityModule);
    }

    [Fact]
    public async Task Upsert_OverwritesExistingListing()
    {
        var repo = new InMemoryListingRepository();
        var listing = NewListing();
        await repo.UpsertAsync(listing, Ct);

        var updated = listing with { Headline = "Updated headline" };
        await repo.UpsertAsync(updated, Ct);

        var fetched = await repo.GetAsync(Tenant, listing.Id, Ct);
        Assert.Equal("Updated headline", fetched!.Headline);
    }
}
