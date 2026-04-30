using System.Net;
using Sunfish.Blocks.PropertyLeasingPipeline.Capabilities;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using ApplicationId = Sunfish.Blocks.PropertyLeasingPipeline.Models.ApplicationId;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Integrations.Signatures;
using Sunfish.Foundation.Macaroons;
using Xunit;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Tests;

/// <summary>
/// W#22 Phase 2: state-machine + capability-promotion tests covering the
/// happy path (Inquiry → Prospect → Application → Decision) and each
/// invalid-transition scenario.
/// </summary>
public sealed class StateMachineTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly PublicListingId TestListing = new(Guid.NewGuid());
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");
    private static readonly ActorId Operator = new("operator");

    private static (InMemoryLeasingPipelineService svc, MacaroonCapabilityPromoter promoter) NewServiceWithPromoter()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var promoter = new MacaroonCapabilityPromoter(issuer, TestTenant, new[] { TestListing });
        var svc = new InMemoryLeasingPipelineService(promoter, time: null);
        return (svc, promoter);
    }

    private static PublicInquiryRequest MakeInquiryRequest() => new()
    {
        Tenant = TestTenant,
        Listing = TestListing,
        ProspectName = "Alice Smith",
        ProspectEmail = "alice@example.com",
        MessageBody = "Is this still available?",
        ClientIp = TestIp,
        UserAgent = "Mozilla/5.0",
    };

    private static AnonymousCapability MakeAnonymousCapability() => new()
    {
        Token = "anon-token-1",
        IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
    };

    private static SubmitApplicationRequest MakeApplicationRequest(ProspectId prospect) => new()
    {
        Tenant = TestTenant,
        Prospect = prospect,
        Listing = TestListing,
        Facts = new DecisioningFacts
        {
            GrossMonthlyIncome = 5000m,
            IncomeSource = "Acme Corp",
            YearsAtIncomeSource = 3,
            PriorEvictionDisclosed = false,
            ReferenceCount = 2,
            PriorLandlordNames = new[] { "Bob", "Carol" },
            DependentCount = 1,
        },
        Demographics = new DemographicProfile { RaceOrEthnicity = "Decline-to-state" },
        ApplicationFee = Money.Usd(50m),
        Signature = new SignatureEventRef(Guid.NewGuid()),
    };

    [Fact]
    public async Task HappyPath_InquiryThroughDecision()
    {
        var (svc, _) = NewServiceWithPromoter();

        // 1. Submit inquiry (boundary call from Bridge per ADR 0059 A1).
        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiryRequest(), MakeAnonymousCapability(), default);
        Assert.Equal(InquiryStatus.Submitted, inquiry.Status);

        // 2. Promote inquiry → prospect on email-verification.
        var prospect = await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", default);
        Assert.Equal(inquiry.Id, prospect.OriginatingInquiry);
        var inquiryAfter = await svc.GetInquiryAsync(inquiry.Id, default);
        Assert.Equal(InquiryStatus.PromotedToProspect, inquiryAfter!.Status);

        // 3. Submit application.
        var application = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);
        Assert.Equal(ApplicationStatus.Submitted, application.Status);

        // 4. Confirm payment + signature → AwaitingBackgroundCheck + ApplicantCapability minted.
        var capability = await svc.ConfirmApplicationAndPromoteAsync(application.Id, Operator, default);
        Assert.Equal(application.Id, capability.Application);
        var afterConfirm = await svc.GetApplicationAsync(application.Id, default);
        Assert.Equal(ApplicationStatus.AwaitingBackgroundCheck, afterConfirm!.Status);

        // 5. Record BG check result → AwaitingDecision.
        var bg = new BackgroundCheckResult
        {
            VendorRef = "v1",
            Application = application.Id,
            Outcome = BackgroundCheckOutcome.Clear,
            Findings = Array.Empty<AdverseFinding>(),
            CompletedAt = DateTimeOffset.UtcNow,
        };
        var afterBg = await svc.RecordBackgroundCheckAsync(application.Id, bg, default);
        Assert.Equal(ApplicationStatus.AwaitingDecision, afterBg.Status);

        // 6. Operator accepts.
        var decision = new ApplicationDecision { Accepted = true, DecidedBy = Operator, Reason = "All criteria met." };
        var afterDecision = await svc.RecordDecisionAsync(application.Id, decision, default);
        Assert.Equal(ApplicationStatus.Accepted, afterDecision.Status);
        Assert.Equal(Operator, afterDecision.DecidedBy);
        Assert.NotNull(afterDecision.DecidedAt);
    }

    [Fact]
    public async Task DeclinedDecision_RoutesToDeclined()
    {
        var (svc, _) = NewServiceWithPromoter();
        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiryRequest(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", default);
        var application = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);
        await svc.ConfirmApplicationAndPromoteAsync(application.Id, Operator, default);
        await svc.RecordBackgroundCheckAsync(application.Id, new BackgroundCheckResult
        {
            VendorRef = "v1", Application = application.Id, Outcome = BackgroundCheckOutcome.HasFindings,
            Findings = new[] { new AdverseFinding { Category = "Eviction", Description = "Prior eviction", Source = "ABC Reports" } },
            CompletedAt = DateTimeOffset.UtcNow,
        }, default);
        var decision = new ApplicationDecision { Accepted = false, DecidedBy = Operator, Reason = "Eviction history." };
        var declined = await svc.RecordDecisionAsync(application.Id, decision, default);

        Assert.Equal(ApplicationStatus.Declined, declined.Status);
    }

    [Fact]
    public async Task WithdrawApplication_FromAnyNonTerminalStatus()
    {
        var (svc, _) = NewServiceWithPromoter();
        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiryRequest(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", default);
        var application = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);

        // Withdraw from Submitted.
        var withdrawn = await svc.WithdrawApplicationAsync(application.Id, Operator, default);
        Assert.Equal(ApplicationStatus.Withdrawn, withdrawn.Status);
    }

    [Fact]
    public async Task WithdrawApplication_FromTerminal_Rejected()
    {
        var (svc, _) = NewServiceWithPromoter();
        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiryRequest(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", default);
        var application = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);
        await svc.ConfirmApplicationAndPromoteAsync(application.Id, Operator, default);
        await svc.RecordBackgroundCheckAsync(application.Id, new BackgroundCheckResult
        {
            VendorRef = "v1", Application = application.Id, Outcome = BackgroundCheckOutcome.Clear,
            Findings = Array.Empty<AdverseFinding>(), CompletedAt = DateTimeOffset.UtcNow,
        }, default);
        await svc.RecordDecisionAsync(application.Id, new ApplicationDecision { Accepted = true, DecidedBy = Operator, Reason = "ok" }, default);

        // Cannot transition out of Accepted.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.WithdrawApplicationAsync(application.Id, Operator, default));
    }

    [Fact]
    public async Task SkippingPaymentConfirmation_CannotJumpToDecision()
    {
        var (svc, _) = NewServiceWithPromoter();
        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiryRequest(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", default);
        var application = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);

        // Application is in Submitted; recording BG check requires AwaitingBackgroundCheck.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RecordBackgroundCheckAsync(application.Id, new BackgroundCheckResult
            {
                VendorRef = "v1", Application = application.Id, Outcome = BackgroundCheckOutcome.Clear,
                Findings = Array.Empty<AdverseFinding>(), CompletedAt = DateTimeOffset.UtcNow,
            }, default));
    }

    [Fact]
    public async Task PromoteInquiry_SecondTime_Rejected()
    {
        var (svc, _) = NewServiceWithPromoter();
        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiryRequest(), MakeAnonymousCapability(), default);
        await svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", default);

        // Inquiry is already PromotedToProspect — terminal; second promotion is rejected.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", default));
    }

    [Fact]
    public async Task PromoteInquiry_WithoutPromoter_Throws()
    {
        var svc = new InMemoryLeasingPipelineService();
        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiryRequest(), MakeAnonymousCapability(), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.PromoteInquiryToProspectAsync(inquiry.Id, "alice@example.com", default));
    }

    [Fact]
    public async Task SubmitApplication_UnknownProspect_Throws()
    {
        var (svc, _) = NewServiceWithPromoter();
        var fakeProspect = new ProspectId(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SubmitApplicationAsync(MakeApplicationRequest(fakeProspect), default));
    }

    [Fact]
    public async Task SubmitInquiry_RejectsNullArgs()
    {
        var (svc, _) = NewServiceWithPromoter();
        var capability = MakeAnonymousCapability();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((IPublicInquiryService)svc).SubmitInquiryAsync(null!, capability, default));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiryRequest(), null!, default));
    }

    [Fact]
    public async Task GetMethods_ReturnNullForUnknownIds()
    {
        var (svc, _) = NewServiceWithPromoter();
        Assert.Null(await svc.GetInquiryAsync(new InquiryId(Guid.NewGuid()), default));
        Assert.Null(await svc.GetProspectAsync(new ProspectId(Guid.NewGuid()), default));
        Assert.Null(await svc.GetApplicationAsync(new ApplicationId(Guid.NewGuid()), default));
    }
}
