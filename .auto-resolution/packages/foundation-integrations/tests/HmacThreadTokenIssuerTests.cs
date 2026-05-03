using System.Threading;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Messaging;
using Sunfish.Foundation.Recovery.TenantKey;
using Xunit;

namespace Sunfish.Foundation.Integrations.Tests;

public sealed class HmacThreadTokenIssuerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");

    private static (HmacThreadTokenIssuer issuer, IRevokedTokenStore log) NewIssuer()
    {
        var keyProvider = new InMemoryTenantKeyProvider();
        var log = new InMemoryRevokedTokenStore();
        return (new HmacThreadTokenIssuer(keyProvider, log), log);
    }

    [Fact]
    public async Task Mint_ProducesNonEmptyToken_WithDotSeparator()
    {
        var (issuer, _) = NewIssuer();
        var token = await issuer.MintAsync(TenantA, new ThreadId(Guid.NewGuid()), DateTimeOffset.UtcNow, ttl: null, Ct);
        Assert.False(string.IsNullOrEmpty(token.Value));
        Assert.Contains(".", token.Value);
    }

    [Fact]
    public async Task Verify_AcceptsRecentToken()
    {
        var (issuer, _) = NewIssuer();
        var token = await issuer.MintAsync(TenantA, new ThreadId(Guid.NewGuid()), DateTimeOffset.UtcNow, ttl: null, Ct);

        var resolved = await issuer.VerifyAsync(TenantA, token, Ct);
        Assert.NotNull(resolved); // Phase 1 returns sentinel ThreadId(Guid.Empty); not null = valid
    }

    [Fact]
    public async Task Verify_RejectsExpiredToken()
    {
        var (issuer, _) = NewIssuer();
        // Mint a token with notBefore far in the past so default 90-day TTL has expired.
        var token = await issuer.MintAsync(TenantA, new ThreadId(Guid.NewGuid()), DateTimeOffset.UtcNow.AddDays(-200), ttl: null, Ct);

        var resolved = await issuer.VerifyAsync(TenantA, token, Ct);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Verify_RejectsRevokedToken()
    {
        var (issuer, _) = NewIssuer();
        var token = await issuer.MintAsync(TenantA, new ThreadId(Guid.NewGuid()), DateTimeOffset.UtcNow, ttl: null, Ct);

        await issuer.RevokeAsync(TenantA, token, Ct);

        var resolved = await issuer.VerifyAsync(TenantA, token, Ct);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Verify_RejectsMalformedToken()
    {
        var (issuer, _) = NewIssuer();
        var resolved = await issuer.VerifyAsync(TenantA, new ThreadToken("nodot"), Ct);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Revoke_IsIdempotent()
    {
        var (issuer, _) = NewIssuer();
        var token = await issuer.MintAsync(TenantA, new ThreadId(Guid.NewGuid()), DateTimeOffset.UtcNow, ttl: null, Ct);
        await issuer.RevokeAsync(TenantA, token, Ct);
        await issuer.RevokeAsync(TenantA, token, Ct); // should not throw
        Assert.Null(await issuer.VerifyAsync(TenantA, token, Ct));
    }

    [Fact]
    public async Task Revoke_IsTenantScoped()
    {
        var (issuer, _) = NewIssuer();
        var token = await issuer.MintAsync(TenantA, new ThreadId(Guid.NewGuid()), DateTimeOffset.UtcNow, ttl: null, Ct);

        // Revocation in a different tenant doesn't affect the original tenant's verify.
        await issuer.RevokeAsync(TenantB, token, Ct);
        Assert.NotNull(await issuer.VerifyAsync(TenantA, token, Ct));
    }
}
