using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Canonical display form for a cryptographic key fingerprint per ADR
/// 0066 §5 (OQ-6 council disposition: hex SHA-256 with <c>':'</c>
/// group-separators every 2 bytes — 32 bytes × 2 hex chars + 31
/// separators = 95-char canonical string).
/// </summary>
/// <remarks>
/// <para>
/// <b>Package placement (W#53 P1b):</b> the hand-off cited
/// <c>foundation-recovery</c>, but a ui-core → foundation-recovery
/// reference would form a cycle (<c>foundation-recovery → kernel-security
/// → ui-core</c> already exists). KeyFingerprint is a tiny crypto value
/// type with no recovery-specific dependencies, so it lives here in
/// <c>foundation/Crypto/</c> alongside <see cref="PrincipalId"/> —
/// sibling to the other crypto value types and reachable from every
/// tier without cycle.
/// </para>
/// <para>
/// The <c>readonly record struct</c> wraps the canonical string form
/// rather than the raw 32-byte digest to keep the value type cheap to
/// pass through the UI projection layer. Implementations that compute a
/// fingerprint from a <c>byte[32]</c> SHA-256 result do the formatting
/// at the boundary; consumers within UI / Atlas projections never see
/// the raw bytes.
/// </para>
/// </remarks>
[JsonConverter(typeof(KeyFingerprintJsonConverter))]
public readonly record struct KeyFingerprint(string Value)
{
    /// <summary>
    /// Total length of the canonical string: 64 hex chars + 31 colons.
    /// </summary>
    public const int CanonicalLength = 95;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Computes the canonical <see cref="KeyFingerprint"/> from a raw
    /// public-key byte array. Per ADR 0066 §5: SHA-256 hash of the key
    /// bytes, formatted as 95-char hex-with-colons.
    /// </summary>
    public static KeyFingerprint FromPublicKey(byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        var hash = SHA256.HashData(publicKey);
        var sb = new StringBuilder(CanonicalLength);
        for (int i = 0; i < hash.Length; i++)
        {
            if (i > 0) sb.Append(':');
            sb.Append(hash[i].ToString("X2"));
        }
        return new KeyFingerprint(sb.ToString());
    }

    /// <summary>
    /// Parse a canonical 95-character hex-with-colons fingerprint.
    /// Throws <see cref="FormatException"/> when the input does not
    /// match the canonical format.
    /// </summary>
    public static KeyFingerprint Parse(string value)
    {
        if (!IsValid(value))
        {
            throw new FormatException(
                $"KeyFingerprint must be 95-char hex-with-colons (e.g., 'AB:CD:..:EF'); got '{value ?? "<null>"}'.");
        }
        return new KeyFingerprint(value!);
    }

    /// <summary>
    /// Returns true when <paramref name="value"/> matches the canonical
    /// 95-character hex-with-colons format. Per ADR 0066 §5: positions
    /// 2, 5, 8, ..., 92 (zero-indexed) are colons; all other positions
    /// are hex digits (0-9 / a-f / A-F).
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != CanonicalLength) return false;
        for (int i = 0; i < value.Length; i++)
        {
            // Positions where (i+1) is divisible by 3 — i.e., indices
            // 2, 5, 8, ... 92 — are separator positions.
            if ((i + 1) % 3 == 0)
            {
                if (value[i] != ':') return false;
            }
            else
            {
                if (!Uri.IsHexDigit(value[i])) return false;
            }
        }
        return true;
    }
}

/// <summary>
/// JSON converter for <see cref="KeyFingerprint"/>. Wire form is the
/// canonical string round-trip via <see cref="KeyFingerprint.Parse"/>.
/// </summary>
public sealed class KeyFingerprintJsonConverter : JsonConverter<KeyFingerprint>
{
    /// <inheritdoc />
    public override KeyFingerprint Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null)
        {
            throw new JsonException("KeyFingerprint cannot be null.");
        }
        return KeyFingerprint.Parse(value);
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        KeyFingerprint value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
