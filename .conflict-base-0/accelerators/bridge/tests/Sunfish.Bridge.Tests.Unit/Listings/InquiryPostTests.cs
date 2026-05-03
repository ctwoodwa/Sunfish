using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Blocks.PublicListings.Defense;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Bridge.Listings;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Listings;

public sealed class InquiryPostTests
{
    private static readonly TenantId DemoTenant = new("demo-tenant");
    private static readonly DateTimeOffset PublishedAt = new(2026, 4, 25, 10, 0, 0, TimeSpan.Zero);

    private sealed record TestEnv(
        FakeListingRepository Repo,
        FakeDefense Defense,
        FakeInquiryService Inquiry);

    private static TestEnv NewEnv(bool defensePasses = true, InquiryDefenseLayer? rejectAt = null)
    {
        var listing = BuildListing(DemoTenant, "downtown-loft", PublicListingStatus.Published, PublishedAt);
        var repo = new FakeListingRepository(listing);
        var defense = new FakeDefense
        {
            Verdict = defensePasses
                ? InquiryDefenseResult.Pass
                : InquiryDefenseResult.Fail(rejectAt ?? InquiryDefenseLayer.Captcha, "test rejection"),
        };
        var inquiry = new FakeInquiryService();
        return new TestEnv(repo, defense, inquiry);
    }

    private static InquiryFormPost ValidBody() =>
        new(Name: "Alice Cooper", Email: "alice@example.com", Phone: null, MessageBody: "Is this listing available?", CaptchaToken: "good-token");

    [Fact]
    public async Task InquiryPost_DefensePass_ForwardsToInquiryServiceAndReturns202()
    {
        var env = NewEnv();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleInquiryPostAsync(
            request, "downtown-loft", ValidBody(), env.Repo, env.Defense, env.Inquiry, CancellationToken.None);

        Assert.Equal(StatusCodes.Status202Accepted, await ExtractStatusAsync(result));
        Assert.Single(env.Inquiry.Submitted);
        Assert.Equal("Alice Cooper", env.Inquiry.Submitted[0].Request.ProspectName);
        Assert.Equal("alice@example.com", env.Inquiry.Submitted[0].Request.ProspectEmail);
        Assert.Equal(DemoTenant, env.Inquiry.Submitted[0].Request.Tenant);
    }

