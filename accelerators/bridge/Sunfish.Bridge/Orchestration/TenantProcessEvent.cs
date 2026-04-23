namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Payload raised on <see cref="ITenantProcessSupervisor.StateChanged"/> when
/// a tenant's <see cref="TenantProcessState"/> changes. Consumed by the
/// lifecycle coordinator (Wave 5.2.C) and by observability sinks (5.2.D).
/// </summary>
/// <param name="TenantId">Identity of the tenant whose process transitioned.</param>
/// <param name="Previous">State observed before the transition. On the first
/// emission for a newly-tracked tenant this is <see cref="TenantProcessState.Unknown"/>.</param>
/// <param name="Current">State observed after the transition.</param>
/// <param name="OccurredAt">UTC wall-clock instant the transition occurred,
/// as measured by the supervisor. Not the instant the event was dispatched
/// to subscribers.</param>
/// <param name="Reason">Optional human-readable explanation — e.g. "three
/// consecutive health-probe failures" or "operator PauseAsync call". Intended
/// for structured logging and operator diagnostics; not a machine-readable
/// discriminator. <see langword="null"/> when the transition does not warrant
/// one.</param>
/// <remarks>
/// Record-type equality is safe to use as a dedup key by downstream consumers
/// because the tuple (TenantId, Previous, Current, OccurredAt) is unique per
/// transition.
/// </remarks>
public sealed record TenantProcessEvent(
    Guid TenantId,
    TenantProcessState Previous,
    TenantProcessState Current,
    DateTimeOffset OccurredAt,
    string? Reason);
