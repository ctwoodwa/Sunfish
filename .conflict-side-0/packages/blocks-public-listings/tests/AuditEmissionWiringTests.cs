using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PublicListings.Audit;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Defense;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Integrations.Captcha;
using Sunfish.Foundation.Macaroons;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.PublicListings.Tests;

/// <summary>
/// W#28 Phase 7 — service-side audit-emission wiring across the 3
/// public-listings services (capability promoter, listing repository,
/// inquiry-form defense).
/// </summary>
public sealed class AuditEmissionWiringTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");

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

    private static (PublicListingAuditEmitter emitter, CapturingAuditTrail trail) NewEmitter()
    {
        var trail = new CapturingAuditTrail();
        var emitter = new PublicListingAuditEmitter(trail, new StubSigner(), TestTenant);
        return (emitter, trail);
    }

    // ─────────── MacaroonCapabilityPromoter ───────────

    [Fact]
    public async Task PromoteToProspect_Emits_CapabilityPromotedToProspect()
    {
        var (emitter, trail) = NewEmitter();
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var listing = new PublicListingId(Guid.NewGuid());
        var promoter = new MacaroonCapabilityPromoter(
            new DefaultMacaroonIssuer(keys),
            TestTenant,
            new[] { listing },
            emitter);

        await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);

        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.CapabilityPromotedToProspect, trail.Records[0].EventType);
        var body = trail.Records[0].Payload.Payload.Body;
        Assert.Equal("alice@example.com", body["verified_email"]);
        Assert.Equal(1, body["accessible_listing_count"]);
    }

    [Fact]
    public async Task PromoteToProspect_NoEmission_WhenAuditUnwired()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var promoter = new MacaroonCapabilityPromoter(
            new DefaultMacaroonIssuer(keys),
            TestTenant,
            Array.Empty<PublicListingId>());

        // No throw on the audit-unwired path → smoke success.
        var capability = await promoter.PromoteToProspectAsync("a@b.com", TestIp, default);
        Assert.False(string.IsNullOrEmpty(capability.MacaroonToken));
    }

    // ─────────── InMemoryListingRepository ───────────

    [Fact]
    public async Task Upsert_NewPublishedListing_Emits_PublicListingPublished()
    {
        var (emitter, trail) = NewEmitter();
        var repo = new InMemoryListingRepository(emitter, time: null);

        await repo.UpsertAsync(MakeListing(PublicListingStatus.Published), default);

        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.PublicListingPublished, trail.Records[0].EventType);
    }

    [Fact]
    public async Task Upsert_DraftToPublished_Emits_PublicListingPublished()
    {
        var (emitter, trail) = NewEmitter();
        var repo = new InMemoryListingRepository(emitter, time: null);
        var listing = MakeListing(PublicListingStatus.Draft);

        await repo.UpsertAsync(listing, default);
        await repo.UpsertAsync(listing with { Status = PublicListingStatus.Published }, default);

        // Only the second upsert emits (Draft → Published is the trigger).
        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.PublicListingPublished, trail.Records[0].EventType);
    }

    [Fact]
    public async Task Upsert_PublishedToUnlisted_Emits_PublicListingUnlisted()
    {
        var (emitter, trail) = NewEmitter();
        var repo = new InMemoryListingRepository(emitter, time: null);
        var listing = MakeListing(PublicListingStatus.Published);

        await repo.UpsertAsync(listing, default);
        await repo.UpsertAsync(listing with { Status = PublicListingStatus.Unlisted }, default);

        Assert.Equal(2, trail.Records.Count);
        Assert.Equal(AuditEventType.PublicListingPublished, trail.Records[0].EventType);
        Assert.Equal(AuditEventType.PublicListingUnlisted, trail.Records[1].EventType);
    }

    [Fact]
    public async Task Upsert_NoStatusChange_NoEmission()
    {
        var (emitter, trail) = NewEmitter();
        var repo = new InMemoryListingRepository(emitter, time: null);
        var listing = MakeListing(PublicListingStatus.Published);

        await repo.UpsertAsync(listing, default);
        await repo.UpsertAsync(listing with { Headline = "Updated headline" }, default);

        // First emit only — no status delta on second upsert.
        Assert.Single(trail.Records);
    }

    [Fact]
    public async Task Upsert_DraftToUnlisted_NoEmission()
    {
        // Status changes that don't pass through Published don't emit
        // (e.g., Draft → Unlisted, when an operator changes their mind).
        var (emitter, trail) = NewEmitter();
        var repo = new InMemoryListingRepository(emitter, time: null);
        var listing = MakeListing(PublicListingStatus.Draft);

        await repo.UpsertAsync(listing, default);
        await repo.UpsertAsync(listing with { Status = PublicListingStatus.Unlisted }, default);

        Assert.Empty(trail.Records);
    }

    // ─────────── InquiryFormDefense ───────────

    [Fact]
    public async Task DefenseEvaluate_OnReject_Emits_InquiryRejected()
    {
        var (emitter, trail) = NewEmitter();
        var captcha = new InMemoryCaptchaVerifier();   // no token seeded → CAPTCHA fails
        var rate = new InMemoryInquiryRateLimiter();
        var mx = new StubEmailMxResolver { DefaultVerdict = true };
        var defense = new InquiryFormDefense(captcha, rate, mx, emitter);

        var submission = new InquiryFormSubmission
        {
            Tenant = TestTenant,
            CaptchaToken = "bad",
            ClientIp = TestIp,
            ProspectEmail = "a@b.com",
            MessageBody = "Hi",
            ReceivedAt = DateTimeOffset.UtcNow,
        };
        var result = await defense.EvaluateAsync(submission, default);

        Assert.False(result.Passed);
        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.InquiryRejected, trail.Records[0].EventType);
        var body = trail.Records[0].Payload.Payload.Body;
        Assert.Equal("Captcha", body["rejected_at_layer"]);
    }

    [Fact]
    public async Task DefenseEvaluate_OnPass_NoEmission()
    {
        var (emitter, trail) = NewEmitter();
        var captcha = new InMemoryCaptchaVerifier();
        captcha.Seed("good", 0.9);
        var rate = new InMemoryInquiryRateLimiter();
        var mx = new StubEmailMxResolver { DefaultVerdict = true };
        var defense = new InquiryFormDefense(captcha, rate, mx, emitter);

        var submission = new InquiryFormSubmission
        {
            Tenant = TestTenant,
            CaptchaToken = "good",
            ClientIp = TestIp,
            ProspectEmail = "a@b.com",
            MessageBody = "Hi",
            ReceivedAt = DateTimeOffset.UtcNow,
        };
        var result = await defense.EvaluateAsync(submission, default);

        Assert.True(result.Passed);
        Assert.Empty(trail.Records);
    }

    // ─────────── Emitter constructor ───────────

    [Fact]
    public void Emitter_RejectsNullAndDefault()
    {
        var trail = new CapturingAuditTrail();
        Assert.Throws<ArgumentNullException>(() => new PublicListingAuditEmitter(null!, new StubSigner(), TestTenant));
        Assert.Throws<ArgumentNullException>(() => new PublicListingAuditEmitter(trail, null!, TestTenant));
        Assert.Throws<ArgumentException>(() => new PublicListingAuditEmitter(trail, new StubSigner(), default));
    }

    private static PublicListing MakeListing(PublicListingStatus status) => new()
    {
        Id = new PublicListingId(Guid.NewGuid()),
        Tenant = TestTenant,
        Property = PropertyId.NewId(),
        Status = status,
        Headline = "Test",
        Description = "Test description",
        Slug = $"slug-{Guid.NewGuid():N}",
        ShowingAvailability = new ShowingAvailability { Kind = ShowingAvailabilityKind.OpenHouse },
        Redaction = new RedactionPolicy
        {
            Address = AddressRedactionLevel.NeighborhoodOnly,
            IncludeFinancialsForProspect = true,
            IncludeAssetInventoryForApplicant = true,
        },
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
