using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Anchor's webhook-URL registration per ADR 0031-A1.4. Sent to Bridge
/// via <c>POST /api/v1/tenant/webhook</c> with a tenant-scoped Bearer
/// token. HTTPS-only; non-loopback callback URL required.
/// </summary>
public sealed record WebhookRegistration
{
    /// <summary>HTTPS URL Bridge POSTs events to. MUST resolve to a non-loopback address.</summary>
    [JsonPropertyName("callbackUrl")]
    public required Uri CallbackUrl { get; init; }

    /// <summary>Per A1.3 — webhook (default) or SSE (long-lived).</summary>
    [JsonPropertyName("deliveryMode")]
    [JsonConverter(typeof(JsonStringEnumConverter<DeliveryMode>))]
    public required DeliveryMode DeliveryMode { get; init; }

    /// <summary>Opt-in event-type filter per A1.4. Default (when null/empty): all 7.</summary>
    [JsonPropertyName("subscribedEvents")]
    public IReadOnlyList<BridgeSubscriptionEventType>? SubscribedEvents { get; init; }

    /// <summary>Per-Anchor shared secret for HMAC signing per A1.4 + A1.12.1. Bridge generates; Anchor stores per ADR 0046 <c>IFieldEncryptor</c>.</summary>
    [JsonPropertyName("sharedSecret")]
    public required string SharedSecret { get; init; }
}

/// <summary>
/// The opt-in event-type filter from a <see cref="WebhookRegistration"/>,
/// projected to a queryable form. Bridge-side delivery uses this to
/// skip un-subscribed events without holding the registration record
/// in memory.
/// </summary>
public sealed record SubscribedEventFilter
{
    /// <summary>The events the Anchor wants. When null OR empty, all 7 are delivered (per A1.4 default).</summary>
    [JsonPropertyName("events")]
    public IReadOnlyList<BridgeSubscriptionEventType>? Events { get; init; }

    /// <summary>True iff <paramref name="eventType"/> passes this filter.</summary>
    public bool Includes(BridgeSubscriptionEventType eventType)
    {
        if (Events is null || Events.Count == 0) return true;
        for (var i = 0; i < Events.Count; i++)
        {
            if (Events[i] == eventType) return true;
        }
        return false;
    }
}
