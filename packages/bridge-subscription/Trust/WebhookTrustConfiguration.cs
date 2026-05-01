namespace Sunfish.Bridge.Subscription;

/// <summary>Cert-trust posture per ADR 0031-A1.12.3. <see cref="PubliclyRootedCa"/> is the default.</summary>
public enum WebhookTrustMode
{
    /// <summary>Verify the Anchor's TLS cert against the system trust store. Default.</summary>
    PubliclyRootedCa,

    /// <summary>Verify the Anchor's TLS cert byte-equals the registered PEM (per-Anchor pinning).</summary>
    PinnedCertificate,

    /// <summary>Bypass chain validation — admin opt-in only; emits <see cref="Sunfish.Kernel.Audit.AuditEventType.BridgeWebhookSelfSignedCertsConfigured"/> at configuration time.</summary>
    AllowSelfSigned,
}

/// <summary>Per-Anchor cert-trust configuration produced by <see cref="ITrustChainResolver"/>.</summary>
public sealed record WebhookTrustConfiguration
{
    /// <summary>Required.</summary>
    public required WebhookTrustMode Mode { get; init; }

    /// <summary>The PEM-encoded cert when <see cref="Mode"/> is <see cref="WebhookTrustMode.PinnedCertificate"/>; null otherwise.</summary>
    public string? PinnedCertPem { get; init; }
}
