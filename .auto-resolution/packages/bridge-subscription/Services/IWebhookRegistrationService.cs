using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Bridge-side webhook URL registration service per ADR 0031-A1.4.
/// Validates HTTPS-only + non-loopback callback URLs; generates the
/// per-Anchor shared secret used for HMAC signing.
/// </summary>
public interface IWebhookRegistrationService
{
    /// <summary>
    /// Registers <paramref name="registration"/> for
    /// <paramref name="tenantId"/>. Returns the registered shape; the
    /// returned <see cref="WebhookRegistration.SharedSecret"/> may be
    /// auto-generated server-side when the request omits it (Bridge
    /// holds it server-side per A1.12.1; Anchor stores the returned
    /// secret per ADR 0046).
    /// </summary>
    ValueTask<WebhookRegistration> RegisterAsync(string tenantId, WebhookRegistration registration, CancellationToken ct = default);
}

/// <summary>Thrown when a registration request fails the A1.4 gating rules.</summary>
public sealed class WebhookRegistrationException : System.Exception
{
    public WebhookRegistrationException(string message) : base(message) { }
}
