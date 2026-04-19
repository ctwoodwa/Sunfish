using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Blobs;

/// <summary>
/// Content Identifier — a self-describing cryptographic hash that uniquely identifies a blob
/// by its content. Two blobs with the same bytes produce the same CID; different blobs produce
/// different CIDs with collision-resistance guaranteed by SHA-256.
/// </summary>
/// <remarks>
/// Format: CID v1 with raw codec (0x55), SHA-256 multihash (0x12 0x20 + digest), base32-lowercase
/// multibase prefix ('b'). Example: <c>bafkreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku</c>.
///
/// This is a minimal implementation of the IPFS CID spec — enough for Sunfish to address blobs
/// with forward-compatible identifiers. Cross-verification against an external IPFS reference
/// implementation is a future integration task (see <c>docs/specifications/research-notes/ipfs-evaluation.md</c>);
/// until that's done, treat these CIDs as Sunfish-internal identifiers that follow the CID v1
/// shape and will later be made byte-for-byte interoperable with IPFS.
/// </remarks>
[JsonConverter(typeof(CidJsonConverter))]
public readonly record struct Cid(string Value)
{
    /// <summary>The multibase prefix for base32-lowercase ('b' per the multibase spec).</summary>
    public const char MultibasePrefix = 'b';

    /// <summary>The multicodec byte for "raw" binary content (0x55).</summary>
    public const byte RawCodec = 0x55;

    /// <summary>The CID version marker (0x01 = v1).</summary>
    public const byte Version1 = 0x01;

    /// <summary>The multihash code for SHA-256 (0x12).</summary>
    public const byte Sha256MultihashCode = 0x12;

    /// <summary>SHA-256 digest length in bytes (32).</summary>
    public const byte Sha256DigestLength = 32;

    /// <summary>Computes the CID of a byte sequence using the canonical Sunfish format.</summary>
    public static Cid FromBytes(ReadOnlySpan<byte> content)
    {
        Span<byte> digest = stackalloc byte[Sha256DigestLength];
        SHA256.HashData(content, digest);

        return FromDigest(digest);
    }

    /// <summary>
    /// Computes the CID of a stream using a single-pass incremental SHA-256 hash.
    /// The stream is read from its current position to the end; the caller is responsible
    /// for seeking if a specific range is intended.
    /// </summary>
    public static async ValueTask<Cid> FromStreamAsync(Stream stream, CancellationToken ct = default)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[81920]; // 80 KiB — matches FileStream default copy buffer
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            hasher.AppendData(buffer, 0, read);
        }

        Span<byte> digest = stackalloc byte[Sha256DigestLength];
        hasher.GetHashAndReset(digest);
        return FromDigest(digest);
    }

    /// <summary>
    /// Encodes a pre-computed SHA-256 digest into the canonical CID v1 / raw / SHA-256 format.
    /// </summary>
    /// <param name="digest">A 32-byte SHA-256 digest.</param>
    internal static Cid FromDigest(ReadOnlySpan<byte> digest)
    {
        // CID v1 = [version] [codec] [multihash-code] [digest-length] [digest]
        Span<byte> cidBytes = stackalloc byte[4 + Sha256DigestLength];
        cidBytes[0] = Version1;
        cidBytes[1] = RawCodec;
        cidBytes[2] = Sha256MultihashCode;
        cidBytes[3] = Sha256DigestLength;
        digest.CopyTo(cidBytes[4..]);

        return new Cid(MultibasePrefix + Base32.Encode(cidBytes));
    }

    /// <summary>
    /// Parses a CID string (as emitted by <see cref="FromBytes"/> or an external IPFS implementation)
    /// and returns a <see cref="Cid"/> value. Performs shape validation only — it does not re-derive
    /// the digest from any bytes. Intended for round-tripping CID strings (e.g. across HTTP calls
    /// to a Kubo daemon) back into Sunfish's type system.
    /// </summary>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="value"/> is null, empty, missing the base32-lowercase multibase
    /// prefix (<see cref="MultibasePrefix"/>), has an unexpected length for a CID v1 / raw /
    /// SHA-256 encoding, or contains characters outside the base32-lowercase alphabet.
    /// </exception>
    /// <remarks>
    /// A CID v1 / raw / SHA-256 binary payload is 36 bytes, which base32-encodes to 58 characters;
    /// together with the <c>'b'</c> multibase prefix the canonical string length is 59. This method
    /// validates that exact shape — it does not attempt to decode alternative multibase encodings
    /// or different multihash algorithms.
    /// </remarks>
    public static Cid Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("CID value is null or empty.");
        }

        // 1 (multibase) + ceil(36 * 8 / 5) = 1 + 58 = 59 characters for CID v1 / raw / SHA-256.
        const int expectedLength = 59;
        if (value.Length != expectedLength)
        {
            throw new FormatException(
                $"CID '{value}' has length {value.Length}; expected {expectedLength} characters " +
                "(base32-lowercase, CID v1, raw codec, SHA-256).");
        }

        if (value[0] != MultibasePrefix)
        {
            throw new FormatException(
                $"CID '{value}' is missing the base32-lowercase multibase prefix '{MultibasePrefix}'.");
        }

        for (int i = 1; i < value.Length; i++)
        {
            var c = value[i];
            var isLower = c >= 'a' && c <= 'z';
            var isDigit = c >= '2' && c <= '7';
            if (!isLower && !isDigit)
            {
                throw new FormatException(
                    $"CID '{value}' contains character '{c}' at index {i} outside the base32-lowercase alphabet.");
            }
        }

        return new Cid(value);
    }

    /// <summary>Returns the string form — the canonical wire representation.</summary>
    public override string ToString() => Value;

    /// <summary>Implicit conversion to string for ergonomic use in paths and logs.</summary>
    public static implicit operator string(Cid cid) => cid.Value;
}

/// <summary>RFC 4648 base32 lowercase, no padding. Used as the CID multibase encoding.</summary>
internal static class Base32
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return string.Empty;

        var outputLength = (bytes.Length * 8 + 4) / 5;
        var output = new char[outputLength];

        int buffer = 0;
        int bitsLeft = 0;
        int outputIndex = 0;

        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output[outputIndex++] = Alphabet[(buffer >> (bitsLeft - 5)) & 0x1F];
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            output[outputIndex] = Alphabet[(buffer << (5 - bitsLeft)) & 0x1F];
        }

        return new string(output);
    }
}

internal sealed class CidJsonConverter : JsonConverter<Cid>
{
    public override Cid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("Cid must be a non-null string.");
        try { return Cid.Parse(str); }
        catch (FormatException ex) { throw new JsonException(ex.Message, ex); }
    }

    public override void Write(Utf8JsonWriter writer, Cid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
