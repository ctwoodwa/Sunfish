using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Bridge.Listings;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Macaroons;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Listings;

public sealed class CriteriaRouteTests
{
    private static readonly TenantId DemoTenant = new("demo-tenant");
    private static readonly TenantId OtherTenant = new("other-tenant");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");
    private static readonly DateTimeOffset PublishedAt = new(2026, 4, 25, 10, 0, 0, TimeSpan.Zero);

    private sealed record TestEnv(
        FakeListingRepository Repo,
        DefaultListingRenderer Renderer,
        IProspectCapabilityVerifier Verifier,
        MacaroonCapabilityPromoter Promoter,
        IReadOnlyList<PublicListingId> Listings);

    private static TestEnv NewEnv(TenantId? tenant = null)
    {
        var t = tenant ?? DemoTenant;
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var listingIds = new[] { new PublicListingId(Guid.NewGuid()), new PublicListingId(Guid.NewGuid()) };
        var promoter = new MacaroonCapabilityPromoter(issuer, t, listingIds);
        var verifier = new MacaroonProspectCapabilityVerifier(keys);

        var listings = new[]
        {
            BuildListing(t, listingIds[0], "downtown-loft", PublicListingStatus.Published),
            BuildListing(t, listingIds[1], "river-view-2br", PublicListingStatus.Published),
        };
        var repo = new FakeListingRepository(listings);
        var renderer = new DefaultListingRenderer(repo);
        return new TestEnv(repo, renderer, verifier, promoter, listingIds);
    }

    [Fact]
    public async Task Criteria_VerifiedToken_RendersAllowedListingsAtProspectTier()
    {
        var env = NewEnv();
        var capability = await env.Promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleCriteriaAsync(
            request, capability.MacaroonToken, env.Repo, env.Renderer, env.Verifier, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        Assert.Contains("alice@example.com", html);
        Assert.Contains("Headline for downtown-loft", html);
        Assert.Contains("Headline for river-view-2br", html);
    }

    [Fact]
    public async Task Criteria_EmitsNoindexNofollowMeta()
    {
        var env = NewEnv();
        var capability = await env.Promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleCriteriaAsync(
            request, capability.MacaroonToken, env.Repo, env.Renderer, env.Verifier, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\">", html);
    }

    [Fact]
    public async Task Criteria_TokenIssuedForOtherTenant_Returns401()
    {
        var env = NewEnv(tenant: OtherTenant);
        var capability = await env.Promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        // Repo is keyed for OtherTenant; route resolves DemoTenant from host;
        // verifier rejects because the caveat tenant != DemoTenant.
        var result = await ListingsEndpoints.HandleCriteriaAsync(
            request, capability.MacaroonToken, env.Repo, env.Renderer, env.Verifier, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, await ExtractStatusAsync(result));
    }

    [Fact]
    public async Task Criteria_MalformedToken_Returns401WithDecodeReason()
    {
        var env = NewEnv();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleCriteriaAsync(
            request, "not-a-real-token", env.Repo, env.Renderer, env.Verifier, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, await ExtractStatusAsync(result));
    }

    [Fact]
    public async Task Criteria_NoPublishedListingsForTenant_Returns401()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var listingIds = new[] { new PublicListingId(Guid.NewGuid()) };
        var promoter = new MacaroonCapabilityPromoter(issuer, DemoTenant, listingIds);
        var verifier = new MacaroonProspectCapabilityVerifier(keys);
        // Empty repo for the requesting tenant.
        var repo = new FakeListingRepository();
        var renderer = new DefaultListingRenderer(repo);

        var capability = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleCriteriaAsync(
            request, capability.MacaroonToken, repo, renderer, verifier, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, await ExtractStatusAsync(result));
    }

    [Fact]
    public async Task Criteria_HtmlEncodesProspectEmailAndHeadlines()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var listingId = new PublicListingId(Guid.NewGuid());
        var promoter = new MacaroonCapabilityPromoter(issuer, DemoTenant, new[] { listingId });
        var verifier = new MacaroonProspectCapabilityVerifier(keys);
        var listing = BuildListing(DemoTenant, listingId, "xss-test", PublicListingStatus.Published) with
        {
            Headline = "<script>alert('xss')</script>",
        };
        var repo = new FakeListingRepository(new[] { listing });
        var renderer = new DefaultListingRenderer(repo);

        var capability = await promoter.PromoteToProspectAsync("eve+<>@example.com", TestIp, default);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleCriteriaAsync(
            request, capability.MacaroonToken, repo, renderer, verifier, CancellationToken.None);
        var html = ExecuteAndCaptureBodyAsString(result);

        Assert.DoesNotContain("<script>alert('xss')</script>", html);
        Assert.Contains("&lt;script", html);
        // Email is HTML-encoded; the exact entity form depends on HtmlEncoder
        // but the raw `<>` chars must NOT appear literally in the output.
        Assert.DoesNotContain("eve+<>@example.com", html);
        Assert.Contains("@example.com", html);
    }

    private static HttpRequest BuildRequest(string host)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString(host);
        return ctx.Request;
    }

    private static PublicListing BuildListing(TenantId tenant, PublicListingId id, string slug, PublicListingStatus status)
        => new()
        {
            Id = id,
            Tenant = tenant,
            Property = new PropertyId(Guid.NewGuid().ToString()),
            Slug = slug,
            Headline = $"Headline for {slug}",
            Description = "Body content",
            Status = status,
            ShowingAvailability = new ShowingAvailability { Kind = ShowingAvailabilityKind.OpenHouse },
            Redaction = new RedactionPolicy { Address = AddressRedactionLevel.NeighborhoodOnly, IncludeFinancialsForProspect = true, IncludeAssetInventoryForApplicant = true },
            CreatedAt = PublishedAt.AddDays(-7),
            PublishedAt = PublishedAt,
        };

    private static string ExecuteAndCaptureBodyAsString(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var stream = new MemoryStream();
        ctx.Response.Body = stream;
        result.ExecuteAsync(ctx).GetAwaiter().GetResult();
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task<int> ExtractStatusAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Response.Body = new MemoryStream();
        await result.ExecuteAsync(ctx);
        return ctx.Response.StatusCode;
    }

    private sealed class FakeListingRepository : IListingRepository
    {
        private readonly List<PublicListing> _items;
        public FakeListingRepository(params PublicListing[] items) => _items = items.ToList();

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
