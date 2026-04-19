namespace Sunfish.UICore.Contracts;

/// <summary>
/// Long-running message stream for real-time UI (sensor feeds, voice transcripts,
/// federation event fan-out). Analog of Iced's <c>Subscription&lt;Message&gt;</c>.
/// </summary>
/// <remarks>
/// Per spec §4.5 Phase 2.5 + Appendix E.3 (L2). The default implementation
/// <see cref="ClientSubscription{TMessage}"/> wraps an
/// <see cref="IAsyncEnumerable{T}"/>; hosts register a dispatcher that reads from
/// these and feeds messages back into the component's update loop until the
/// subscription is cancelled or completes.
/// </remarks>
/// <typeparam name="TMessage">The message type produced by the stream.</typeparam>
public interface IClientSubscription<TMessage>
{
    /// <summary>
    /// Opens the subscription and returns an async stream of messages. The stream
    /// terminates when <paramref name="ct"/> is cancelled or the underlying source
    /// completes.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An async stream of messages.</returns>
    IAsyncEnumerable<TMessage> SubscribeAsync(CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IClientSubscription{TMessage}"/> implementation that wraps
/// an <see cref="IAsyncEnumerable{T}"/> factory. Factories are invoked lazily on
/// <see cref="SubscribeAsync"/>.
/// </summary>
/// <typeparam name="TMessage">The message type produced by the stream.</typeparam>
/// <param name="Factory">
/// A factory producing the <see cref="IAsyncEnumerable{T}"/> to enumerate.
/// Invoked once per <see cref="SubscribeAsync"/> call.
/// </param>
public sealed record ClientSubscription<TMessage>(
    Func<CancellationToken, IAsyncEnumerable<TMessage>> Factory)
    : IClientSubscription<TMessage>
{
    /// <inheritdoc />
    public IAsyncEnumerable<TMessage> SubscribeAsync(CancellationToken ct = default) =>
        Factory(ct);
}
