namespace Sunfish.Ingestion.Core.Hooks;

/// <summary>
/// Optional post-ingestion hook invoked by modality pipelines after a successful (or failed)
/// ingestion completes. Handlers may publish events, update projections, or emit telemetry.
/// </summary>
/// <typeparam name="TResult">The ingestion result payload type.</typeparam>
public interface IPostIngestHandler<TResult>
{
    /// <summary>
    /// Handles a completed ingestion. Implementations should be side-effect-only and must not
    /// throw for expected failure modes — use the <see cref="IngestionContext"/> for correlation.
    /// </summary>
    ValueTask HandleAsync(TResult result, IngestionContext context, CancellationToken ct);
}
