using System;

namespace Sunfish.Anchor.Services.Pairing;

/// <summary>
/// Per W#23 Phase 0 — minimal pairing-token record per W#19 stub precedent.
/// HMAC binds <see cref="PairingTokenId"/> + <see cref="DeviceId"/> +
/// <see cref="IssuedAt"/> against a tenant-derived key (purpose
/// <c>field-pairing-token-hmac</c>).
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the W#18 <c>VendorMagicLink</c> pattern (hand-off precedent). The
/// token is short-lived (default 10-minute TTL — operator pairs the device in
/// front of them); single-use (the <see cref="ConsumedAt"/> stamp marks
/// consumption); revocable via <c>RevokePairingAsync</c>.
/// </para>
/// </remarks>
/// <param name="PairingTokenId">Random 16-byte token id, base32-encoded.</param>
/// <param name="DeviceId">Device id derived from the iOS install Ed25519 public key (first 16 hex chars of SHA-256).</param>
/// <param name="IssuedAt">Anchor-stamped issuance timestamp (UTC).</param>
/// <param name="ExpiresAt">UTC instant past which the token is rejected.</param>
/// <param name="ConsumedAt">UTC instant of consumption (single-use); null if not yet consumed.</param>
/// <param name="Hmac">HMAC-SHA256 over UTF-8 bytes of <c>{PairingTokenId}:{DeviceId}:{IssuedAt:O}</c>; base32-encoded.</param>
public sealed record PairingToken(
    string PairingTokenId,
    string DeviceId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt,
    string Hmac);
