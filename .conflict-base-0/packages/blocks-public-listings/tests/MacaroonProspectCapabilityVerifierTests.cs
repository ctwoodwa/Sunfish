using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Macaroons;
using Xunit;

namespace Sunfish.Blocks.PublicListings.Tests;

public sealed class MacaroonProspectCapabilityVerifierTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static (MacaroonCapabilityPromoter promoter, MacaroonProspectCapabilityVerifier verifier, IReadOnlyList<PublicListingId> listings)
        NewPair(TenantId? tenant = null, TimeSpan? ttl = null, TimeProvider? time = null)
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var listings = new[] { new PublicListingId(Guid.NewGuid()), new PublicListingId(Guid.NewGuid()) };
        var promoter = new MacaroonCapabilityPromoter(issuer, tenant ?? TenantA, listings, ttl: ttl, time: time);
        var verifier = new MacaroonProspectCapabilityVerifier(keys);
        return (promoter, verifier, listings);
    }

    [Fact]
    public async Task RoundTrip_VerifiesAndProjectsCapability()
    {
        var (promoter, verifier, listings) = NewPair(time: new FakeTimeProvider(FixedNow));
        var cap = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);

        var verified = await verifier.VerifyAsync(cap.MacaroonToken, TenantA, listings[0], FixedNow.AddMinutes(1), default);

        Assert.Equal(cap.Id.Value, verified.CapabilityId);
        Assert.Equal(TenantA, verified.Tenant);
        Assert.Equal("alice@example.com", verified.Email);
        Assert.Equal(FixedNow.AddDays(7), verified.ExpiresAt);
        Assert.Equal(2, verified.AllowedListings.Count);
        Assert.Contains(listings[0], verified.AllowedListings);
    }

    [Fact]
    public async Task WrongTenant_ThrowsWithReason()
    {
        var (promoter, verifier, listings) = NewPair(tenant: TenantA);
        var cap = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);

        var ex = await Assert.ThrowsAsync<ProspectCapabilityDeniedException>(
            () => verifier.VerifyAsync(cap.MacaroonToken, TenantB, listings[0], DateTimeOffset.UtcNow, default));

        Assert.Contains("wrong-tenant", ex.Reason);
    }

    [Fact]
    public async Task ListingNotInAllowedSet_Throws()
    {
        var (promoter, verifier, _) = NewPair();
        var cap = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);
        var unrelatedListing = new PublicListingId(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<ProspectCapabilityDeniedException>(
            () => verifier.VerifyAsync(cap.MacaroonToken, TenantA, unrelatedListing, DateTimeOffset.UtcNow, default));

        Assert.Contains("listing-not-in-allowed-set", ex.Reason);
    }

    [Fact]
    public async Task Expired_Throws()
    {
        var (promoter, verifier, listings) = NewPair(ttl: TimeSpan.FromMinutes(5), time: new FakeTimeProvider(FixedNow));
        var cap = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);

        var ex = await Assert.ThrowsAsync<ProspectCapabilityDeniedException>(
            () => verifier.VerifyAsync(cap.MacaroonToken, TenantA, listings[0], FixedNow.AddHours(1), default));

        Assert.Contains("expired", ex.Reason);
    }

    [Fact]
    public async Task SignatureTampered_RejectedAsSignatureMismatch()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var listings = new[] { new PublicListingId(Guid.NewGuid()) };
        var promoter = new MacaroonCapabilityPromoter(issuer, TenantA, listings);

        // Verifier with a different root key store → signature won't match.
        var otherKeys = new InMemoryRootKeyStore();
        otherKeys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99 });
        var verifier = new MacaroonProspectCapabilityVerifier(otherKeys);

        var cap = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);

        var ex = await Assert.ThrowsAsync<ProspectCapabilityDeniedException>(
            () => verifier.VerifyAsync(cap.MacaroonToken, TenantA, listings[0], DateTimeOffset.UtcNow, default));

        Assert.Equal("signature-mismatch", ex.Reason);
    }

    [Fact]
    public async Task NoRootKeyForLocation_Throws()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var listings = new[] { new PublicListingId(Guid.NewGuid()) };
        var promoter = new MacaroonCapabilityPromoter(issuer, TenantA, listings);

        // Empty keystore → no root key for the location → no-root-key denial.
        var emptyKeys = new InMemoryRootKeyStore();
        var verifier = new MacaroonProspectCapabilityVerifier(emptyKeys);
        var cap = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);

        var ex = await Assert.ThrowsAsync<ProspectCapabilityDeniedException>(
            () => verifier.VerifyAsync(cap.MacaroonToken, TenantA, listings[0], DateTimeOffset.UtcNow, default));

        Assert.Contains("no-root-key", ex.Reason);
    }

    [Fact]
    public async Task DecodeFailure_RejectedWithDecodeMessage()
    {
        var keys = new InMemoryRootKeyStore();
        var verifier = new MacaroonProspectCapabilityVerifier(keys);

        var ex = await Assert.ThrowsAsync<ProspectCapabilityDeniedException>(
            () => verifier.VerifyAsync("not-a-valid-macaroon-token!!!", TenantA, new PublicListingId(Guid.NewGuid()), DateTimeOffset.UtcNow, default));

        Assert.Contains("decode failed", ex.Reason);
    }

    [Fact]
    public async Task NullOrEmptyToken_ThrowsArgumentException()
    {
        var keys = new InMemoryRootKeyStore();
        var verifier = new MacaroonProspectCapabilityVerifier(keys);

        await Assert.ThrowsAsync<ArgumentException>(
            () => verifier.VerifyAsync(string.Empty, TenantA, new PublicListingId(Guid.NewGuid()), DateTimeOffset.UtcNow, default));
    }

    [Fact]
    public async Task RoundTrip_CapabilityIdMatchesPromoter()
    {
        var (promoter, verifier, listings) = NewPair();
        var cap = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);

        var verified = await verifier.VerifyAsync(cap.MacaroonToken, TenantA, listings[0], DateTimeOffset.UtcNow, default);

        Assert.Equal(cap.Id.Value, verified.CapabilityId);
    }

    [Fact]
    public void ProspectCaveatNames_ConstantsAreStable()
    {
        // Pin the caveat keys — issuer + verifier MUST agree on these.
        Assert.Equal("capability-id", ProspectCaveatNames.CapabilityId);
        Assert.Equal("tenant", ProspectCaveatNames.Tenant);
        Assert.Equal("email", ProspectCaveatNames.Email);
        Assert.Equal("email-verified", ProspectCaveatNames.EmailVerified);
        Assert.Equal("issued-from-ip", ProspectCaveatNames.IssuedFromIp);
        Assert.Equal("expires", ProspectCaveatNames.Expires);
        Assert.Equal("listing-allowed", ProspectCaveatNames.ListingAllowed);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
