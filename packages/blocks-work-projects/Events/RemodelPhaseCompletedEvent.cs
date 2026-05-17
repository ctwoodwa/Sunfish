using Sunfish.Blocks.WorkProjects.Models;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Payload for <c>Work.RemodelPhaseCompleted</c> per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c>
/// §3.2 catalog. Idempotency-key format
/// <c>remodel-phase-completed:{phaseId}</c> (one-shot — phase
/// completion is terminal).
/// </summary>
public sealed record RemodelPhaseCompletedEvent(
    RemodelPhaseId PhaseId,
    RemodelProjectId RemodelProjectId,
    ProjectId ProjectId,
    int Ordinal,
    string Name,
    decimal? ActualAmount,
    string? Currency,
    DateOnly ActualEndDate);
