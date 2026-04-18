using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;

namespace Sunfish.Foundation.Assets.Postgres.Internal;

/// <summary>
/// Shared hashing / id-derivation helpers mirroring <c>InMemoryEntityStore</c>.
/// </summary>
/// <remarks>
/// Duplicated (rather than re-exported) so the cryptographic surface of the in-memory
/// backend stays internal; both backends compute identical hashes so they remain
/// interchangeable at the contract level (plan D-VERSION-STORE-SHAPE).
/// </remarks>
internal static class PostgresHashing
{
    private const string Base32Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    /// <summary>Deterministic local-part derivation from schema + mint options.</summary>
    public static EntityId DeriveEntityId(SchemaId schema, CreateOptions options)
    {
        if (options.ExplicitLocalPart is { Length: > 0 } explicitLocal)
            return new EntityId(options.Scheme, options.Authority, explicitLocal);

        var input = Encoding.UTF8.GetBytes($"{schema.Value}|{options.Authority}|{options.Nonce}|{options.Issuer.Value}");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        var local = Base32Lower(digest[..16]);
        return new EntityId(options.Scheme, options.Authority, local);
    }

    /// <summary>
    /// Version hash: <c>SHA-256(parentHash ?? "" || '|' || canonicalBody || '|' || validFrom.ToString("O"))</c>.
    /// </summary>
    public static string HashVersion(string? parentHash, string canonicalBody, DateTimeOffset validFrom)
    {
        var prefix = parentHash ?? string.Empty;
        var input = Encoding.UTF8.GetBytes($"{prefix}|{canonicalBody}|{validFrom:O}");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>Canonicalize a <see cref="JsonDocument"/> using the shared algorithm.</summary>
    public static string CanonicalizeBody(JsonDocument body)
        => JsonCanonicalizer.ToCanonicalString(body);

    /// <summary>RFC 4648 base32 lowercase, no padding.</summary>
    private static string Base32Lower(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return string.Empty;

        var outputLength = (bytes.Length * 8 + 4) / 5;
        var output = new char[outputLength];
        int buffer = 0, bitsLeft = 0, outputIndex = 0;

        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output[outputIndex++] = Base32Alphabet[(buffer >> (bitsLeft - 5)) & 0x1F];
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
            output[outputIndex] = Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F];

        return new string(output);
    }
}
