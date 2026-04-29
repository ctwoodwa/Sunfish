using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Mints, verifies, and revokes <see cref="ThreadToken"/>s per ADR 0052
/// amendment A2 (HMAC-SHA256 + per-tenant key from
/// <c>Sunfish.Foundation.Recovery.ITenantKeyProvider</c> + 90-day default
/// TTL + revocation log). Phase 3 of W#20 ships the
/// <c>HmacThreadTokenIssuer</c> implementation; Phase 1 is the contract
/// only.
/// </summary>
public interface IThreadTokenIssuer
{
    /// <summary>Mints a fresh token for the given tenant + thread.</summary>
    /// <param name="tenant">Tenant the token is scoped to; tokens leaked across tenants must be rejected on verify.</param>
    /// <param name="threadId">Thread the token routes to.</param>
    /// <param name="notBefore">Wall-clock time the token becomes valid.</param>
    /// <param name="ttl">Optional TTL override; defaults to <see cref="MessagingProviderConfig.ThreadTokenTtl"/> or 90 days.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ThreadToken> MintAsync(
        TenantId tenant,
        ThreadId threadId,
        DateTimeOffset notBefore,
        TimeSpan? ttl,
        CancellationToken ct);

    /// <summary>Verifies a token returned via inbound parsing — checks HMAC, TTL, revocation log, and tenant scope.</summary>
    /// <param name="tenant">Tenant the inbound message was scoped to (from provider routing).</param>
    /// <param name="token">Token extracted from the inbound payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decoded thread id when verification passes; null when the token is invalid, expired, revoked, or scoped to a different tenant.</returns>
    Task<ThreadId?> VerifyAsync(TenantId tenant, ThreadToken token, CancellationToken ct);

    /// <summary>Revokes an active token (e.g., after a thread is closed or a participant is removed). Idempotent.</summary>
    /// <param name="tenant">Tenant scope.</param>
    /// <param name="token">Token to revoke.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RevokeAsync(TenantId tenant, ThreadToken token, CancellationToken ct);
}
