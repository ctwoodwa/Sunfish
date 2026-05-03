using System;
using System.Text.Json.Serialization;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// One Bridge → Anchor subscription-event payload per ADR 0031-A1.2.
/// Canonical-JSON-encoded via
/// <see cref="Sunfish.Foundation.Crypto.CanonicalJson.Serialize"/>;
/// camelCase wire shape per ADR 0028-A7.8.
/// </summary>
/// <remarks>
/// The <see cref="Signature"/> field is excluded from the signing
/// surface (signing is over canonical-JSON bytes of the event MINUS the
/// signature). The <see cref="Algorithm"/> field defaults to
/// <see cref="SignatureAlgorithm.HmacSha256"/> per A1.12.2; Phase 1 ships
/// HMAC-SHA256 only.
/// </remarks>
public sealed record BridgeSubscriptionEvent
{
    /// <summary>The tenant the subscription event applies to (Bridge-side identifier).</summary>
    [JsonPropertyName("tenantId")]
    public required string TenantId { get; init; }

    /// <summary>One of the 7 canonical event types per A1.1.</summary>
    [JsonPropertyName("eventType")]
    [JsonConverter(typeof(JsonStringEnumConverter<BridgeSubscriptionEventType>))]
    public required BridgeSubscriptionEventType EventType { get; init; }

    /// <summary>The edition key (per ADR 0009 <c>IEditionResolver</c>) BEFORE the event. Null on <see cref="BridgeSubscriptionEventType.SubscriptionStarted"/>.</summary>
    [JsonPropertyName("editionBefore")]
    public string? EditionBefore { get; init; }

    /// <summary>The edition key AFTER the event. Null on <see cref="BridgeSubscriptionEventType.SubscriptionCancelled"/> + <see cref="BridgeSubscriptionEventType.SubscriptionExpired"/>.</summary>
    [JsonPropertyName("editionAfter")]
    public string? EditionAfter { get; init; }

    /// <summary>When the event takes effect, UTC. Anchor enforces ±5min clock-skew tolerance per A1.2.</summary>
    [JsonPropertyName("effectiveAt")]
    public required DateTimeOffset EffectiveAt { get; init; }

    /// <summary>Globally-unique UUID per delivery+event tuple. Anchor de-duplicates by this key (per-tenant LRU; 24h retention).</summary>
    [JsonPropertyName("eventId")]
    public required Guid EventId { get; init; }

    /// <summary>1-indexed delivery attempt count. Bumped on each Bridge-side retry per A1.5.</summary>
    [JsonPropertyName("deliveryAttempt")]
    public required int DeliveryAttempt { get; init; }

    /// <summary>The HMAC-SHA256 (or future Ed25519) signature, base64url-encoded with the algorithm prefix (e.g., <c>"hmac-sha256:..."</c>). Excluded from the signing surface.</summary>
    [JsonPropertyName("signature")]
    public required string Signature { get; init; }

    /// <summary>Reserved per A1.12.2 for Phase 2+ Ed25519 migration. Defaults to <see cref="SignatureAlgorithm.HmacSha256"/>.</summary>
    [JsonPropertyName("algorithm")]
    [JsonConverter(typeof(JsonStringEnumConverter<SignatureAlgorithm>))]
    public SignatureAlgorithm Algorithm { get; init; } = SignatureAlgorithm.HmacSha256;
}
