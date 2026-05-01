using System.Net.Http;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Produces the <see cref="HttpClient"/> Bridge uses to deliver
/// webhooks to a specific Anchor, with the right cert-pinning + self-
/// signed-allowance posture per ADR 0031-A1.12.3.
/// </summary>
/// <remarks>
/// Three modes per A1.12.3:
/// <list type="bullet">
///   <item><b>Default — publicly-rooted CA verification.</b> Standard <see cref="HttpClient"/> with the system's certificate trust store.</item>
///   <item><b>Per-Anchor cert pinning.</b> Anchor uploads a PEM at registration; Bridge verifies presented cert byte-equals the pinned one.</item>
///   <item><b>Self-signed allowance.</b> Per-deployment opt-in with audit emission on configuration. Bypasses chain validation.</item>
/// </list>
/// </remarks>
public interface ITrustChainResolver
{
    /// <summary>Returns the configuration for <paramref name="tenantId"/>'s webhook delivery.</summary>
    WebhookTrustConfiguration ResolveFor(string tenantId);
}
