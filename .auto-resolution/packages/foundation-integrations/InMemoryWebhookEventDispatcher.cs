namespace Sunfish.Foundation.Integrations;

/// <summary>Default in-memory <see cref="IWebhookEventDispatcher"/>.</summary>
public sealed class InMemoryWebhookEventDispatcher : IWebhookEventDispatcher
{
    private readonly List<IWebhookEventHandler> _handlers = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void Register(IWebhookEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock)
        {
            _handlers.Add(handler);
        }
    }

    /// <inheritdoc />
    public async ValueTask DispatchAsync(WebhookEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        IWebhookEventHandler[] snapshot;
        lock (_lock)
        {
            snapshot = _handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            if (string.Equals(handler.ProviderKey, envelope.ProviderKey, StringComparison.Ordinal)
                && string.Equals(handler.EventType, envelope.EventType, StringComparison.Ordinal))
            {
                await handler.HandleAsync(envelope, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
