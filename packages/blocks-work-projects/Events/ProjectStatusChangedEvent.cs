using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Payload for <c>Work.ProjectStatusChanged</c>. Idempotency-key
/// <c>project-status:{projectId}:{occurredAtTicks}</c> — multi-fire
/// safe (project lifecycle can transition multiple times).
/// </summary>
public sealed record ProjectStatusChangedEvent(
    ProjectId ProjectId,
    ProjectStatus FromStatus,
    ProjectStatus ToStatus,
    Guid TransitionedByPartyId,
    Instant TransitionedAt);
