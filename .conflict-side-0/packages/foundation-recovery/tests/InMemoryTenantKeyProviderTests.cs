using System.Threading;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.TenantKey;
using Xunit;

namespace Sunfish.Foundation.Recovery.Tests;

public sealed class InMemoryTenantKeyProviderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task DeriveKey_Produces32Bytes()
    {
        var p = new InMemoryTenantKeyProvider();
        var key = await p.DeriveKeyAsync(new TenantId("tenant-a"), "thread-token-hmac", Ct);
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public async Task DeriveKey_DifferentTenants_DifferentKeys()
    {
        var p = new InMemoryTenantKeyProvider();
        var k1 = await p.DeriveKeyAsync(new TenantId("tenant-a"), "thread-token-hmac", Ct);
        var k2 = await p.DeriveKeyAsync(new TenantId("tenant-b"), "thread-token-hmac", Ct);
        Assert.False(k1.Span.SequenceEqual(k2.Span));
    }

    [Fact]
    public async Task DeriveKey_DifferentPurposes_DifferentKeys()
    {
        var p = new InMemoryTenantKeyProvider();
        var k1 = await p.DeriveKeyAsync(new TenantId("tenant-a"), "thread-token-hmac", Ct);
        var k2 = await p.DeriveKeyAsync(new TenantId("tenant-a"), "encrypted-field-aes", Ct);
        Assert.False(k1.Span.SequenceEqual(k2.Span));
    }

    [Fact]
    public async Task DeriveKey_IsIdempotent()
    {
        var p = new InMemoryTenantKeyProvider();
        var k1 = await p.DeriveKeyAsync(new TenantId("tenant-a"), "thread-token-hmac", Ct);
        var k2 = await p.DeriveKeyAsync(new TenantId("tenant-a"), "thread-token-hmac", Ct);
        Assert.True(k1.Span.SequenceEqual(k2.Span));
    }
}
