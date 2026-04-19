using Microsoft.AspNetCore.Components;
using Sunfish.UICore.Contracts;

namespace Sunfish.Components.Blazor.Async;

/// <summary>
/// Bridges <see cref="IClientTask{TMessage}"/> and
/// <see cref="IClientSubscription{TMessage}"/> to Blazor's
/// <see cref="EventCallback{TValue}"/> message pump.
/// </summary>
/// <remarks>
/// Hosts inject this and call <see cref="DispatchAsync{TMessage}(IClientTask{TMessage}, EventCallback{TMessage}, CancellationToken)"/>
/// from inside a component's update handler to execute an async task and route its
/// resulting message back into the component via <see cref="EventCallback"/>. The
/// subscription overload repeatedly dispatches until the stream completes or is
/// cancelled.
/// </remarks>
public sealed class InMemoryClientTaskDispatcher
{
    /// <summary>
    /// Executes an <see cref="IClientTask{TMessage}"/> and invokes
    /// <paramref name="onMessage"/> with the resulting message.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="task">The task to execute.</param>
    /// <param name="onMessage">The callback invoked with the produced message.</param>
    /// <param name="ct">A cancellation token.</param>
    public async Task DispatchAsync<TMessage>(
        IClientTask<TMessage> task,
        EventCallback<TMessage> onMessage,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        var message = await task.ExecuteAsync(ct).ConfigureAwait(false);
        if (onMessage.HasDelegate)
        {
            await onMessage.InvokeAsync(message).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Enumerates an <see cref="IClientSubscription{TMessage}"/> and invokes
    /// <paramref name="onMessage"/> for every message produced until the
    /// subscription completes or <paramref name="ct"/> is cancelled.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="subscription">The subscription to enumerate.</param>
    /// <param name="onMessage">The callback invoked for each message.</param>
    /// <param name="ct">A cancellation token.</param>
    public async Task DispatchAsync<TMessage>(
        IClientSubscription<TMessage> subscription,
        EventCallback<TMessage> onMessage,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await foreach (var message in subscription.SubscribeAsync(ct).ConfigureAwait(false))
        {
            if (onMessage.HasDelegate)
            {
                await onMessage.InvokeAsync(message).ConfigureAwait(false);
            }
        }
    }
}
