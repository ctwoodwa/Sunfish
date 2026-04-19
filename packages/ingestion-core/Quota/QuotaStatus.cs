namespace Sunfish.Ingestion.Core.Quota;

/// <summary>
/// Snapshot of a tenant's token-bucket state — returned by
/// <see cref="IIngestionQuotaStore.GetStatusAsync"/> for observability and diagnostics.
/// </summary>
/// <param name="TenantId">The tenant this status describes.</param>
/// <param name="AvailableTokens">Tokens currently in the bucket (after any pending refill).</param>
/// <param name="Capacity">Maximum tokens the bucket can hold.</param>
/// <param name="NextRefillAt">
/// UTC instant at which the next partial refill will be credited. <c>null</c> if the bucket is
/// at capacity and no refill is scheduled.
/// </param>
public readonly record struct QuotaStatus(
    string TenantId,
    int AvailableTokens,
    int Capacity,
    DateTimeOffset? NextRefillAt)
{
    /// <summary><c>true</c> when <see cref="AvailableTokens"/> is zero.</summary>
    public bool IsExhausted => AvailableTokens == 0;
}
