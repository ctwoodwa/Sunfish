using System.Collections.Generic;
using Sunfish.Kernel.Audit;

namespace Sunfish.Bridge.Subscription.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the W#36 Bridge → Anchor
/// subscription-event-emitter audit-event set (ADR 0031-A1.7 + A1.12).
/// Mirrors the <c>VersionVectorAuditPayloads</c> +
/// <c>MigrationAuditPayloads</c> + <c>TransportAuditPayloads</c> conventions:
/// keys alphabetized; bodies opaque to the substrate.
/// </summary>
public static class BridgeSubscriptionAuditPayloads
{
    /// <summary>
    /// Body for <see cref="AuditEventType.BridgeSubscriptionEventEmitted"/> +
    /// <see cref="AuditEventType.BridgeSubscriptionEventDelivered"/> +
    /// <see cref="AuditEventType.BridgeSubscriptionEventDeliveryFailed"/> +
    /// <see cref="AuditEventType.BridgeSubscriptionEventDeliveryFailedTerminal"/> +
    /// <see cref="AuditEventType.BridgeSubscriptionEventReceived"/>.
    /// </summary>
    public static AuditPayload Event(string tenantId, BridgeSubscriptionEventType eventType, string eventId, int deliveryAttempt) =>
        new(new Dictionary<string, object?>
        {
            ["delivery_attempt"] = deliveryAttempt,
            ["event_id"] = eventId,
            ["event_type"] = eventType.ToString(),
            ["tenant_id"] = tenantId,
        });

    /// <summary>Body for <see cref="AuditEventType.BridgeSubscriptionEventSignatureFailed"/>.</summary>
    public static AuditPayload SignatureFailed(string tenantId, string eventId, string sourceIp) =>
        new(new Dictionary<string, object?>
        {
            ["event_id"] = eventId,
            ["source_ip"] = sourceIp,
            ["tenant_id"] = tenantId,
        });

    /// <summary>Body for <see cref="AuditEventType.BridgeSubscriptionEventStale"/>.</summary>
    public static AuditPayload Stale(string tenantId, BridgeSubscriptionEventType eventType, string eventId, double clockSkewSeconds) =>
        new(new Dictionary<string, object?>
        {
            ["clock_skew_seconds"] = clockSkewSeconds,
            ["event_id"] = eventId,
            ["event_type"] = eventType.ToString(),
            ["tenant_id"] = tenantId,
        });

    /// <summary>Body for <see cref="AuditEventType.BridgeSubscriptionWebhookRegistered"/>.</summary>
    public static AuditPayload WebhookRegistered(string tenantId, string callbackUrl, DeliveryMode deliveryMode) =>
        new(new Dictionary<string, object?>
        {
            ["callback_url"] = callbackUrl,
            ["delivery_mode"] = deliveryMode.ToString(),
            ["tenant_id"] = tenantId,
        });

    /// <summary>Body for <see cref="AuditEventType.BridgeSubscriptionWebhookRotationStaged"/>.</summary>
    public static AuditPayload WebhookRotationStaged(string tenantId, string previousSecretFingerprint, string newSecretFingerprint, int graceWindowHours) =>
        new(new Dictionary<string, object?>
        {
            ["grace_window_hours"] = graceWindowHours,
            ["new_secret_fingerprint"] = newSecretFingerprint,
            ["previous_secret_fingerprint"] = previousSecretFingerprint,
            ["tenant_id"] = tenantId,
        });

    /// <summary>Body for <see cref="AuditEventType.BridgeWebhookSelfSignedCertsConfigured"/>.</summary>
    public static AuditPayload SelfSignedCertsConfigured(string tenantId, bool allowed) =>
        new(new Dictionary<string, object?>
        {
            ["allowed"] = allowed,
            ["tenant_id"] = tenantId,
        });
}
