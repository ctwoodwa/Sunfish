using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Reference <see cref="IWebhookRegistrationService"/> per ADR
/// 0031-A1.4. Validates HTTPS-only + non-loopback URLs; auto-generates
/// the per-Anchor shared secret when not supplied.
/// </summary>
public sealed class DefaultWebhookRegistrationService : IWebhookRegistrationService
{
    private const int SharedSecretByteLength = 32; // 256 bits

    /// <inheritdoc />
    public ValueTask<WebhookRegistration> RegisterAsync(string tenantId, WebhookRegistration registration, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(registration);
        ct.ThrowIfCancellationRequested();

        ValidateCallbackUrl(registration.CallbackUrl);

        var secret = string.IsNullOrEmpty(registration.SharedSecret)
            ? GenerateSharedSecret()
            : registration.SharedSecret;

        return ValueTask.FromResult(registration with { SharedSecret = secret });
    }

    /// <summary>HTTPS-only + non-loopback per A1.4.</summary>
    public static void ValidateCallbackUrl(Uri callbackUrl)
    {
        ArgumentNullException.ThrowIfNull(callbackUrl);
        if (!string.Equals(callbackUrl.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new WebhookRegistrationException(
                $"Callback URL must use HTTPS per ADR 0031-A1.4. Got '{callbackUrl.Scheme}'.");
        }
        if (IsLoopbackOrLinkLocal(callbackUrl))
        {
            throw new WebhookRegistrationException(
                $"Callback URL must resolve to a non-loopback address per ADR 0031-A1.4. Got '{callbackUrl.Host}'.");
        }
    }

    private static bool IsLoopbackOrLinkLocal(Uri callbackUrl)
    {
        if (callbackUrl.IsLoopback) return true;
        var host = callbackUrl.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (IPAddress.TryParse(host, out var ip))
        {
            return IPAddress.IsLoopback(ip);
        }
        return false;
    }

    private static string GenerateSharedSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SharedSecretByteLength);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
