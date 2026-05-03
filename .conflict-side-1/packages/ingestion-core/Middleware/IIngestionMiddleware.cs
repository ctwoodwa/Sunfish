namespace Sunfish.Ingestion.Core.Middleware;

/// <summary>
/// Delegate representing the next stage in the ingestion middleware chain. Invoking it runs the
/// remainder of the chain (including the terminal) for the given <paramref name="input"/>.
/// </summary>
/// <typeparam name="TInput">The modality-specific input type.</typeparam>
/// <param name="input">The modality-specific input payload.</param>
/// <param name="context">The ambient ingestion context.</param>
/// <param name="ct">Cancellation token propagated through the chain.</param>
public delegate ValueTask<IngestionResult<IngestedEntity>> IngestionDelegate<TInput>(
    TInput input, IngestionContext context, CancellationToken ct);

/// <summary>
/// A unit of behavior that wraps the ingestion pipeline — equivalent in spirit to ASP.NET Core
/// middleware. Implementations must call the supplied <c>next</c> delegate to continue the chain,
/// or return a short-circuited <see cref="IngestionResult{T}"/> to abort.
/// </summary>
/// <typeparam name="TInput">The modality-specific input type.</typeparam>
public interface IIngestionMiddleware<TInput>
{
    /// <summary>
    /// Invokes this middleware. Implementations may short-circuit by returning a result without
    /// calling <paramref name="next"/>, or they may call <paramref name="next"/> and inspect or
    /// transform its result before returning.
    /// </summary>
    /// <param name="input">The modality-specific input payload.</param>
    /// <param name="context">The ambient ingestion context.</param>
    /// <param name="next">The remainder of the middleware chain (plus the terminal delegate).</param>
    /// <param name="ct">Cancellation token propagated through the chain.</param>
    ValueTask<IngestionResult<IngestedEntity>> InvokeAsync(
        TInput input,
        IngestionContext context,
        IngestionDelegate<TInput> next,
        CancellationToken ct);
}
