namespace Sunfish.Foundation.Integrations;

/// <summary>
/// Routes <see cref="WebhookEventEnvelope"/> instances to registered
/// <see cref="IWebhookEventHandler"/>s. Multiple handlers may fire for a
/// single envelope; ordering is registration order.
/// </summary>
public interface IWebhookEventDispatcher
{
    /// <summary>Registers a handler.</summary>
    void Register(IWebhookEventHandler handler);

    /// <summary>Dispatches an envelope to every matching registered handler.</summary>
    ValueTask DispatchAsync(WebhookEventEnvelope envelope, CancellationToken cancellationToken = default);
}
