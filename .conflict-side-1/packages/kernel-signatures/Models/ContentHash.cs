using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Sunfish.Kernel.Signatures.Canonicalization;

namespace Sunfish.Kernel.Signatures.Models;

/// <summary>
/// SHA-256 content hash binding a <see cref="SignatureEvent"/> to the
/// canonical bytes of the document being signed (per ADR 0054 amendment
/// A1). The canonicalization rule (RFC 8785 for JSON, NFC UTF-8 for
/// plain text, PDF/A for PDFs) lives in W#21 Phase 2; Phase 1 ships
/// the value type + simple byte-array constructor.
/// </summary>
/// <param name="Bytes">SHA-256 digest as 32 raw bytes.</param>
public readonly record struct ContentHash(byte[] Bytes)
{
    /// <summary>SHA-256 digest size in bytes.</summary>
    public const int DigestLengthBytes = 32;

    /// <summary>Computes a SHA-256 hash over <paramref name="canonicalBytes"/>. Caller is responsible for canonicalization.</summary>
    public static ContentHash ComputeFromCanonicalBytes(ReadOnlySpan<byte> canonicalBytes)
    {
        Span<byte> digest = stackalloc byte[DigestLengthBytes];
        SHA256.HashData(canonicalBytes, digest);
        return new ContentHash(digest.ToArray());
    }

    /// <summary>Computes a SHA-256 hash over the UTF-8 NFC-normalized bytes of <paramref name="text"/>.</summary>
    public static ContentHash ComputeFromUtf8Nfc(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var nfc = text.Normalize(NormalizationForm.FormC);
        var bytes = Encoding.UTF8.GetBytes(nfc);
        return ComputeFromCanonicalBytes(bytes);
    }

    private static readonly JsonCanonicalCanonicalizer JsonCanonicalizer = new();

    /// <summary>
    /// Computes a SHA-256 hash over the JSON-canonical bytes of
    /// <paramref name="json"/>. Uses the same canonicalization scheme
    /// as <c>Foundation.Crypto.CanonicalJson</c> (alphabetically-sorted
    /// keys + no whitespace + UTF-8); ADR 0054 amendment A1 pins this
    /// rule for structured JSON documents.
    /// </summary>
    public static ContentHash ComputeFromJson(JsonNode json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return ComputeFromCanonicalBytes(JsonCanonicalizer.Canonicalize(json));
    }

    /// <summary>
    /// Computes a SHA-256 hash over the JSON-canonical bytes of an
    /// arbitrary CLR object (typically an <see cref="IReadOnlyDictionary{TKey, TValue}"/>
    /// or a record). Same canonicalization rule as <see cref="ComputeFromJson"/>.
    /// </summary>
    public static ContentHash ComputeFromJsonObject<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ComputeFromCanonicalBytes(JsonCanonicalizer.Canonicalize(value));
    }

    /// <summary>Hexadecimal lower-case rendering of the digest.</summary>
    public string ToHex() => Bytes is null ? string.Empty : Convert.ToHexStringLower(Bytes);

    /// <inheritdoc />
    public override string ToString() => $"sha256:{ToHex()}";

    /// <summary>Constant-time equality comparison; safe to use on untrusted input.</summary>
    public bool ConstantTimeEquals(ContentHash other) =>
        Bytes is not null
            && other.Bytes is not null
            && Bytes.Length == other.Bytes.Length
            && CryptographicOperations.FixedTimeEquals(Bytes, other.Bytes);
}
