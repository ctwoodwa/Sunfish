using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Append-only revocation log for <see cref="ThreadToken"/>s.
/// <see cref="HmacThreadTokenIssuer"/> consults this on
/// <see cref="IThreadTokenIssuer.VerifyAsync"/>; tokens whose hash is in
/// the log are rejected.
/// </summary>
public interface IRevokedTokenStore
{
    /// <summary>Records a token's HMAC-portion as revoked. Idempotent.</summary>
    /// <param name="tenant">Tenant the token was scoped to.</param>
    /// <param name="tokenHmacFragment">The HMAC portion of the token (the prefix before the <c>"."</c> separator).</param>
    /// <param name="ct">Cancellation token.</param>
    Task AppendAsync(TenantId tenant, string tokenHmacFragment, CancellationToken ct);

    /// <summary>Returns true iff the given HMAC fragment has been revoked for the tenant.</summary>
    /// <param name="tenant">Tenant scope.</param>
    /// <param name="tokenHmacFragment">The HMAC portion of the token.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> IsRevokedAsync(TenantId tenant, string tokenHmacFragment, CancellationToken ct);
}
