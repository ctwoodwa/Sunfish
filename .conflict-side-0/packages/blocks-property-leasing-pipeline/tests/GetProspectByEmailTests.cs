using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Macaroons;
using Xunit;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Tests;

public sealed class GetProspectByEmailTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");

    private static InMemoryLeasingPipelineService NewService(TenantId tenant)
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var promoter = new MacaroonCapabilityPromoter(issuer, tenant, new[] { new PublicListingId(Guid.NewGuid()) });
        return new InMemoryLeasingPipelineService(promoter, time: null);
    }

    private static async Task<(InMemoryLeasingPipelineService svc, Inquiry inquiry)> SetupWithInquiryAsync(TenantId tenant)
    {
        var svc = NewService(tenant);
        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(
            new PublicInquiryRequest
            {
                Tenant = tenant,
                Listing = new PublicListingId(Guid.NewGuid()),
                ProspectName = "Alice Cooper",
                ProspectEmail = "Alice@Example.COM",
                MessageBody = "Looking for a 2br",
                ClientIp = TestIp,
                UserAgent = "test",
            },
            new AnonymousCapability { Token = "anon-x", IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) },
            CancellationToken.None);
        return (svc, inquiry);
    }

    [Fact]
    public async Task ResolvesProspect_AfterPromotion()
    {
        var (svc, inquiry) = await SetupWithInquiryAsync(TenantA);
        var prospect = await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", CancellationToken.None);

        var lookup = await svc.GetProspectByEmailAsync(TenantA, "alice@example.com", CancellationToken.None);

        Assert.NotNull(lookup);
        Assert.Equal(prospect.Id, lookup!.Id);
    }

    [Fact]
    public async Task LookupIsCaseInsensitive()
    {
        var (svc, inquiry) = await SetupWithInquiryAsync(TenantA);
        await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", CancellationToken.None);

        var lookup = await svc.GetProspectByEmailAsync(TenantA, "ALICE@EXAMPLE.COM", CancellationToken.None);

        Assert.NotNull(lookup);
    }

    [Fact]
    public async Task LookupTrimsWhitespace()
    {
        var (svc, inquiry) = await SetupWithInquiryAsync(TenantA);
        await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", CancellationToken.None);

        var lookup = await svc.GetProspectByEmailAsync(TenantA, "  alice@example.com  ", CancellationToken.None);

        Assert.NotNull(lookup);
    }

    [Fact]
    public async Task ReturnsNull_WhenEmailDoesNotMatch()
    {
        var (svc, inquiry) = await SetupWithInquiryAsync(TenantA);
        await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", CancellationToken.None);

        var lookup = await svc.GetProspectByEmailAsync(TenantA, "other@example.com", CancellationToken.None);

        Assert.Null(lookup);
    }

    [Fact]
    public async Task IsPerTenant_DoesNotLeakAcrossTenants()
    {
        var (svc, inquiry) = await SetupWithInquiryAsync(TenantA);
        await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", CancellationToken.None);

        var lookup = await svc.GetProspectByEmailAsync(TenantB, "alice@example.com", CancellationToken.None);

        Assert.Null(lookup);
    }

    [Fact]
    public async Task ReturnsNull_ForNullOrEmptyEmail()
    {
        var svc = NewService(TenantA);

        Assert.Null(await svc.GetProspectByEmailAsync(TenantA, null!, CancellationToken.None));
        Assert.Null(await svc.GetProspectByEmailAsync(TenantA, string.Empty, CancellationToken.None));
        Assert.Null(await svc.GetProspectByEmailAsync(TenantA, "   ", CancellationToken.None));
    }
}
