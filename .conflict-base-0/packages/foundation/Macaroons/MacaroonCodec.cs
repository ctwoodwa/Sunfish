using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Sunfish.Foundation.Macaroons;

/// <summary>
/// Wire-format codec and HMAC-SHA256 chain computation for macaroons.
/// </summary>
/// <remarks>
/// <para>The chain signature is computed as:
/// <c>sig_0 = HMAC-SHA256(rootKey, identifier)</c>,
/// <c>sig_{i+1} = HMAC-SHA256(sig_i, caveat_i.Predicate)</c>. The final <c>sig_n</c> is the
/// macaroon's signature. This construction gives the chain its two load-bearing properties:
/// (1) the root key is required to mint, but (2) any holder can attenuate by continuing
/// the chain from the current signature.</para>
/// <para>The wire format is:
/// <c>location 0x1E identifier 0x1E caveat_0 0x1E ... caveat_{n-1} 0x1F signature-bytes</c>,
/// base64url-encoded. The <c>0x1F</c> (UNIT SEPARATOR) is a single sentinel that separates
/// the text portion from the binary signature; there is exactly one per macaroon.</para>
/// </remarks>
public static class MacaroonCodec
{
    /// <summary>ASCII RECORD SEPARATOR, separates location / identifier / caveat elements.</summary>
    public const byte RecordSeparator = 0x1E;

    /// <summary>ASCII UNIT SEPARATOR, the single delimiter between text portion and signature bytes.</summary>
    public const byte UnitSeparator = 0x1F;

    /// <summary>
    /// Computes the HMAC-SHA256 chain signature for the given root key, identifier, and caveat list.
    /// Returns a fresh 32-byte array.
    /// </summary>
    public static byte[] ComputeChain(
        ReadOnlySpan<byte> rootKey,
        string identifier,
        IReadOnlyList<Caveat> caveats)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(caveats);

        var sig = HMACSHA256.HashData(rootKey, Encoding.UTF8.GetBytes(identifier));
        for (var i = 0; i < caveats.Count; i++)
        {
            sig = HMACSHA256.HashData(sig, Encoding.UTF8.GetBytes(caveats[i].Predicate));
        }
        return sig;
    }

    /// <summary>
    /// Encodes a macaroon to its base64url wire form. The signature must be non-null; location,
    /// identifier, and every caveat predicate are encoded as UTF-8.
    /// </summary>
    public static string EncodeBase64Url(Macaroon m)
    {
        ArgumentNullException.ThrowIfNull(m);

        var buf = new List<byte>(capacity: 256);
        buf.AddRange(Encoding.UTF8.GetBytes(m.Location));
        buf.Add(RecordSeparator);
        buf.AddRange(Encoding.UTF8.GetBytes(m.Identifier));
        for (var i = 0; i < m.Caveats.Count; i++)
        {
            buf.Add(RecordSeparator);
            buf.AddRange(Encoding.UTF8.GetBytes(m.Caveats[i].Predicate));
        }
        buf.Add(UnitSeparator);
        buf.AddRange(m.Signature);

        var bytes = buf.ToArray();
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>
    /// Decodes a base64url-encoded macaroon back into its components.
    /// </summary>
    /// <exception cref="FormatException">The input is not valid base64url, is missing the
    /// UNIT SEPARATOR sentinel, has a signature of wrong length, or has fewer than two
    /// text-portion records (location + identifier).</exception>
    public static Macaroon DecodeBase64Url(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(encoded.AsSpan());
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new FormatException("Macaroon wire form is not valid base64url.", ex);
        }

        // Locate the single UNIT SEPARATOR (0x1F) that splits text portion from signature.
        var unitIdx = Array.IndexOf(bytes, UnitSeparator);
        if (unitIdx < 0)
            throw new FormatException("Macaroon wire form is missing the UNIT SEPARATOR sentinel.");

        var textPortion = new byte[unitIdx];
        Buffer.BlockCopy(bytes, 0, textPortion, 0, unitIdx);

        var sigLen = bytes.Length - unitIdx - 1;
        if (sigLen != 32)
            throw new FormatException($"Macaroon signature must be 32 bytes (got {sigLen}).");

        var sig = new byte[32];
        Buffer.BlockCopy(bytes, unitIdx + 1, sig, 0, 32);

        var parts = SplitOn(textPortion, RecordSeparator);
        if (parts.Count < 2)
            throw new FormatException("Macaroon wire form requires at least a location and identifier.");

        var location = Encoding.UTF8.GetString(parts[0]);
        var identifier = Encoding.UTF8.GetString(parts[1]);
        var caveats = new List<Caveat>(parts.Count - 2);
        for (var i = 2; i < parts.Count; i++)
        {
            caveats.Add(new Caveat(Encoding.UTF8.GetString(parts[i])));
        }

        return new Macaroon(location, identifier, caveats, sig);
    }

    private static List<byte[]> SplitOn(byte[] source, byte delimiter)
    {
        var result = new List<byte[]>();
        var start = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == delimiter)
            {
                var chunk = new byte[i - start];
                Buffer.BlockCopy(source, start, chunk, 0, chunk.Length);
                result.Add(chunk);
                start = i + 1;
            }
        }
        var tail = new byte[source.Length - start];
        Buffer.BlockCopy(source, start, tail, 0, tail.Length);
        result.Add(tail);
        return result;
    }
}
