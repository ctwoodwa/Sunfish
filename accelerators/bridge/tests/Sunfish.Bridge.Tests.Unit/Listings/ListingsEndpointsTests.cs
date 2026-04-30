using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Bridge.Listings;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Listings;

public sealed class ListingsEndpointsTests
{
    private static readonly TenantId DemoTenant = new("demo-tenant");
    private static readonly DateTimeOffset PublishedAt = new(2026, 4, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RobotsTxt_AllowsListingsAndDisallowsCriteriaAndPointsAtSitemap()
    {
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = ListingsEndpoints.HandleRobotsAsync(request);
        var body = ExtractContentResultBody(result);

        Assert.Contains("User-agent: *", body);
        Assert.Contains("Allow: /listings", body);
        Assert.Contains("Disallow: /listings/criteria/", body);
        Assert.Contains("Sitemap: https://demo-tenant.bridge.sunfish.dev/sitemap.xml", body);
    }

    [Fact]
    public async Task SitemapXml_ListsOnlyPublishedListingsForTheRequestedTenant()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "downtown-loft", PublicListingStatus.Published, PublishedAt),
            BuildListing(DemoTenant, "river-view-2br", PublicListingStatus.Published, PublishedAt.AddDays(-2)),
            BuildListing(DemoTenant, "draft-listing", PublicListingStatus.Draft, null),
            BuildListing(DemoTenant, "unlisted-house", PublicListingStatus.Unlisted, null),
            BuildListing(new TenantId("other-tenant"), "neighbor-loft", PublicListingStatus.Published, PublishedAt));
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleSitemapAsync(request, repo, CancellationToken.None);
        var body = ExtractBytesResultBody(result);
        var xml = XDocument.Parse(body);

        var ns = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9");
        var locs = xml.Descendants(ns + "loc").Select(e => e.Value).ToList();

        Assert.Contains("https://demo-tenant.bridge.sunfish.dev/listings/downtown-loft", locs);
        Assert.Contains("https://demo-tenant.bridge.sunfish.dev/listings/river-view-2br", locs);
        Assert.DoesNotContain(locs, l => l.Contains("draft-listing"));
        Assert.DoesNotContain(locs, l => l.Contains("unlisted-house"));
        Assert.DoesNotContain(locs, l => l.Contains("neighbor-loft"));
    }

    [Fact]
    public async Task SitemapXml_ProducesValidSitemapsOrgFormat()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "loft-1", PublicListingStatus.Published, PublishedAt));
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleSitemapAsync(request, repo, CancellationToken.None);
        var body = ExtractBytesResultBody(result);
        var xml = XDocument.Parse(body);

        Assert.Equal(XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9"), xml.Root!.Name.Namespace);
        Assert.Equal("urlset", xml.Root.Name.LocalName);
        var firstUrl = xml.Root.Elements().Single();
        Assert.Equal("url", firstUrl.Name.LocalName);
        Assert.Equal("2026-04-25", firstUrl.Element(XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9") + "lastmod")!.Value);
    }

    [Fact]
    public async Task SitemapXml_EmitsEmptyUrlsetWhenNoPublishedListings()
    {
        var repo = new FakeListingRepository();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleSitemapAsync(request, repo, CancellationToken.None);
        var body = ExtractBytesResultBody(result);
        var xml = XDocument.Parse(body);

        Assert.Equal("urlset", xml.Root!.Name.LocalName);
        Assert.Empty(xml.Root.Elements());
    }

    [Theory]
    [InlineData("demo-tenant.bridge.sunfish.dev", "demo-tenant")]
    [InlineData("acme.bridge.sunfish.dev", "acme")]
    [InlineData("localhost", "default")]
    [InlineData("", "default")]
    public void ResolveTenantFromHost_StripsLeadingSubdomainLabel(string host, string expectedTenantValue)
    {
        var tenant = ListingsEndpoints.ResolveTenantFromHost(host);

        Assert.Equal(expectedTenantValue, tenant.Value);
    }

    private static HttpRequest BuildRequest(string host, string scheme)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = scheme;
        ctx.Request.Host = new HostString(host);
        return ctx.Request;
    }

    private static PublicListing BuildListing(TenantId tenant, string slug, PublicListingStatus status, DateTimeOffset? publishedAt)
        => new()
        {
            Id = new PublicListingId(Guid.NewGuid()),
            Tenant = tenant,
            Property = new PropertyId(Guid.NewGuid().ToString()),
            Slug = slug,
            Headline = $"Headline for {slug}",
            Description = "Body content",
            Status = status,
            ShowingAvailability = new ShowingAvailability { Kind = ShowingAvailabilityKind.OpenHouse },
            Redaction = new RedactionPolicy { Address = AddressRedactionLevel.NeighborhoodOnly, IncludeFinancialsForProspect = true, IncludeAssetInventoryForApplicant = true },
            CreatedAt = PublishedAt.AddDays(-7),
            PublishedAt = publishedAt,
        };

    private static string ExtractContentResultBody(IResult result)
    {
        // Results.Text returns ContentHttpResult internally; serialize via the Execute path.
        return ExecuteAndCaptureBodyAsString(result);
    }

    private static string ExtractBytesResultBody(IResult result)
        => ExecuteAndCaptureBodyAsString(result);

    private static string ExecuteAndCaptureBodyAsString(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var ctx = new DefaultHttpContext { RequestServices = provider };
        var stream = new MemoryStream();
        ctx.Response.Body = stream;
        result.ExecuteAsync(ctx).GetAwaiter().GetResult();
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class FakeListingRepository : IListingRepository
    {
        private readonly List<PublicListing> _items;

        public FakeListingRepository(params PublicListing[] items)
        {
            _items = items.ToList();
        }

        public Task<PublicListing> UpsertAsync(PublicListing listing, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<PublicListing?> GetAsync(TenantId tenant, PublicListingId id, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<PublicListing?> GetBySlugAsync(TenantId tenant, string slug, CancellationToken ct)
            => throw new NotImplementedException();

        public async IAsyncEnumerable<PublicListing> ListAsync(TenantId tenant, [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var item in _items.Where(l => l.Tenant.Equals(tenant)))
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }
}
