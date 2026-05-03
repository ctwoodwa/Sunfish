using System;
using System.Collections.Concurrent;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Reference <see cref="ITrustChainResolver"/> per ADR 0031-A1.12.3.
/// Defaults every tenant to <see cref="WebhookTrustMode.PubliclyRootedCa"/>;
/// per-tenant overrides are recorded via
/// <see cref="ConfigurePinnedCertificate"/> + <see cref="AllowSelfSigned"/>.
/// </summary>
/// <remarks>
/// The default is intentionally NOT <see cref="WebhookTrustMode.AllowSelfSigned"/>
/// per A1.12.3 + the W#36 hand-off halt-condition #4. Self-signed mode
/// is admin opt-in only with audit emission at configuration time.
/// </remarks>
public sealed class DefaultTrustChainResolver : ITrustChainResolver
{
    private readonly ConcurrentDictionary<string, WebhookTrustConfiguration> _overrides = new();

    /// <inheritdoc />
    public WebhookTrustConfiguration ResolveFor(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        return _overrides.TryGetValue(tenantId, out var cfg)
            ? cfg
            : new WebhookTrustConfiguration { Mode = WebhookTrustMode.PubliclyRootedCa };
    }

    /// <summary>Pin <paramref name="tenantId"/>'s webhook cert to the supplied PEM.</summary>
    public void ConfigurePinnedCertificate(string tenantId, string pemCert)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentException.ThrowIfNullOrEmpty(pemCert);
        _overrides[tenantId] = new WebhookTrustConfiguration
        {
            Mode = WebhookTrustMode.PinnedCertificate,
            PinnedCertPem = pemCert,
        };
    }

    /// <summary>
    /// Configure self-signed-cert allowance for <paramref name="tenantId"/>.
    /// Caller MUST emit the <see cref="Sunfish.Kernel.Audit.AuditEventType.BridgeWebhookSelfSignedCertsConfigured"/>
    /// audit event per A1.12.3 — this method only records the
    /// configuration; the audit-emission boundary lives in the host's
    /// admin handler.
    /// </summary>
    public void AllowSelfSigned(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        _overrides[tenantId] = new WebhookTrustConfiguration { Mode = WebhookTrustMode.AllowSelfSigned };
    }

    /// <summary>Reset <paramref name="tenantId"/> to the default (publicly-rooted CA).</summary>
    public void ResetToDefault(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        _overrides.TryRemove(tenantId, out _);
    }
}
