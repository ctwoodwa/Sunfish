using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// HMAC-SHA256 implementation of <see cref="IEventSigner"/> per ADR
/// 0031-A1.2. Signature format: <c>"hmac-sha256:&lt;base64url-encoded
/// HMAC&gt;"</c> per the wire example in A1.2.
/// </summary>
public sealed class HmacSha256EventSigner : IEventSigner
{
    /// <summary>Wire-format prefix per the A1.2 example (e.g., <c>"hmac-sha256:..."</c>).</summary>
    public const string SignaturePrefix = "hmac-sha256:";

    /// <inheritdoc />
    public SignatureAlgorithm Algorithm => SignatureAlgorithm.HmacSha256;

    /// <inheritdoc />
    public ValueTask<BridgeSubscriptionEvent> SignAsync(BridgeSubscriptionEvent unsigned, string sharedSecret, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unsigned);
        ArgumentException.ThrowIfNullOrEmpty(sharedSecret);
        ct.ThrowIfCancellationRequested();

        var canonical = unsigned with
        {
            Algorithm = SignatureAlgorithm.HmacSha256,
            Signature = string.Empty,
        };
        var bytes = CanonicalJson.Serialize(canonical);
        var mac = ComputeMac(bytes, sharedSecret);
        var signed = canonical with { Signature = SignaturePrefix + mac };
        return ValueTask.FromResult(signed);
    }

    /// <inheritdoc />
    public ValueTask<bool> VerifyAsync(BridgeSubscriptionEvent signed, string sharedSecret, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(signed);
        ArgumentException.ThrowIfNullOrEmpty(sharedSecret);
        ct.ThrowIfCancellationRequested();

        if (signed.Algorithm != SignatureAlgorithm.HmacSha256) return ValueTask.FromResult(false);
        if (signed.Signature is null) return ValueTask.FromResult(false);
        if (!signed.Signature.StartsWith(SignaturePrefix, StringComparison.Ordinal)) return ValueTask.FromResult(false);

        var supplied = signed.Signature[SignaturePrefix.Length..];
        var canonical = signed with { Signature = string.Empty };
        var bytes = CanonicalJson.Serialize(canonical);
        var expected = ComputeMac(bytes, sharedSecret);

        // Constant-time comparison.
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(expected));
        return ValueTask.FromResult(ok);
    }

    private static string ComputeMac(byte[] bytes, string sharedSecret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(sharedSecret);
        var hash = HMACSHA256.HashData(keyBytes, bytes);
        return Base64Url.Encode(hash);
    }

    private static class Base64Url
    {
        public static string Encode(byte[] bytes) =>
            Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
    }
}
