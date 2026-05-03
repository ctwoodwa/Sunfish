using Sunfish.Ingestion.Core.Middleware;

namespace Sunfish.Ingestion.Core.Quota;

/// <summary>
/// Ingestion middleware that enforces per-tenant ingestion quotas using a token-bucket algorithm.
/// Short-circuits the pipeline with <see cref="IngestOutcome.QuotaExceeded"/> when the calling
/// tenant's bucket is exhausted.
/// </summary>
/// <typeparam name="TInput">The modality-specific input type.</typeparam>
/// <remarks>
/// Place this middleware early in the pipeline (ideally after authentication/tenant resolution
/// but before any expensive operations) so that rate-limited calls fail fast.
/// </remarks>
public sealed class QuotaMiddleware<TInput>(IIngestionQuotaStore store, int tokensPerCall = 1)
    : IIngestionMiddleware<TInput>
{
    /// <inheritdoc />
    public async ValueTask<IngestionResult<IngestedEntity>> InvokeAsync(
        TInput input,
        IngestionContext context,
        IngestionDelegate<TInput> next,
        CancellationToken ct)
    {
        var granted = await store.TryConsumeAsync(context.TenantId, tokensPerCall, ct);

        if (!granted)
        {
            return IngestionResult<IngestedEntity>.Fail(
                IngestOutcome.QuotaExceeded,
                $"Ingestion quota exceeded for tenant '{context.TenantId}'. " +
                $"Back off and retry after the quota window refills.");
        }

        return await next(input, context, ct);
    }
}
