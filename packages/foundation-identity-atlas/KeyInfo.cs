using System;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Current key-pair metadata for an actor per ADR 0066 §Phase 3 / ADR 0087.
/// Raw public key bytes used by <see cref="Crypto.KeyFingerprint.FromPublicKey"/>
/// to derive the canonical fingerprint.
/// </summary>
/// <param name="PublicKey">Raw Ed25519 public-key bytes (32 bytes).</param>
/// <param name="HistoricalKeyCount">Count of rotated keys; 0 = first key, never rotated.</param>
/// <param name="RotationInProgress">True when a rotation Standing Order is in-flight.</param>
/// <param name="RotationWindowExpiry">
/// Deadline by which the rotation must complete; null when no rotation is in progress.
/// </param>
public sealed record KeyInfo(
    byte[] PublicKey,
    int HistoricalKeyCount,
    bool RotationInProgress,
    DateTimeOffset? RotationWindowExpiry);
