using Sunfish.Blocks.WorkProjects.Models;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Payload for <c>Work.ProjectCreated</c> per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §3.2.
/// Idempotency-key format <c>project-created:{projectId}</c> (one-shot).
/// </summary>
public sealed record ProjectCreatedEvent(
    ProjectId ProjectId,
    string Code,
    string Name,
    ProjectKind Kind,
    Guid? PropertyId,
    Guid? CustomerPartyId,
    Guid OwnerPartyId);
