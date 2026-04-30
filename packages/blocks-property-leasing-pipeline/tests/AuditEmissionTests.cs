using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.PropertyLeasingPipeline.Capabilities;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Integrations.Signatures;
using Sunfish.Foundation.Macaroons;
using Sunfish.Kernel.Audit;
using Xunit;
using ApplicationId = Sunfish.Blocks.PropertyLeasingPipeline.Models.ApplicationId;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Tests;

/// <summary>
/// W#22 Phase 6: lifecycle audit emission. Verifies the 9 events tied to
/// shipped operations + the FHA-defense audit-tier invariant
/// (DemographicProfile field names never leak into audit body keys).
/// </summary>
public sealed class AuditEmissionTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly PublicListingId TestListing = new(Guid.NewGuid());
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");
    private static readonly ActorId Operator = new("operator");

    private sealed class CapturingAuditTrail : IAuditTrail
    {
        public List<AuditRecord> Records { get; } = new();
        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
        public async IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var r in Records) yield return r;
            await Task.CompletedTask;
        }
    }

    private sealed class StubSigner : IOperationSigner
    {
        public PrincipalId IssuerId { get; } = PrincipalId.FromBytes(new byte[32]);
        public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default) =>
            ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, Signature.FromBytes(new byte[64])));
    }

    private static (InMemoryLeasingPipelineService svc, CapturingAuditTrail trail) NewServiceWithAudit()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var promoter = new MacaroonCapabilityPromoter(new DefaultMacaroonIssuer(keys), TestTenant, new[] { TestListing });
        var trail = new CapturingAuditTrail();
        var svc = new InMemoryLeasingPipelineService(
            prospectPromoter: promoter,
            inquiryValidator: null,
            auditTrail: trail,
            signer: new StubSigner(),
            tenantId: TestTenant,
            time: null);
        return (svc, trail);
    }

    private static PublicInquiryRequest MakeInquiry() => new()
    {
        Tenant = TestTenant,
        Listing = TestListing,
        ProspectName = "Alice Smith",
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
        Demographics = new DemographicProfileSubmission
        {
            RaceOrEthnicity = "Decline-to-state",
            FamilialStatus = "single-parent",
            Religion = "secret-religion-ABCDEF",
            DisabilityStatus = "Disabled",
        },
        ApplicationFee = Money.Usd(50m),
        Signature = new SignatureEventRef(Guid.NewGuid()),
    };

    [Fact]
    public async Task SubmitInquiry_Emits_InquiryAccepted()
    {
        var (svc, trail) = NewServiceWithAudit();
        await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);

        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.InquiryAccepted, trail.Records[0].EventType);
        Assert.Equal(TestTenant, trail.Records[0].TenantId);
    }

    [Fact]
    public async Task PromoteInquiry_Emits_ProspectPromoted()
    {
        var (svc, trail) = NewServiceWithAudit();
        var inq = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);

        await svc.PromoteInquiryToProspectAsync(inq.Id, "alice@example.com", default);

        Assert.Equal(2, trail.Records.Count);
        Assert.Equal(AuditEventType.ProspectPromoted, trail.Records[1].EventType);
    }

    [Fact]
    public async Task SubmitApplication_Emits_ApplicationSubmitted()
    {
        var (svc, trail) = NewServiceWithAudit();
        var inq = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inq.Id, "alice@example.com", default);

        await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);

        Assert.Equal(AuditEventType.ApplicationSubmitted, trail.Records[^1].EventType);
    }

    [Fact]
    public async Task ConfirmApplication_Emits_ApplicantPromoted()
    {
        var (svc, trail) = NewServiceWithAudit();
        var inq = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inq.Id, "alice@example.com", default);
        var app = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);

        await svc.ConfirmApplicationAndPromoteAsync(app.Id, Operator, default);

        Assert.Equal(AuditEventType.ApplicantPromoted, trail.Records[^1].EventType);
    }

    [Fact]
    public async Task RecordBackgroundCheck_Emits_BackgroundCheckCompleted()
    {
        var (svc, trail) = NewServiceWithAudit();
        var (_, _, app) = await ProgressToAwaitingBgAsync(svc);

        var bg = new BackgroundCheckResult
        {
            VendorRef = "v1",
            Application = app.Id,
            Outcome = BackgroundCheckOutcome.Clear,
            Findings = Array.Empty<AdverseFinding>(),
            CompletedAt = DateTimeOffset.UtcNow,
        };
        await svc.RecordBackgroundCheckAsync(app.Id, bg, default);

        Assert.Equal(AuditEventType.BackgroundCheckCompleted, trail.Records[^1].EventType);
    }

    [Fact]
    public async Task RecordDecision_Accept_Emits_ApplicationAccepted()
    {
        var (svc, trail) = NewServiceWithAudit();
        var (_, _, app) = await ProgressToAwaitingDecisionAsync(svc);
        await svc.RecordDecisionAsync(app.Id, new ApplicationDecision { Accepted = true, DecidedBy = Operator, Reason = "ok" }, default);

        Assert.Equal(AuditEventType.ApplicationAccepted, trail.Records[^1].EventType);
    }

    [Fact]
    public async Task RecordDecision_Decline_Emits_ApplicationDeclined()
    {
        var (svc, trail) = NewServiceWithAudit();
        var (_, _, app) = await ProgressToAwaitingDecisionAsync(svc);
        await svc.RecordDecisionAsync(app.Id, new ApplicationDecision { Accepted = false, DecidedBy = Operator, Reason = "Eviction" }, default);

        Assert.Equal(AuditEventType.ApplicationDeclined, trail.Records[^1].EventType);
    }

    [Fact]
    public async Task WithdrawApplication_Emits_ApplicationWithdrawn()
    {
        var (svc, trail) = NewServiceWithAudit();
        var inq = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inq.Id, "alice@example.com", default);
        var app = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);
        await svc.WithdrawApplicationAsync(app.Id, Operator, default);

        Assert.Equal(AuditEventType.ApplicationWithdrawn, trail.Records[^1].EventType);
    }

    /// <summary>
    /// FHA-defense at audit tier: ANY demographic field name appearing in
    /// audit-payload body keys is a halt-condition + design failure per
    /// the W#22 hand-off. This test reflects over every emitted record's
    /// payload body and asserts no demographic-related key surfaces.
    /// </summary>
    [Fact]
    public async Task Audit_NeverLeaks_DemographicProfile()
    {
        var (svc, trail) = NewServiceWithAudit();
        var (_, _, app) = await ProgressToAwaitingDecisionAsync(svc);
        await svc.RecordDecisionAsync(app.Id, new ApplicationDecision { Accepted = false, DecidedBy = Operator, Reason = "Eviction" }, default);

        // Reflect over every body's keys + values; check none of the
        // DemographicProfile field names appear.
        var demographicNames = typeof(DemographicProfile)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(DemographicProfile).GetProperties().Select(p => p.Name.ToLowerInvariant()))
            .Concat(typeof(DemographicProfile).GetProperties().Select(p => SnakeCase(p.Name)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Test data above seeds these specific values; confirm none appear in audit:
        var demographicValues = new[] { "secret-religion-ABCDEF", "single-parent", "Disabled" };

        foreach (var record in trail.Records)
        {
            var body = record.Payload.Payload.Body;
            foreach (var key in body.Keys)
            {
                Assert.False(
                    demographicNames.Contains(key),
                    $"Audit body for {record.EventType} contains demographic field-name key '{key}'.");
            }
            foreach (var (key, value) in body)
            {
                if (value is string s)
                {
                    foreach (var sentinel in demographicValues)
                    {
                        Assert.False(s.Contains(sentinel, StringComparison.OrdinalIgnoreCase),
                            $"Audit body for {record.EventType} contains demographic value sentinel '{sentinel}' under key '{key}'.");
                    }
                }
            }
        }
    }

    [Fact]
    public async Task NoEmission_When_AuditUnwired()
    {
        // Service constructed without audit; verify nothing throws during full lifecycle.
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var promoter = new MacaroonCapabilityPromoter(new DefaultMacaroonIssuer(keys), TestTenant, new[] { TestListing });
        var svc = new InMemoryLeasingPipelineService(promoter, time: null);

        var inq = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inq.Id, "alice@example.com", default);
        var app = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);
        await svc.ConfirmApplicationAndPromoteAsync(app.Id, Operator, default);

        Assert.Equal(ApplicationStatus.AwaitingBackgroundCheck, (await svc.GetApplicationAsync(app.Id, default))!.Status);
    }

    [Fact]
    public void Constructor_RejectsAuditWithoutSigner()
    {
        Assert.Throws<ArgumentException>(() =>
            new InMemoryLeasingPipelineService(
                prospectPromoter: null,
                inquiryValidator: null,
                auditTrail: new CapturingAuditTrail(),
                signer: null,
                tenantId: TestTenant,
                time: null));
    }

    [Fact]
    public void Constructor_RejectsAuditWithoutTenant()
    {
        Assert.Throws<ArgumentException>(() =>
            new InMemoryLeasingPipelineService(
                prospectPromoter: null,
                inquiryValidator: null,
                auditTrail: new CapturingAuditTrail(),
                signer: new StubSigner(),
                tenantId: default,
                time: null));
    }

    private async Task<(Inquiry inq, Prospect prospect, Application app)> ProgressToAwaitingBgAsync(InMemoryLeasingPipelineService svc)
    {
        var inq = await ((IPublicInquiryService)svc).SubmitInquiryAsync(MakeInquiry(), MakeAnonymousCapability(), default);
        var prospect = await svc.PromoteInquiryToProspectAsync(inq.Id, "alice@example.com", default);
        var app = await svc.SubmitApplicationAsync(MakeApplicationRequest(prospect.Id), default);
        await svc.ConfirmApplicationAndPromoteAsync(app.Id, Operator, default);
        return (inq, prospect, app);
    }

    private async Task<(Inquiry inq, Prospect prospect, Application app)> ProgressToAwaitingDecisionAsync(InMemoryLeasingPipelineService svc)
    {
        var (inq, prospect, app) = await ProgressToAwaitingBgAsync(svc);
        await svc.RecordBackgroundCheckAsync(app.Id, new BackgroundCheckResult
        {
            VendorRef = "v1", Application = app.Id, Outcome = BackgroundCheckOutcome.Clear,
            Findings = Array.Empty<AdverseFinding>(), CompletedAt = DateTimeOffset.UtcNow,
        }, default);
        return (inq, prospect, app);
    }

    private static string SnakeCase(string pascalCase) =>
        string.Concat(pascalCase.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
