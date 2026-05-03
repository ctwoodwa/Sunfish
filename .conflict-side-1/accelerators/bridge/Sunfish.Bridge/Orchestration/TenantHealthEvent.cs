namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Payload raised by <see cref="TenantHealthMonitor"/> whenever a tenant's
/// health status transitions (Healthy → Unhealthy or Unhealthy → Healthy).
/// Wave 5.2.C's <see cref="ITenantProcessSupervisor"/> subscribes to these
/// to drive its own <see cref="TenantProcessState"/> transitions — the
/// monitor itself never calls the supervisor directly, keeping the two
/// responsibilities decoupled.
/// </summary>
/// <param name="TenantId">Tenant whose health transitioned.</param>
/// <param name="Previous">Health observed before the transition. For the
/// first emission for a newly-polled tenant this is
/// <see cref="TenantHealthStatus.Unknown"/>.</param>
/// <param name="Current">Health observed after the transition.</param>
/// <param name="OccurredAt">UTC wall-clock instant of the transition.</param>
/// <param name="Reason">Optional human-readable reason — e.g.
/// "three consecutive health-probe failures" or "health probe succeeded
/// after previous failure". Intended for structured logging and operator
/// diagnostics.</param>
public sealed record TenantHealthEvent(
    Guid TenantId,
    TenantHealthStatus Previous,
    TenantHealthStatus Current,
    DateTimeOffset OccurredAt,
    string? Reason);

/// <summary>
/// Health status tracked by <see cref="TenantHealthMonitor"/>. Intentionally
/// narrower than <see cref="TenantProcessState"/> — the monitor observes only
/// probe success/failure, not process lifecycle. Wave 5.2.C's supervisor
/// projects these into <see cref="TenantProcessState"/> via event
/// subscription.
/// </summary>
public enum TenantHealthStatus
{
    /// <summary>
    /// Monitor has not yet polled the tenant, or the tenant is not in the
    /// endpoint registry. Default state before the first poll.
    /// </summary>
    Unknown,

    /// <summary>
    /// Last poll returned HTTP 200 with body "Healthy". Strike count is 0.
    /// </summary>
    Healthy,

    /// <summary>
    /// Three (configurable via
    /// <see cref="BridgeOrchestrationOptions.HealthFailureStrikeCount"/>)
    /// consecutive polls failed — HTTP non-2xx, timeout, or transport error.
    /// </summary>
    Unhealthy,
}
