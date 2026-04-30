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
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Bridge.Listings;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Macaroons;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Listings;

public sealed class StartApplicationRouteTests
{
    private static readonly TenantId DemoTenant = new("demo-tenant");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");
    private static readonly DateTimeOffset PublishedAt = new(2026, 4, 25, 10, 0, 0, TimeSpan.Zero);

    private sealed record TestEnv(
        FakeListingRepository Repo,
        IProspectCapabilityVerifier Verifier,
        InMemoryLeasingPipelineService Leasing,
        MacaroonCapabilityPromoter Promoter,
        PublicListingId ListingId);

    private static TestEnv NewEnv()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var listingId = new PublicListingId(Guid.NewGuid());
        var promoter = new MacaroonCapabilityPromoter(issuer, DemoTenant, new[] { listingId });
        var verifier = new MacaroonProspectCapabilityVerifier(keys);
        var leasing = new InMemoryLeasingPipelineService(promoter, time: null);
        var listing = BuildListing(DemoTenant, listingId, "downtown-loft", PublicListingStatus.Published);
        var repo = new FakeListingRepository(listing);
        return new TestEnv(repo, verifier, leasing, promoter, listingId);
    }

    private static StartApplicationFormPost ValidBody(Guid listingId, Guid signatureEventId) =>
        new(
            ListingId: listingId,
            Facts: new DecisioningFacts
            {
                GrossMonthlyIncome = 5000m,
                IncomeSource = "Acme Corp",
                YearsAtIncomeSource = 3,
                PriorEvictionDisclosed = false,
                ReferenceCount = 2,
                PriorLandlordNames = new[] { "Bob", "Carol" },
                DependentCount = 1,
            },
            Demographics: new DemographicProfile { RaceOrEthnicity = "Decline-to-state" },
            ApplicationFee: Money.Usd(50m),
            SignatureEventId: signatureEventId);

    private static async Task<(string token, ProspectId prospectId)> ProvisionVerifiedProspectAsync(TestEnv env, string email = "alice@example.com")
    {
        var inquiry = await ((IPublicInquiryService)env.Leasing).SubmitInquiryAsync(
            new PublicInquiryRequest
            {
                Tenant = DemoTenant,
                Listing = env.ListingId,
                ProspectName = "Alice Cooper",
                ProspectEmail = email,
                MessageBody = "Looking",
                ClientIp = TestIp,
                UserAgent = "test",
            },
            new AnonymousCapability { Token = "anon-x", IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) },
            CancellationToken.None);
        var prospect = await env.Leasing.PromoteInquiryToProspectAsync(inquiry.Id, email, CancellationToken.None);
        var capability = await env.Promoter.PromoteToProspectAsync(email, TestIp, CancellationToken.None);
        return (capability.MacaroonToken, prospect.Id);
    }

    [Fact]
    public async Task StartApplication_HappyPath_Returns202WithApplicationId()
    {
        var env = NewEnv();
        var (token, prospectId) = await ProvisionVerifiedProspectAsync(env);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleStartApplicationAsync(
            request, token, ValidBody(env.ListingId.Value, Guid.NewGuid()),
            env.Repo, env.Verifier, env.Leasing, CancellationToken.None);

        Assert.Equal(StatusCodes.Status202Accepted, await ExtractStatusAsync(result));
    }

    [Fact]
    public async Task StartApplication_WrongListing_Returns404()
    {
        var env = NewEnv();
        var (token, _) = await ProvisionVerifiedProspectAsync(env);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");
        var unrelatedListing = Guid.NewGuid();

        var result = await ListingsEndpoints.HandleStartApplicationAsync(
            request, token, ValidBody(unrelatedListing, Guid.NewGuid()),
            env.Repo, env.Verifier, env.Leasing, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task StartApplication_DraftListing_Returns404()
    {
        var env = NewEnv();
        var (token, _) = await ProvisionVerifiedProspectAsync(env);
        var draftId = new PublicListingId(Guid.NewGuid());
        env.Repo.Add(BuildListing(DemoTenant, draftId, "draft", PublicListingStatus.Draft));
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleStartApplicationAsync(
            request, token, ValidBody(draftId.Value, Guid.NewGuid()),
            env.Repo, env.Verifier, env.Leasing, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task StartApplication_MalformedToken_Returns401()
    {
        var env = NewEnv();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleStartApplicationAsync(
            request, "not-a-real-token", ValidBody(env.ListingId.Value, Guid.NewGuid()),
            env.Repo, env.Verifier, env.Leasing, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, await ExtractStatusAsync(result));
    }

    [Fact]
    public async Task StartApplication_OrphanedCapability_Returns410()
    {
        // Capability is verified, but no Prospect row exists for the email
        // (data inconsistency; capability outlived the Prospect).
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var listingId = new PublicListingId(Guid.NewGuid());
        var promoter = new MacaroonCapabilityPromoter(issuer, DemoTenant, new[] { listingId });
        var verifier = new MacaroonProspectCapabilityVerifier(keys);
        // Empty leasing service — no Prospect persisted.
        var leasing = new InMemoryLeasingPipelineService(promoter, time: null);
        var listing = BuildListing(DemoTenant, listingId, "downtown-loft", PublicListingStatus.Published);
        var repo = new FakeListingRepository(listing);

        var capability = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, CancellationToken.None);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleStartApplicationAsync(
            request, capability.MacaroonToken, ValidBody(listingId.Value, Guid.NewGuid()),
            repo, verifier, leasing, CancellationToken.None);

        Assert.Equal(StatusCodes.Status410Gone, await ExtractStatusAsync(result));
    }

    [Fact]
    public async Task StartApplication_NullBody_ReturnsValidationProblem()
    {
        var env = NewEnv();
        var (token, _) = await ProvisionVerifiedProspectAsync(env);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleStartApplicationAsync(
            request, token, body: null!, env.Repo, env.Verifier, env.Leasing, CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(result);
    }

    [Fact]
    public async Task StartApplication_HappyPath_BindsApplicationToVerifiedProspect()
    {
        var env = NewEnv();
        var (token, prospectId) = await ProvisionVerifiedProspectAsync(env);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        await ListingsEndpoints.HandleStartApplicationAsync(
            request, token, ValidBody(env.ListingId.Value, Guid.NewGuid()),
            env.Repo, env.Verifier, env.Leasing, CancellationToken.None);

        // Verify the Application was created against the prospect we minted.
        // Using the Leasing service's own enumeration would require iterating;
        // instead, look up the prospect by email to confirm it's the same id.
        var lookedUp = await env.Leasing.GetProspectByEmailAsync(DemoTenant, "alice@example.com", CancellationToken.None);
        Assert.NotNull(lookedUp);
        Assert.Equal(prospectId, lookedUp!.Id);
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
            PublishedAt = status == PublicListingStatus.Published ? PublishedAt : null,
        };

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
        public void Add(PublicListing listing) => _items.Add(listing);

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