    [Fact]
    public async Task InquiryPost_DefenseRejects_ReturnsUnprocessableEntityAndDoesNotForward()
    {
        var env = NewEnv(defensePasses: false, rejectAt: InquiryDefenseLayer.Captcha);
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleInquiryPostAsync(
            request, "downtown-loft", ValidBody(), env.Repo, env.Defense, env.Inquiry, CancellationToken.None);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, await ExtractStatusAsync(result));
        Assert.Empty(env.Inquiry.Submitted);
    }

    [Fact]
    public async Task InquiryPost_UnknownSlug_Returns404()
    {
        var env = NewEnv();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleInquiryPostAsync(
            request, "no-such-listing", ValidBody(), env.Repo, env.Defense, env.Inquiry, CancellationToken.None);

        Assert.IsType<NotFound>(result);
        Assert.Empty(env.Inquiry.Submitted);
    }

    [Fact]
    public async Task InquiryPost_DraftListing_Returns404()
    {
        var draftListing = BuildListing(DemoTenant, "draft-listing", PublicListingStatus.Draft, null);
        var env = new TestEnv(new FakeListingRepository(draftListing), new FakeDefense(), new FakeInquiryService());
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleInquiryPostAsync(
            request, "draft-listing", ValidBody(), env.Repo, env.Defense, env.Inquiry, CancellationToken.None);

        Assert.IsType<NotFound>(result);
        Assert.Empty(env.Inquiry.Submitted);
    }

    [Fact]
    public async Task InquiryPost_UsesXForwardedForWhenPresent()
    {
        var env = NewEnv();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");
        request.Headers["X-Forwarded-For"] = "203.0.113.99, 10.0.0.1";

        await ListingsEndpoints.HandleInquiryPostAsync(
            request, "downtown-loft", ValidBody(), env.Repo, env.Defense, env.Inquiry, CancellationToken.None);

        Assert.Equal(IPAddress.Parse("203.0.113.99"), env.Defense.LastSubmission!.ClientIp);
    }

    [Fact]
    public async Task InquiryPost_FallsBackToConnectionIpWhenForwardedHeaderMissing()
    {
        var env = NewEnv();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");
        request.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.5");

        await ListingsEndpoints.HandleInquiryPostAsync(
            request, "downtown-loft", ValidBody(), env.Repo, env.Defense, env.Inquiry, CancellationToken.None);

        Assert.Equal(IPAddress.Parse("198.51.100.5"), env.Defense.LastSubmission!.ClientIp);
    }

    [Fact]
    public async Task InquiryPost_PopulatesSubmissionUserAgentAndListingSlug()
    {
        var env = NewEnv();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");
        request.Headers["User-Agent"] = "Mozilla/5.0 BridgeTest";

        await ListingsEndpoints.HandleInquiryPostAsync(
            request, "downtown-loft", ValidBody(), env.Repo, env.Defense, env.Inquiry, CancellationToken.None);

        Assert.Equal("Mozilla/5.0 BridgeTest", env.Defense.LastSubmission!.UserAgent);
        Assert.Equal("downtown-loft", env.Defense.LastSubmission.ListingSlug);
        Assert.Equal("Alice Cooper", env.Defense.LastSubmission.InquirerName);
    }

    [Fact]
    public async Task InquiryPost_NullBody_ReturnsValidationProblem()
    {
        var env = NewEnv();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        var result = await ListingsEndpoints.HandleInquiryPostAsync(
            request, "downtown-loft", body: null!, env.Repo, env.Defense, env.Inquiry, CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(result);
        Assert.Empty(env.Inquiry.Submitted);
    }

    [Fact]
    public async Task InquiryPost_MintsAnonymousCapabilityWith30MinTtl()
    {
        var env = NewEnv();
        var request = BuildRequest("demo-tenant.bridge.sunfish.dev");

        await ListingsEndpoints.HandleInquiryPostAsync(
            request, "downtown-loft", ValidBody(), env.Repo, env.Defense, env.Inquiry, CancellationToken.None);

        var capability = env.Inquiry.Submitted.Single().Capability;
        Assert.False(string.IsNullOrEmpty(capability.Token));
        var span = capability.ExpiresAt - capability.IssuedAt;
        Assert.InRange(span.TotalMinutes, 29, 31);
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

    private static HttpRequest BuildRequest(string host)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Request.Scheme = "https";
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
            CreatedAt = (publishedAt ?? DateTimeOffset.UtcNow).AddDays(-7),
            PublishedAt = publishedAt,
        };

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

    private sealed class FakeDefense : IInquiryFormDefense
    {
        public InquiryDefenseResult Verdict { get; set; } = InquiryDefenseResult.Pass;
        public InquiryFormSubmission? LastSubmission { get; private set; }

        public Task<InquiryDefenseResult> EvaluateAsync(InquiryFormSubmission submission, CancellationToken ct)
        {
            LastSubmission = submission;
            return Task.FromResult(Verdict);
        }
    }

    private sealed class FakeInquiryService : IPublicInquiryService
    {
        public List<(PublicInquiryRequest Request, AnonymousCapability Capability)> Submitted { get; } = new();

        public Task<Inquiry> SubmitInquiryAsync(PublicInquiryRequest request, AnonymousCapability capability, CancellationToken ct)
        {
            Submitted.Add((request, capability));
            return Task.FromResult(new Inquiry
            {
                Id = new InquiryId(Guid.NewGuid()),
                Tenant = request.Tenant,
                Listing = request.Listing,
                ProspectName = request.ProspectName,
                ProspectEmail = request.ProspectEmail,
                ProspectPhone = request.ProspectPhone,
                MessageBody = request.MessageBody,
                ClientIp = request.ClientIp,
                UserAgent = request.UserAgent,
                Status = InquiryStatus.Submitted,
                SubmittedAt = DateTimeOffset.UtcNow,
            });
        }
    }
}
