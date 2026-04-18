using System.Security.Cryptography;

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

        // CID v1 = [version] [codec] [multihash-code] [digest-length] [digest]
        Span<byte> cidBytes = stackalloc byte[4 + Sha256DigestLength];
        cidBytes[0] = Version1;
        cidBytes[1] = RawCodec;
        cidBytes[2] = Sha256MultihashCode;
        cidBytes[3] = Sha256DigestLength;
        digest.CopyTo(cidBytes[4..]);

        return new Cid(MultibasePrefix + Base32.Encode(cidBytes));
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
