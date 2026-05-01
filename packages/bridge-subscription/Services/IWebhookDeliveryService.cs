using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Bridge-side delivery contract per ADR 0031-A1.3 + A1.5. Posts an
/// already-signed <see cref="BridgeSubscriptionEvent"/> to the
/// Anchor-registered webhook URL with the 7-attempt retry policy +
/// dead-letter handling.
/// </summary>
public interface IWebhookDeliveryService
{
    /// <summary>
    /// Delivers <paramref name="evt"/> to <paramref name="callbackUrl"/>.
    /// Returns the terminal outcome (Delivered / DeadLettered).
    /// Honors <paramref name="ct"/>; on cancellation the outcome
    /// surfaces as <see cref="OperationCanceledException"/>.
    /// </summary>
    ValueTask<WebhookDeliveryOutcome> DeliverAsync(
        BridgeSubscriptionEvent evt,
        Uri callbackUrl,
        CancellationToken ct = default);
}

/// <summary>Terminal outcomes of <see cref="IWebhookDeliveryService.DeliverAsync"/>.</summary>
public enum WebhookDeliveryOutcome
{
    /// <summary>Delivery succeeded (HTTP 2xx) within the 7-attempt budget.</summary>
    Delivered,

    /// <summary>All 7 retries exhausted — event moved to the dead-letter queue.</summary>
    DeadLettered,
}
