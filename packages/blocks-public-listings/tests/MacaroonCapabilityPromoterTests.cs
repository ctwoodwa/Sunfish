using System.Net;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Macaroons;
using Xunit;

namespace Sunfish.Blocks.PublicListings.Tests;

public sealed class MacaroonCapabilityPromoterTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");

    private static (MacaroonCapabilityPromoter promoter, IRootKeyStore keys, IReadOnlyList<PublicListingId> listings) NewPromoter(TimeSpan? ttl = null, TimeProvider? time = null)
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        var listings = new[] { new PublicListingId(Guid.NewGuid()), new PublicListingId(Guid.NewGuid()) };
        var promoter = new MacaroonCapabilityPromoter(issuer, Tenant, listings, ttl: ttl, time: time);
        return (promoter, keys, listings);
    }

    [Fact]
    public async Task PromoteToProspectAsync_ReturnsCapability_WithMacaroonToken()
    {
        var (promoter, _, listings) = NewPromoter();
        var cap = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);

        Assert.NotEqual(default, cap.Id);
        Assert.False(string.IsNullOrEmpty(cap.MacaroonToken));
        Assert.Equal(listings, cap.AccessibleListings);
    }

    [Fact]
    public async Task PromoteToProspectAsync_AppliesDefaultSevenDayTtl()
    {
        var fixedNow = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fixedNow);
        var (promoter, _, _) = NewPromoter(time: time);

        var cap = await promoter.PromoteToProspectAsync("a@b.com", TestIp, default);

        Assert.Equal(fixedNow, cap.IssuedAt);
        Assert.Equal(fixedNow.AddDays(7), cap.ExpiresAt);
    }

    [Fact]
    public async Task PromoteToProspectAsync_HonorsCustomTtl()
    {
        var fixedNow = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fixedNow);
        var (promoter, _, _) = NewPromoter(ttl: TimeSpan.FromHours(2), time: time);

        var cap = await promoter.PromoteToProspectAsync("a@b.com", TestIp, default);

        Assert.Equal(fixedNow.AddHours(2), cap.ExpiresAt);
    }

    [Fact]
    public async Task EveryCallProducesDistinctCapabilityId()
    {
        var (promoter, _, _) = NewPromoter();
        var c1 = await promoter.PromoteToProspectAsync("a@b.com", TestIp, default);
        var c2 = await promoter.PromoteToProspectAsync("a@b.com", TestIp, default);

        Assert.NotEqual(c1.Id, c2.Id);
        Assert.NotEqual(c1.MacaroonToken, c2.MacaroonToken);
    }

    [Fact]
    public async Task MacaroonToken_IsBase64Url_Decodable()
    {
        var (promoter, _, _) = NewPromoter();
        var cap = await promoter.PromoteToProspectAsync("a@b.com", TestIp, default);

        var decoded = MacaroonCodec.DecodeBase64Url(cap.MacaroonToken);

        Assert.Equal(MacaroonCapabilityPromoter.DefaultLocation, decoded.Location);
        Assert.Equal(cap.Id.Value.ToString("D"), decoded.Identifier);
    }

    [Fact]
    public async Task DecodedMacaroon_CarriesAllExpectedCaveats()
    {
        var fixedNow = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fixedNow);
        var (promoter, _, listings) = NewPromoter(time: time);

        var cap = await promoter.PromoteToProspectAsync("alice@example.com", TestIp, default);
        var decoded = MacaroonCodec.DecodeBase64Url(cap.MacaroonToken);
        var predicates = decoded.Caveats.Select(c => c.Predicate).ToList();

        Assert.Contains($"capability-id = {cap.Id.Value:D}", predicates);
        Assert.Contains("tenant = tenant-a", predicates);
        Assert.Contains("email = alice@example.com", predicates);
        Assert.Contains("email-verified = true", predicates);
        Assert.Contains($"issued-from-ip = {TestIp}", predicates);
        Assert.Contains($"expires = {fixedNow.AddDays(7):O}", predicates);
        foreach (var listingId in listings)
        {
            Assert.Contains($"listing-allowed = {listingId.Value:D}", predicates);
        }
    }

    [Fact]
    public async Task MacaroonSignature_IsNonZero_AndStable()
    {
        var (promoter, _, _) = NewPromoter();
        var cap = await promoter.PromoteToProspectAsync("a@b.com", TestIp, default);
        var decoded = MacaroonCodec.DecodeBase64Url(cap.MacaroonToken);

        Assert.NotNull(decoded.Signature);
        Assert.Equal(32, decoded.Signature.Length); // HMAC-SHA256 = 32 bytes
        Assert.NotEqual(new byte[32], decoded.Signature);
    }

    [Fact]
    public async Task PromoteToProspectAsync_ThrowsOn_NullOrEmpty_Email()
    {
        var (promoter, _, _) = NewPromoter();
        await Assert.ThrowsAsync<ArgumentException>(() => promoter.PromoteToProspectAsync("", TestIp, default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => promoter.PromoteToProspectAsync(null!, TestIp, default));
    }

    [Fact]
    public async Task PromoteToProspectAsync_ThrowsOn_NullIp()
    {
        var (promoter, _, _) = NewPromoter();
        await Assert.ThrowsAsync<ArgumentNullException>(() => promoter.PromoteToProspectAsync("a@b.com", null!, default));
    }

    [Fact]
    public void Constructor_RejectsDefaultTenant()
    {
        var keys = new InMemoryRootKeyStore();
        keys.Set(MacaroonCapabilityPromoter.DefaultLocation, new byte[32]);
        var issuer = new DefaultMacaroonIssuer(keys);
        Assert.Throws<ArgumentException>(() =>
            new MacaroonCapabilityPromoter(issuer, default, Array.Empty<PublicListingId>()));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
