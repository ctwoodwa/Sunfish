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

    // ===== W#28 Phase 5c-2 — index + detail HTML pages =====

    [Fact]
    public async Task Index_Lists_PublishedListingsForTheTenant()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "downtown-loft", PublicListingStatus.Published, PublishedAt),
            BuildListing(DemoTenant, "river-view-2br", PublicListingStatus.Published, PublishedAt.AddDays(-1)),
            BuildListing(DemoTenant, "draft-listing", PublicListingStatus.Draft, null),
            BuildListing(new TenantId("other-tenant"), "neighbor-loft", PublicListingStatus.Published, PublishedAt));
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleIndexAsync(request, repo, renderer, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        Assert.Contains("Headline for downtown-loft", html);
        Assert.Contains("Headline for river-view-2br", html);
        Assert.DoesNotContain("draft-listing", html);
        Assert.DoesNotContain("neighbor-loft", html);
        Assert.Contains("href=\"/listings/downtown-loft\"", html);
    }

    [Fact]
    public async Task Index_EmitsCanonicalAndOpenGraphMetadata()
    {
        var repo = new FakeListingRepository();
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleIndexAsync(request, repo, renderer, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        Assert.Contains("<link rel=\"canonical\" href=\"https://demo-tenant.bridge.sunfish.dev/listings\">", html);
        Assert.Contains("<meta property=\"og:type\" content=\"website\">", html);
        Assert.Contains("<meta property=\"og:url\" content=\"https://demo-tenant.bridge.sunfish.dev/listings\">", html);
    }

    [Fact]
    public async Task Index_RendersEmptyStateWhenNoPublishedListings()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "draft-listing", PublicListingStatus.Draft, null));
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleIndexAsync(request, repo, renderer, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        Assert.Contains("No listings are currently published.", html);
    }

    [Fact]
    public async Task Detail_Returns404ForUnknownSlug()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "downtown-loft", PublicListingStatus.Published, PublishedAt));
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleDetailAsync(request, "nonexistent-slug", repo, renderer, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }

    [Fact]
    public async Task Detail_Returns404ForDraftListings()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "draft-listing", PublicListingStatus.Draft, null));
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleDetailAsync(request, "draft-listing", repo, renderer, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }

    [Fact]
    public async Task Detail_Returns404ForUnlistedListings()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "unlisted-listing", PublicListingStatus.Unlisted, null));
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleDetailAsync(request, "unlisted-listing", repo, renderer, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }

    [Fact]
    public async Task Detail_RendersListingHeadlineAndCanonicalUrl()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "downtown-loft", PublicListingStatus.Published, PublishedAt));
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleDetailAsync(request, "downtown-loft", repo, renderer, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        Assert.Contains("<title>Headline for downtown-loft</title>", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://demo-tenant.bridge.sunfish.dev/listings/downtown-loft\">", html);
        Assert.Contains("<h1>Headline for downtown-loft</h1>", html);
    }

    [Fact]
    public async Task Detail_EmitsSchemaOrgAccommodationJsonLd()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "downtown-loft", PublicListingStatus.Published, PublishedAt));
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleDetailAsync(request, "downtown-loft", repo, renderer, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        var ldStart = html.IndexOf("<script type=\"application/ld+json\">", StringComparison.Ordinal);
        Assert.True(ldStart >= 0, "expected JSON-LD script tag in detail HTML");
        var ldEnd = html.IndexOf("</script>", ldStart, StringComparison.Ordinal);
        Assert.True(ldEnd > ldStart);
        var jsonStart = ldStart + "<script type=\"application/ld+json\">".Length;
        var jsonText = html[jsonStart..ldEnd];
        var doc = System.Text.Json.JsonDocument.Parse(jsonText);
        Assert.Equal("https://schema.org", doc.RootElement.GetProperty("@context").GetString());
        Assert.Equal("Accommodation", doc.RootElement.GetProperty("@type").GetString());
        Assert.Equal("Headline for downtown-loft", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("https://demo-tenant.bridge.sunfish.dev/listings/downtown-loft", doc.RootElement.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Detail_EmitsOpenGraphMetadata()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "downtown-loft", PublicListingStatus.Published, PublishedAt));
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleDetailAsync(request, "downtown-loft", repo, renderer, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        Assert.Contains("<meta property=\"og:title\" content=\"Headline for downtown-loft\">", html);
        Assert.Contains("<meta property=\"og:type\" content=\"website\">", html);
        Assert.Contains("<meta property=\"og:url\" content=\"https://demo-tenant.bridge.sunfish.dev/listings/downtown-loft\">", html);
        Assert.Contains("<meta property=\"og:description\"", html);
    }

    [Fact]
    public async Task Detail_HtmlEncodesHeadlineAndDescription()
    {
        var repo = new FakeListingRepository(
            BuildListing(DemoTenant, "xss-test", PublicListingStatus.Published, PublishedAt) with
            {
                Headline = "<script>alert('xss')</script>",
                Description = "Body with <em>html</em>",
            });
        var renderer = new Sunfish.Blocks.PublicListings.Services.DefaultListingRenderer(repo);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev", scheme: "https");

        var result = await ListingsEndpoints.HandleDetailAsync(request, "xss-test", repo, renderer, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        Assert.DoesNotContain("<script>alert('xss')</script>", html);
        Assert.Contains("&lt;script", html);
        Assert.Contains("&lt;em&gt;html&lt;/em&gt;", html);
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
            => Task.FromResult(_items.FirstOrDefault(l => l.Tenant.Equals(tenant) && l.Id.Equals(id)));

        public Task<PublicListing?> GetBySlugAsync(TenantId tenant, string slug, CancellationToken ct)
            => Task.FromResult(_items.FirstOrDefault(l => l.Tenant.Equals(tenant) && l.Slug == slug));

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
