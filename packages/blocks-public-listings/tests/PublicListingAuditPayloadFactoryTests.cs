using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PublicListings.Audit;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Defense;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.PublicListings.Tests;

/// <summary>
/// W#28 Phase 6 — schema snapshot tests for the 6 public-listing
/// AuditEventType + PublicListingAuditPayloadFactory body shapes.
/// </summary>
public sealed class PublicListingAuditPayloadFactoryTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly PublicListingId TestListing = new(Guid.NewGuid());

    [Fact]
    public void AuditEventType_PublicListings_AllSixEventsAddressable()
    {
        // The 6 events declared by W#28 Phase 6 (4 net-new + 2 reused
        // from W#22 ADR 0057 InquiryAccepted/Rejected).
        var values = new[]
        {
            AuditEventType.PublicListingPublished.Value,
            AuditEventType.PublicListingUnlisted.Value,
            AuditEventType.InquirySubmitted.Value,
            AuditEventType.InquiryRejected.Value,             // reused
            AuditEventType.CapabilityPromotedToProspect.Value,
            AuditEventType.CapabilityPromotedToApplicant.Value,
        };
        Assert.Equal(6, values.Length);
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void PublicListingPublished_BodyHasExpectedKeys()
    {
        var listing = MakeListing();
        var body = PublicListingAuditPayloadFactory.PublicListingPublished(listing).Body;

        Assert.Equal(listing.Id.Value, body["listing_id"]);
        Assert.Equal(listing.Tenant.Value, body["tenant"]);
        Assert.Equal(listing.Slug, body["slug"]);
        Assert.Equal(listing.Headline, body["headline"]);
    }

    [Fact]
    public void PublicListingUnlisted_BodyMinimalShape()
    {
        var listing = MakeListing() with { Status = PublicListingStatus.Unlisted };
        var body = PublicListingAuditPayloadFactory.PublicListingUnlisted(listing).Body;

        Assert.Equal(listing.Id.Value, body["listing_id"]);
        Assert.Equal(listing.Slug, body["slug"]);
    }

    [Fact]
    public void InquirySubmitted_BodyHasExpectedKeys()
    {
        var body = PublicListingAuditPayloadFactory
            .InquirySubmitted(TestTenant, TestListing, "198.51.100.42")
            .Body;

        Assert.Equal(TestTenant.Value, body["tenant"]);
        Assert.Equal(TestListing.Value, body["listing_id"]);
        Assert.Equal("198.51.100.42", body["client_ip"]);
    }

    [Fact]
    public void InquiryRejected_BodyCarriesRejectingLayer()
    {
        var body = PublicListingAuditPayloadFactory
            .InquiryRejected(TestTenant, TestListing, InquiryDefenseLayer.Captcha, "Score below threshold")
            .Body;

        Assert.Equal("Captcha", body["rejected_at_layer"]);
        Assert.Equal("Score below threshold", body["reason"]);
    }

    [Fact]
    public void CapabilityPromotedToProspect_BodyCarriesCapabilityShape()
    {
        var capability = new ProspectCapability
        {
            Id = new ProspectCapabilityId(Guid.NewGuid()),
            MacaroonToken = "test-token",
            IssuedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero),
            AccessibleListings = new[] { TestListing, new PublicListingId(Guid.NewGuid()) },
        };
        var body = PublicListingAuditPayloadFactory
            .CapabilityPromotedToProspect(TestTenant, capability, "alice@example.com")
            .Body;

        Assert.Equal(capability.Id.Value, body["capability_id"]);
        Assert.Equal(2, body["accessible_listing_count"]);
        Assert.Equal("alice@example.com", body["verified_email"]);
    }

    [Fact]
    public void CapabilityPromotedToApplicant_BodyMinimalShape()
    {
        var body = PublicListingAuditPayloadFactory
            .CapabilityPromotedToApplicant(TestTenant, "app-id-1", "operator")
            .Body;

        Assert.Equal(TestTenant.Value, body["tenant"]);
        Assert.Equal("app-id-1", body["application_id"]);
        Assert.Equal("operator", body["actor"]);
    }

    private static PublicListing MakeListing() => new()
    {
        Id = TestListing,
        Tenant = TestTenant,
        Property = PropertyId.NewId(),
        Status = PublicListingStatus.Published,
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
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
