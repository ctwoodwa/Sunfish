using System.Net;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Integrations.Signatures;
using Sunfish.Foundation.Macaroons;
using Xunit;
using ApplicationId = Sunfish.Blocks.PropertyLeasingPipeline.Models.ApplicationId;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Tests;

/// <summary>
/// W#22 Phase 7: cross-package wiring with <see cref="IPaymentGateway"/>.
/// Verifies SubmitApplicationAsync authorizes the application fee when
/// a gateway is wired + stores the auth handle for downstream capture.
/// </summary>
public sealed class PaymentWiringTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly PublicListingId TestListing = new(Guid.NewGuid());
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");
    private static readonly ActorId Operator = new("operator");

    private static (InMemoryLeasingPipelineService svc, InMemoryPaymentGateway pay) NewServiceWithPayment()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var promoter = new MacaroonCapabilityPromoter(new DefaultMacaroonIssuer(keys), TestTenant, new[] { TestListing });
        var pay = new InMemoryPaymentGateway();
        var svc = new InMemoryLeasingPipelineService(
            prospectPromoter: promoter,
            inquiryValidator: null,
            auditTrail: null,
            signer: null,
            tenantId: default,
            paymentGateway: pay,
            time: null);
        return (svc, pay);
    }

    private static PublicInquiryRequest MakeInquiry() => new()
    {
        Tenant = TestTenant,
        Listing = TestListing,
        ProspectName = "Alice",
        ProspectEmail = "alice@example.com",
        MessageBody = "Hi.",
        ClientIp = TestIp,
        UserAgent = "Mozilla/5.0",
    };

    private static AnonymousCapability MakeAnonymousCapability() => new()
    {
        Token = "anon-1",
        IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
    };

    private static SubmitApplicationRequest MakeApplicationRequest(ProspectId prospect, decimal feeUsd) => new()
    {
        Tenant = TestTenant,
        Prospect = prospect,
        Listing = TestListing,
        Facts = new DecisioningFacts
        {
            GrossMonthlyIncome = 5000m,
            IncomeSource = "Acme",
            YearsAtIncomeSource = 3,
            PriorEvictionDisclosed = false,
            ReferenceCount = 1,
            PriorLandlordNames = new[] { "Bob" },
            DependentCount = 0,
        },
        Demographics = new DemographicProfileSubmission(),
        ApplicationFee = Money.Usd(feeUsd),
        Signature = new SignatureEventRef(Guid.NewGuid()),
    };

    [Fact]
    public async Task SubmitApplication_AuthorizesFee_WhenPaymentGatewayWired()
    {
        var (svc, pay) = NewServiceWithPayment();
        var inq = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inq.Id, "alice@example.com", default);

        var app = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id, 50m), default);

        Assert.True(svc.PaymentAuthorizationHandles.ContainsKey(app.Id));
        var handle = svc.PaymentAuthorizationHandles[app.Id];
        Assert.True(pay.Journal.ContainsKey(handle));
        Assert.Equal(PaymentStatus.Authorized, pay.Journal[handle].Status);
        Assert.Equal(Money.Usd(50m), pay.Journal[handle].Request.Amount);
    }

    [Fact]
    public async Task SubmitApplication_NoAuthorization_WhenGatewayUnwired()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var promoter = new MacaroonCapabilityPromoter(new DefaultMacaroonIssuer(keys), TestTenant, new[] { TestListing });
        var svc = new InMemoryLeasingPipelineService(promoter, time: null);
        var inq = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inq.Id, "alice@example.com", default);

        var app = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id, 50m), default);

        Assert.False(svc.PaymentAuthorizationHandles.ContainsKey(app.Id));
    }

    [Fact]
    public async Task PaymentAuthorization_CarriesApplicationIdAsCorrelation()
    {
        var (svc, pay) = NewServiceWithPayment();
        var inq = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inq.Id, "alice@example.com", default);

        var app = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id, 75m), default);
        var handle = svc.PaymentAuthorizationHandles[app.Id];

        Assert.Equal(app.Id.Value.ToString("D"), pay.Journal[handle].Request.CorrelationId);
        Assert.Equal(TestTenant, pay.Journal[handle].Request.Tenant);
    }
}
