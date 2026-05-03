namespace Sunfish.Foundation.Integrations;

/// <summary>
/// Handler for webhook envelopes of a given provider and event type.
/// Registered with <see cref="IWebhookEventDispatcher"/> at startup.
/// </summary>
public interface IWebhookEventHandler
{
    /// <summary>Provider key this handler cares about.</summary>
    string ProviderKey { get; }

    /// <summary>Event type this handler cares about.</summary>
    string EventType { get; }

    /// <summary>Processes a webhook envelope.</summary>
    ValueTask HandleAsync(WebhookEventEnvelope envelope, CancellationToken cancellationToken = default);
}
