using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Anchor.Services.Pairing;

/// <summary>
/// Per W#23 Phase 0 — Anchor-side pairing service. Issues / consumes /
/// revokes <see cref="PairingToken"/>s for the iOS Field-Capture app's
/// per-device install identity.
/// </summary>
/// <remarks>
/// <para>
/// The token is HMAC-bound to <c>(PairingTokenId, DeviceId, IssuedAt)</c>
/// using a per-tenant key derived via
/// <c>ITenantKeyProvider.DeriveKeyAsync</c> with purpose label
/// <c>field-pairing-token-hmac</c>. Different tenants derive different
/// keys; tampered or cross-tenant tokens fail HMAC verification.
/// </para>
/// </remarks>
public interface IPairingService
{
    /// <summary>
    /// Issues a new pairing token for the supplied <paramref name="deviceId"/>.
    /// Phase 0 default TTL is 10 minutes (operator pairs the device live).
    /// </summary>
    Task<PairingToken> IssuePairingTokenAsync(
        TenantId tenant,
        string deviceId,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies + consumes a pairing token presented by the iOS app. Returns the
    /// validated token (with <see cref="PairingToken.ConsumedAt"/> stamped) or
    /// null if HMAC fails / token is past <see cref="PairingToken.ExpiresAt"/> /
    /// already consumed / already revoked.
    /// </summary>
    Task<PairingToken?> ConsumePairingTokenAsync(
        TenantId tenant,
        PairingToken presented,
        CancellationToken ct = default);

    /// <summary>Revokes an outstanding pairing token (operator-initiated).</summary>
    Task RevokePairingAsync(
        TenantId tenant,
        string pairingTokenId,
        CancellationToken ct = default);
}
