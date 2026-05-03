namespace Sunfish.Ingestion.Core;

/// <summary>
/// Ambient context for a single ingestion call — carries tenant, actor, correlation id, and
/// start time. Passed through the middleware chain unchanged (use <see cref="With"/> to derive
/// modified copies without mutating the original).
/// </summary>
/// <param name="TenantId">The tenant (or workspace) that owns the ingested entity.</param>
/// <param name="ActorId">The actor (user or service principal) that initiated the ingestion.</param>
/// <param name="CorrelationId">An opaque id that ties this ingestion to a broader operation.</param>
/// <param name="StartedUtc">UTC instant at which the ingestion was initiated.</param>
public sealed record IngestionContext(
    string TenantId,
    string ActorId,
    string CorrelationId,
    DateTime StartedUtc)
{
    /// <summary>
    /// Creates a new <see cref="IngestionContext"/> with a freshly generated correlation id
    /// and <see cref="DateTime.UtcNow"/> as <see cref="StartedUtc"/>.
    /// </summary>
    public static IngestionContext NewCorrelation(string tenantId, string actorId) =>
        new(tenantId, actorId, Guid.NewGuid().ToString("n"), DateTime.UtcNow);

    /// <summary>
    /// Returns a copy of this context with one or both identity fields replaced. Useful when an
    /// upstream component needs to re-scope an ingestion to a different tenant or acting principal
    /// without losing the correlation id.
    /// </summary>
    public IngestionContext With(string? tenantId = null, string? actorId = null) =>
        this with { TenantId = tenantId ?? TenantId, ActorId = actorId ?? ActorId };
}
