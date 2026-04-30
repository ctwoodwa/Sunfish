using System.Net;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Macaroons;
using Xunit;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Tests;

/// <summary>
/// W#22 Phase 5: leasing-pipeline-side validation hooks. Verifies the
/// validator gates listing-existence + tenant-match + Published-status +
/// email-format and that <see cref="InMemoryLeasingPipelineService"/>
/// throws <see cref="InquiryValidationException"/> when wired.
/// </summary>
public sealed class InquiryValidationTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");

    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static PublicListing MakeListing(TenantId tenant, PublicListingId id, PublicListingStatus status) => new()
    {
        Id = id,
        Tenant = tenant,
        Property = PropertyId.NewId(),
        Status = status,
        Headline = "Test Listing",
        Description = "Test description",
        Slug = "test-listing",
        ShowingAvailability = new ShowingAvailability { Kind = ShowingAvailabilityKind.OpenHouse },
        Redaction = new RedactionPolicy
        {
            Address = AddressRedactionLevel.NeighborhoodOnly,
            IncludeFinancialsForProspect = true,
            IncludeAssetInventoryForApplicant = true,
        },
        CreatedAt = Now.AddDays(-2),
    };

    private static PublicInquiryRequest MakeRequest(TenantId tenant, PublicListingId listing, string email = "alice@example.com") => new()
    {
        Tenant = tenant,
        Listing = listing,
        ProspectName = "Alice Smith",
        ProspectEmail = email,
        MessageBody = "Hi, is this still available?",
        ClientIp = TestIp,
        UserAgent = "Mozilla/5.0",
    };

    private static (DefaultInquiryValidator validator, InMemoryListingRepository repo) NewValidator()
    {
        var repo = new InMemoryListingRepository();
        var validator = new DefaultInquiryValidator(repo);
        return (validator, repo);
    }

    [Fact]
    public async Task ValidListing_PassesValidation()
    {
        var (validator, repo) = NewValidator();
        var listingId = new PublicListingId(Guid.NewGuid());
        await repo.UpsertAsync(MakeListing(TenantA, listingId, PublicListingStatus.Published), default);

        var result = await validator.ValidateAsync(MakeRequest(TenantA, listingId), default);

        Assert.True(result.Passed);
        Assert.Null(result.FailedAt);
    }

    [Fact]
    public async Task UnknownListing_FailsValidation()
    {
        var (validator, _) = NewValidator();

        var result = await validator.ValidateAsync(MakeRequest(TenantA, new PublicListingId(Guid.NewGuid())), default);

        Assert.False(result.Passed);
        Assert.Equal(InquiryValidationFailure.ListingNotFound, result.FailedAt);
    }

    [Fact]
    public async Task DraftListing_FailsValidation()
    {
        var (validator, repo) = NewValidator();
        var listingId = new PublicListingId(Guid.NewGuid());
        await repo.UpsertAsync(MakeListing(TenantA, listingId, PublicListingStatus.Draft), default);

        var result = await validator.ValidateAsync(MakeRequest(TenantA, listingId), default);

        Assert.False(result.Passed);
        Assert.Equal(InquiryValidationFailure.ListingNotPublished, result.FailedAt);
    }

    [Fact]
    public async Task UnlistedListing_FailsValidation()
    {
        var (validator, repo) = NewValidator();
        var listingId = new PublicListingId(Guid.NewGuid());
        await repo.UpsertAsync(MakeListing(TenantA, listingId, PublicListingStatus.Unlisted), default);

        var result = await validator.ValidateAsync(MakeRequest(TenantA, listingId), default);

        Assert.False(result.Passed);
        Assert.Equal(InquiryValidationFailure.ListingNotPublished, result.FailedAt);
    }

    [Fact]
    public async Task TenantMismatch_FailsValidation()
    {
        var (validator, repo) = NewValidator();
        var listingId = new PublicListingId(Guid.NewGuid());
        await repo.UpsertAsync(MakeListing(TenantA, listingId, PublicListingStatus.Published), default);

        // Request is for tenant-b but listing belongs to tenant-a — repo lookup
        // is tenant-scoped + returns null, so the failure is ListingNotFound.
        var result = await validator.ValidateAsync(MakeRequest(TenantB, listingId), default);

        Assert.False(result.Passed);
        Assert.Equal(InquiryValidationFailure.ListingNotFound, result.FailedAt);
    }

    [Fact]
    public async Task BadEmail_FailsBeforeListingLookup()
    {
        var (validator, _) = NewValidator();
        var result = await validator.ValidateAsync(MakeRequest(TenantA, new PublicListingId(Guid.NewGuid()), email: "not-an-email"), default);

        Assert.False(result.Passed);
        Assert.Equal(InquiryValidationFailure.EmailFormat, result.FailedAt);
    }

    [Fact]
    public async Task SubmitInquiry_WithValidator_RejectsInvalidRequest()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var promoter = new MacaroonCapabilityPromoter(new DefaultMacaroonIssuer(keys), TenantA, Array.Empty<PublicListingId>());
        var (validator, _) = NewValidator();
        var svc = new InMemoryLeasingPipelineService(promoter, validator, time: null);

        // Listing not seeded → validation fails.
        await Assert.ThrowsAsync<InquiryValidationException>(() =>
            ((IPublicInquiryService)svc).SubmitInquiryAsync(
                MakeRequest(TenantA, new PublicListingId(Guid.NewGuid())),
                MakeAnonymousCapability(),
                default));
    }

    [Fact]
    public async Task SubmitInquiry_WithValidator_AcceptsValidRequest()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var promoter = new MacaroonCapabilityPromoter(new DefaultMacaroonIssuer(keys), TenantA, Array.Empty<PublicListingId>());
        var (validator, repo) = NewValidator();
        var listingId = new PublicListingId(Guid.NewGuid());
        await repo.UpsertAsync(MakeListing(TenantA, listingId, PublicListingStatus.Published), default);
        var svc = new InMemoryLeasingPipelineService(promoter, validator, time: null);

        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(
            MakeRequest(TenantA, listingId),
            MakeAnonymousCapability(),
            default);

        Assert.Equal(InquiryStatus.Submitted, inquiry.Status);
        Assert.Equal(listingId, inquiry.Listing);
    }

    [Fact]
    public async Task SubmitInquiry_WithoutValidator_AcceptsAllRequests()
    {
        // Validator is opt-in (Phase 5); without one, the service accepts
        // raw requests just like Phase 2 + Phase 1 expectations.
        var svc = new InMemoryLeasingPipelineService();

        var inquiry = await ((IPublicInquiryService)svc).SubmitInquiryAsync(
            MakeRequest(TenantA, new PublicListingId(Guid.NewGuid())),
            MakeAnonymousCapability(),
            default);

        Assert.Equal(InquiryStatus.Submitted, inquiry.Status);
    }

    [Fact]
    public void Constructor_RejectsNullListings()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultInquiryValidator(null!));
    }

    [Fact]
    public void InquiryValidationException_CarriesFailureCategory()
    {
        var ex = new InquiryValidationException(InquiryValidationFailure.TenantMismatch, "test");
        Assert.Equal(InquiryValidationFailure.TenantMismatch, ex.Failure);
        Assert.Equal("test", ex.Message);
    }

    private static AnonymousCapability MakeAnonymousCapability() => new()
    {
        Token = "anon-1",
        IssuedAt = Now,
        ExpiresAt = Now.AddMinutes(30),
    };
}
