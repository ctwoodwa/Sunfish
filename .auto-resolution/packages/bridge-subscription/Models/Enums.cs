namespace Sunfish.Bridge.Subscription;

/// <summary>
/// The 7 canonical subscription-event types per ADR 0031-A1.1.
/// Names round-trip as PascalCase string literals on the wire when paired
/// with <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>
/// (no naming-policy override) — matching the "SubscriptionTierUpgraded"
/// shape in the A1.2 example.
/// </summary>
public enum BridgeSubscriptionEventType
{
    /// <summary>Tenant first activates a subscribed Edition. <c>editionAfter</c> set; <c>editionBefore</c> null.</summary>
    SubscriptionStarted,

    /// <summary>Existing subscription auto-renews. <c>editionBefore == editionAfter</c>.</summary>
    SubscriptionRenewed,

    /// <summary>Tenant cancels (effective at next billing-cycle boundary). <c>editionBefore</c> set; <c>editionAfter</c> null.</summary>
    SubscriptionCancelled,

    /// <summary>Tier upgrade. Both editions set; reflects sub-second per A1's purpose.</summary>
    SubscriptionTierUpgraded,

    /// <summary>Tier downgrade. May sequester features per ADR 0028-A8.3.</summary>
    SubscriptionTierDowngraded,

    /// <summary>Payment failed; tenant is in grace period.</summary>
    SubscriptionDunning,

    /// <summary>Grace period exhausted; subscription lapsed (differs from <see cref="SubscriptionCancelled"/>: payment- vs operator-driven).</summary>
    SubscriptionExpired,
}

/// <summary>
/// Per A1.3 — webhook (HTTPS POST) is primary; SSE (long-lived
/// connection) is for Anchor instances that elect long-lived delivery.
/// Per A1.12.4, SSE reconnect is unbounded; webhook retry is bounded.
/// </summary>
public enum DeliveryMode
{
    /// <summary>HTTPS POST per A1.3; 30-second timeout; 7-attempt retry policy per A1.5.</summary>
    Webhook,

    /// <summary>Server-Sent Events long-lived connection per A1.3 + A1.12.4; unbounded reconnect.</summary>
    Sse,
}

/// <summary>
/// Per A1.12.2 — HMAC-SHA256 is the v0 default; Ed25519 reserved for
/// Phase 2+ migration. Phase 1 substrate ships HMAC only.
/// </summary>
public enum SignatureAlgorithm
{
    /// <summary>HMAC-SHA256 over canonical-JSON bytes (excluding the signature field). Default per A1.2.</summary>
    HmacSha256,

    /// <summary>Reserved for Phase 2+ migration per A1.12.2. Phase 1 substrate does NOT implement.</summary>
    Ed25519,
}
