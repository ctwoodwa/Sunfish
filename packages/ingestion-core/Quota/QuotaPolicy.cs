namespace Sunfish.Ingestion.Core.Quota;

/// <summary>
/// Describes the token-bucket parameters applied to a single tenant (or globally as a default).
/// </summary>
/// <param name="Capacity">
/// The maximum number of tokens the bucket can hold. Also the initial fill level when a tenant
/// is first seen.
/// </param>
/// <param name="RefillTokens">
/// The number of tokens added to the bucket each time the refill interval elapses.
/// Capped at <see cref="Capacity"/> after each refill.
/// </param>
/// <param name="RefillInterval">
/// How often the bucket receives <see cref="RefillTokens"/> new tokens.
/// </param>
public sealed record QuotaPolicy(int Capacity, int RefillTokens, TimeSpan RefillInterval)
{
    /// <summary>
    /// Validates that all field values are positive. Throws <see cref="ArgumentOutOfRangeException"/>
    /// if any constraint is violated.
    /// </summary>
    public void Validate()
    {
        if (Capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(Capacity), "Capacity must be positive.");
        if (RefillTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(RefillTokens), "RefillTokens must be positive.");
        if (RefillInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RefillInterval), "RefillInterval must be positive.");
    }
}
