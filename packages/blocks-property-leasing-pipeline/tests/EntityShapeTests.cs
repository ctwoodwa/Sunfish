using System.Net;
using System.Reflection;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using ApplicationId = Sunfish.Blocks.PropertyLeasingPipeline.Models.ApplicationId;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Integrations.Signatures;
using Xunit;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Tests;

/// <summary>
/// W#22 Phase 1: smoke tests covering each entity's required-property
/// surface + the FHA-defense layout invariant
/// (<see cref="IApplicationDecisioner.DecideAsync"/> MUST NOT accept
/// <see cref="DemographicProfile"/>).
/// </summary>
public sealed class EntityShapeTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly PublicListingId TestListing = new(Guid.NewGuid());
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");

    [Fact]
    public void Inquiry_RequiresAllNonOptionalFields()
    {
        var inq = new Inquiry
        {
            Id = new InquiryId(Guid.NewGuid()),
            Tenant = TestTenant,
            Listing = TestListing,
            ProspectName = "Alice Smith",
            ProspectEmail = "alice@example.com",
            MessageBody = "Hi, is this still available?",
            ClientIp = TestIp,
            UserAgent = "Mozilla/5.0 ...",
            SubmittedAt = DateTimeOffset.UtcNow,
            Status = InquiryStatus.Submitted,
        };

        Assert.Equal(InquiryStatus.Submitted, inq.Status);
        Assert.Null(inq.ProspectPhone);
    }

    [Fact]
    public void Application_QuarantinesDemographicsFromFacts()
    {
        var app = new Application
        {
            Id = new ApplicationId(Guid.NewGuid()),
            Tenant = TestTenant,
            Prospect = new ProspectId(Guid.NewGuid()),
            Listing = TestListing,
            Facts = new DecisioningFacts
            {
                GrossMonthlyIncome = 5000m,
                IncomeSource = "Acme Corp",
                YearsAtIncomeSource = 3,
                PriorEvictionDisclosed = false,
                ReferenceCount = 2,
                PriorLandlordNames = new[] { "Bob Smith", "Carol Jones" },
                DependentCount = 1,
            },
            Demographics = new DemographicProfile
            {
                RaceOrEthnicity = "Decline-to-state",
            },
            Status = ApplicationStatus.Submitted,
            ApplicationSignature = new SignatureEventRef(Guid.NewGuid()),
            ApplicationFee = Money.Usd(50m),
            SubmittedAt = DateTimeOffset.UtcNow,
        };

        Assert.NotSame(app.Facts, app.Demographics);
        Assert.Equal(5000m, app.Facts.GrossMonthlyIncome);
        Assert.Null(app.DecidedAt);
        Assert.Null(app.DecidedBy);
    }

    /// <summary>
    /// FHA-defense invariant per ADR 0057: no decisioning code path may
    /// accept <see cref="DemographicProfile"/> as a parameter. Reflect
    /// over <see cref="IApplicationDecisioner"/> + assert.
    /// </summary>
    [Fact]
    public void IApplicationDecisioner_DoesNotAccept_DemographicProfile()
    {
        var iface = typeof(IApplicationDecisioner);
        foreach (var method in iface.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var param in method.GetParameters())
            {
                Assert.False(
                    param.ParameterType == typeof(DemographicProfile),
                    $"FHA-defense breach: {iface.Name}.{method.Name} accepts DemographicProfile via parameter '{param.Name}'.");
            }
        }
    }

    [Fact]
    public void BackgroundCheckResult_RequiresFindingsList()
    {
        var bg = new BackgroundCheckResult
        {
            VendorRef = "vendor-ref-1",
            Application = new ApplicationId(Guid.NewGuid()),
            Outcome = BackgroundCheckOutcome.Clear,
            Findings = Array.Empty<AdverseFinding>(),
            CompletedAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal(BackgroundCheckOutcome.Clear, bg.Outcome);
        Assert.Empty(bg.Findings);
    }

    [Fact]
    public void AdverseActionNotice_CapturesFcraMandatoryFields()
    {
        var notice = new AdverseActionNotice
        {
            Id = new AdverseActionNoticeId(Guid.NewGuid()),
            Application = new ApplicationId(Guid.NewGuid()),
            CitedFindings = new[]
            {
                new AdverseFinding { Category = "Eviction", Description = "2024 eviction in CA", Source = "ABC Reports" },
            },
            FcraStatement = "FCRA §615(a) statement…",
            DisputeWindowExpiresAt = DateTimeOffset.UtcNow.AddDays(60),
            ConsumerReportingAgency = "ABC Reports Inc.",
            Address = "123 Main St, Anytown",
            NoticeIssuanceSignature = new SignatureEventRef(Guid.NewGuid()),
            IssuedAt = DateTimeOffset.UtcNow,
        };

        Assert.Single(notice.CitedFindings);
        Assert.True(notice.DisputeWindowExpiresAt > notice.IssuedAt);
    }

    [Fact]
    public void LeaseOffer_LinksToApplication()
    {
        var appId = new ApplicationId(Guid.NewGuid());
        var offer = new LeaseOffer
        {
            Id = new LeaseOfferId(Guid.NewGuid()),
            Tenant = TestTenant,
            Application = appId,
            MonthlyRent = Money.Usd(2500m),
            SecurityDeposit = Money.Usd(2500m),
            TermStart = new DateOnly(2026, 6, 1),
            TermEnd = new DateOnly(2027, 5, 31),
            Status = LeaseOfferStatus.Issued,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IssuedAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal(appId, offer.Application);
    }

    [Fact]
    public void IPublicInquiryService_ContractSurface_IsDeclared()
    {
        var iface = typeof(IPublicInquiryService);
        var method = iface.GetMethod(nameof(IPublicInquiryService.SubmitInquiryAsync));

        Assert.NotNull(method);
        var paramTypes = method!.GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Contains(typeof(PublicInquiryRequest), paramTypes);
        Assert.Contains(typeof(AnonymousCapability), paramTypes);
        Assert.Contains(typeof(CancellationToken), paramTypes);
    }
}
