namespace Sunfish.Ingestion.Core.Quota;

/// <summary>
/// Provides per-tenant token-bucket quota management for the ingestion pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The contract uses a <em>token-bucket</em> model: each tenant starts with a full bucket of
/// <see cref="QuotaPolicy.Capacity"/> tokens. Each ingestion attempt consumes
/// <c>tokensRequested</c> tokens. The bucket refills by <see cref="QuotaPolicy.RefillTokens"/>
/// every <see cref="QuotaPolicy.RefillInterval"/>. Refill is computed <em>lazily</em> on access
/// rather than via a background timer.
/// </para>
/// <para>
/// The default implementation is <see cref="InMemoryIngestionQuotaStore"/>, which is suitable for
/// single-process deployments. A distributed implementation backed by Redis (using Lua scripts for
/// atomic compare-and-swap) is planned as a future work item; see GitHub issue
/// <c>sunfish/platform#TODO</c> for tracking.
/// </para>
/// </remarks>
public interface IIngestionQuotaStore
{
    /// <summary>
    /// Attempts to consume <paramref name="tokensRequested"/> tokens from the bucket belonging to
    /// <paramref name="tenantId"/>. Applies any pending refill before checking availability.
    /// </summary>
    /// <param name="tenantId">The tenant whose bucket is debited.</param>
    /// <param name="tokensRequested">Number of tokens to consume. Must be &gt; 0.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the tokens were granted and deducted from the bucket;
    /// <c>false</c> if the bucket did not contain enough tokens (rate-limited).
    /// </returns>
    ValueTask<bool> TryConsumeAsync(string tenantId, int tokensRequested, CancellationToken ct);

    /// <summary>
    /// Returns the current bucket level and refill timing for <paramref name="tenantId"/>,
    /// without consuming any tokens. Applies any pending refill before returning.
    /// </summary>
    /// <param name="tenantId">The tenant whose bucket is inspected.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<QuotaStatus> GetStatusAsync(string tenantId, CancellationToken ct);
}
