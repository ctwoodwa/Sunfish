namespace Sunfish.UICore.Contracts;

/// <summary>
/// First-class async primitive returnable from component state transitions.
/// Analog of Iced's <c>Task&lt;Message&gt;</c>. Fire-and-report: awaits a future
/// and produces a message on completion.
/// </summary>
/// <remarks>
/// Per spec §4.5 Phase 2.5 + Appendix E.3 (L2). Supplements Blazor
/// <c>EventCallback</c> for the complex-flow case (real-time sensor dashboards,
/// multi-step wizards, federation event handlers). The default implementation
/// <see cref="ClientTask{TMessage}"/> wraps a plain
/// <see cref="ValueTask{TResult}"/>; hosts register a dispatcher that reads from
/// these and feeds messages back into the component's update loop.
/// </remarks>
/// <typeparam name="TMessage">The message type produced on completion.</typeparam>
public interface IClientTask<TMessage>
{
    /// <summary>
    /// Executes the task and returns the message it produces. Respects
    /// <paramref name="ct"/> for cancellation.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The message produced by the task.</returns>
    ValueTask<TMessage> ExecuteAsync(CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IClientTask{TMessage}"/> implementation that wraps a
/// <see cref="ValueTask{TResult}"/> factory. Factories are invoked lazily on
/// <see cref="ExecuteAsync"/>.
/// </summary>
/// <typeparam name="TMessage">The message type produced on completion.</typeparam>
/// <param name="Factory">
/// A factory producing the <see cref="ValueTask{TResult}"/> to await. Invoked
/// once per <see cref="ExecuteAsync"/> call.
/// </param>
public sealed record ClientTask<TMessage>(Func<CancellationToken, ValueTask<TMessage>> Factory)
    : IClientTask<TMessage>
{
    /// <inheritdoc />
    public ValueTask<TMessage> ExecuteAsync(CancellationToken ct = default) => Factory(ct);

    /// <summary>
    /// Creates a task that completes immediately with the given
    /// <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message the returned task should produce when executed.</param>
    /// <returns>A <see cref="ClientTask{TMessage}"/> that yields <paramref name="message"/>.</returns>
    public static ClientTask<TMessage> FromResult(TMessage message) =>
        new(_ => ValueTask.FromResult(message));

    /// <summary>
    /// Creates a no-op task that completes immediately with the default message
    /// value. Useful for state transitions that do not need to produce follow-up
    /// messages.
    /// </summary>
    /// <returns>A <see cref="ClientTask{TMessage}"/> that yields <c>default(TMessage)</c>.</returns>
    public static ClientTask<TMessage> None() =>
        new(_ => ValueTask.FromResult(default(TMessage)!));
}
