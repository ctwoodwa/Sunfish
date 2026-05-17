using System.Buffers.Binary;

namespace Sunfish.Foundation.Events;

/// <summary>
/// ULID generator for <see cref="DomainEventEnvelope{TPayload}.EventId"/>.
/// Produces a 26-character Crockford base-32 string derived from
/// 48-bit timestamp (ms since Unix epoch) + 80-bit cryptographically-
/// random suffix.
/// </summary>
/// <remarks>
/// <para>
/// Per <c>_shared/engineering/crdt-friendly-schema-conventions.md</c>
/// §1 + the ULID spec at <c>https://github.com/ulid/spec</c>: the
/// timestamp prefix makes ULIDs sortable by mint-time; the random
/// suffix makes them collision-free across replicas without
/// coordination.
/// </para>
/// <para>
/// Inline implementation (no external <c>Ulid</c> NuGet dependency)
/// — keeps the substrate footprint minimal. A future swap to the
/// upstream <c>Ulid</c> package is value-shape-only since callers
/// treat <see cref="DomainEventEnvelope{TPayload}.EventId"/> as an
/// opaque string.
/// </para>
/// </remarks>
public static class EventId
{
    /// <summary>Crockford base-32 alphabet (no I/L/O/U to avoid OCR confusion).</summary>
    private const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly Random Rng = Random.Shared;

    /// <summary>Mint a fresh ULID-format event id. Thread-safe.</summary>
    public static string New() => New(DateTimeOffset.UtcNow);

    /// <summary>
    /// Mint a ULID-format event id with the supplied timestamp.
    /// Test-friendly overload — production code uses
    /// <see cref="New()"/>.
    /// </summary>
    public static string New(DateTimeOffset timestamp)
    {
        var timestampMs = timestamp.ToUnixTimeMilliseconds();
        if (timestampMs < 0)
            throw new ArgumentOutOfRangeException(
                nameof(timestamp), "Timestamp must be on or after 1970-01-01 UTC.");

        Span<byte> bytes = stackalloc byte[16];
        // 48-bit big-endian timestamp into bytes[0..5].
        BinaryPrimitives.WriteUInt64BigEndian(bytes[0..8], (ulong)timestampMs << 16);
        // 80-bit (10-byte) cryptographically-random suffix into bytes[6..15].
        Span<byte> randomTail = bytes[6..16];
        Rng.NextBytes(randomTail);
        return EncodeCrockford32(bytes);
    }

    /// <summary>
    /// Encode 16 bytes (128 bits) into 26 Crockford base-32 chars.
    /// Per the ULID spec.
    /// </summary>
    private static string EncodeCrockford32(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 16)
            throw new ArgumentException("Must be 16 bytes for a 128-bit ULID.", nameof(bytes));

        Span<char> chars = stackalloc char[26];

        // Char 0 — top 3 bits of byte 0 only (ULID timestamp is 48 bits
        // = 9.6 base-32 chars rounded up to 10, but ULID only allows
        // values 0..7 in the high nibble of char 0, so we mask).
        chars[0] = Crockford[(bytes[0] & 0xE0) >> 5];
        chars[1] = Crockford[bytes[0] & 0x1F];
        chars[2] = Crockford[(bytes[1] & 0xF8) >> 3];
        chars[3] = Crockford[((bytes[1] & 0x07) << 2) | ((bytes[2] & 0xC0) >> 6)];
        chars[4] = Crockford[(bytes[2] & 0x3E) >> 1];
        chars[5] = Crockford[((bytes[2] & 0x01) << 4) | ((bytes[3] & 0xF0) >> 4)];
        chars[6] = Crockford[((bytes[3] & 0x0F) << 1) | ((bytes[4] & 0x80) >> 7)];
        chars[7] = Crockford[(bytes[4] & 0x7C) >> 2];
        chars[8] = Crockford[((bytes[4] & 0x03) << 3) | ((bytes[5] & 0xE0) >> 5)];
        chars[9] = Crockford[bytes[5] & 0x1F];

        // Chars 10..25 — the 80-bit random suffix encoded the same way.
        chars[10] = Crockford[(bytes[6] & 0xF8) >> 3];
        chars[11] = Crockford[((bytes[6] & 0x07) << 2) | ((bytes[7] & 0xC0) >> 6)];
        chars[12] = Crockford[(bytes[7] & 0x3E) >> 1];
        chars[13] = Crockford[((bytes[7] & 0x01) << 4) | ((bytes[8] & 0xF0) >> 4)];
        chars[14] = Crockford[((bytes[8] & 0x0F) << 1) | ((bytes[9] & 0x80) >> 7)];
        chars[15] = Crockford[(bytes[9] & 0x7C) >> 2];
        chars[16] = Crockford[((bytes[9] & 0x03) << 3) | ((bytes[10] & 0xE0) >> 5)];
        chars[17] = Crockford[bytes[10] & 0x1F];
        chars[18] = Crockford[(bytes[11] & 0xF8) >> 3];
        chars[19] = Crockford[((bytes[11] & 0x07) << 2) | ((bytes[12] & 0xC0) >> 6)];
        chars[20] = Crockford[(bytes[12] & 0x3E) >> 1];
        chars[21] = Crockford[((bytes[12] & 0x01) << 4) | ((bytes[13] & 0xF0) >> 4)];
        chars[22] = Crockford[((bytes[13] & 0x0F) << 1) | ((bytes[14] & 0x80) >> 7)];
        chars[23] = Crockford[(bytes[14] & 0x7C) >> 2];
        chars[24] = Crockford[((bytes[14] & 0x03) << 3) | ((bytes[15] & 0xE0) >> 5)];
        chars[25] = Crockford[bytes[15] & 0x1F];

        return new string(chars);
    }
}
