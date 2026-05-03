using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Anchor.Services.Pairing;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.TenantKey;
using Xunit;

namespace Sunfish.Anchor.Tests;

public sealed class HmacPairingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private const string DeviceId = "abcdef0123456789"; // 16 hex chars

    private static HmacPairingService Build(FakeTime time, FakeKeyProvider? keys = null) =>
        new(keys ?? new FakeKeyProvider(), time);

    [Fact]
    public async Task Issue_ProducesTokenWithExpectedShape()
    {
        var time = new FakeTime(Now);
        var svc = Build(time);

        var token = await svc.IssuePairingTokenAsync(TenantA, DeviceId);

        Assert.Equal(DeviceId, token.DeviceId);
        Assert.Equal(Now, token.IssuedAt);
        Assert.Equal(Now + HmacPairingService.DefaultTtl, token.ExpiresAt);
        Assert.Null(token.ConsumedAt);
        Assert.False(string.IsNullOrEmpty(token.PairingTokenId));
        Assert.False(string.IsNullOrEmpty(token.Hmac));
    }

    [Fact]
    public async Task Issue_ThenConsume_StampsConsumedAt()
    {
        var time = new FakeTime(Now);
        var svc = Build(time);

        var issued = await svc.IssuePairingTokenAsync(TenantA, DeviceId);
        time.Advance(TimeSpan.FromMinutes(2));
        var consumed = await svc.ConsumePairingTokenAsync(TenantA, issued);

        Assert.NotNull(consumed);
        Assert.Equal(Now + TimeSpan.FromMinutes(2), consumed!.ConsumedAt);
        Assert.Equal(issued.PairingTokenId, consumed.PairingTokenId);
    }

    [Fact]
    public async Task Consume_AfterExpiry_ReturnsNull()
    {
        var time = new FakeTime(Now);
        var svc = Build(time);

        var issued = await svc.IssuePairingTokenAsync(TenantA, DeviceId);
        time.Advance(HmacPairingService.DefaultTtl + TimeSpan.FromSeconds(1));
        var result = await svc.ConsumePairingTokenAsync(TenantA, issued);

        Assert.Null(result);
    }

    [Fact]
    public async Task Consume_DoubleConsume_SecondReturnsNull()
    {
        var svc = Build(new FakeTime(Now));
        var issued = await svc.IssuePairingTokenAsync(TenantA, DeviceId);

        var first = await svc.ConsumePairingTokenAsync(TenantA, issued);
        var second = await svc.ConsumePairingTokenAsync(TenantA, issued);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task Consume_RevokedToken_ReturnsNull()
    {
        var svc = Build(new FakeTime(Now));
        var issued = await svc.IssuePairingTokenAsync(TenantA, DeviceId);

        await svc.RevokePairingAsync(TenantA, issued.PairingTokenId);
        var result = await svc.ConsumePairingTokenAsync(TenantA, issued);

        Assert.Null(result);
    }

    [Fact]
    public async Task Consume_TamperedHmac_ReturnsNull()
    {
        var svc = Build(new FakeTime(Now));
        var issued = await svc.IssuePairingTokenAsync(TenantA, DeviceId);
        var tampered = issued with { Hmac = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" };

        var result = await svc.ConsumePairingTokenAsync(TenantA, tampered);

        Assert.Null(result);
    }

    [Fact]
    public async Task Consume_CrossTenant_ReturnsNull()
    {
        var keys = new FakeKeyProvider(); // distinct keys per tenant
        var svc = Build(new FakeTime(Now), keys);

        var issuedForA = await svc.IssuePairingTokenAsync(TenantA, DeviceId);
        // Present token with TenantB — different derived key + token not stored under (B, id).
        var result = await svc.ConsumePairingTokenAsync(TenantB, issuedForA);

        Assert.Null(result);
    }

    [Fact]
    public async Task Consume_DeviceIdMismatch_ReturnsNull()
    {
        var svc = Build(new FakeTime(Now));
        var issued = await svc.IssuePairingTokenAsync(TenantA, DeviceId);
        var swapped = issued with { DeviceId = "ffffffffffffffff" };

        var result = await svc.ConsumePairingTokenAsync(TenantA, swapped);

        Assert.Null(result);
    }

    [Fact]
    public async Task Consume_UnknownTokenId_ReturnsNull()
    {
        var svc = Build(new FakeTime(Now));
        var bogus = new PairingToken(
            PairingTokenId: "AAAAAAAAAAAAAAAAAAAAAAAAAA",
            DeviceId: DeviceId,
            IssuedAt: Now,
            ExpiresAt: Now + TimeSpan.FromMinutes(10),
            ConsumedAt: null,
            Hmac: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        var result = await svc.ConsumePairingTokenAsync(TenantA, bogus);

        Assert.Null(result);
    }

    [Fact]
    public async Task Issue_NullOrEmptyDevice_Throws()
    {
        var svc = Build(new FakeTime(Now));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.IssuePairingTokenAsync(TenantA, ""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => svc.IssuePairingTokenAsync(TenantA, null!));
    }

    [Fact]
    public void Constructor_NullKeyProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HmacPairingService(null!));
    }

    [Fact]
    public async Task IssueAsync_HonorsCancellation()
    {
        var svc = Build(new FakeTime(Now));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            svc.IssuePairingTokenAsync(TenantA, DeviceId, cts.Token));
    }

    [Fact]
    public void HmacKeyPurpose_MatchesSpec()
    {
        Assert.Equal("field-pairing-token-hmac", HmacPairingService.HmacKeyPurpose);
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class FakeKeyProvider : ITenantKeyProvider
    {
        public Task<ReadOnlyMemory<byte>> DeriveKeyAsync(TenantId tenant, string purpose, CancellationToken ct)
        {
            // Distinct deterministic key per (tenant, purpose) — sufficient for unit tests.
            var seed = System.Text.Encoding.UTF8.GetBytes($"{tenant.Value}:{purpose}");
            var key = System.Security.Cryptography.SHA256.HashData(seed);
            return Task.FromResult<ReadOnlyMemory<byte>>(key);
        }
    }
}
