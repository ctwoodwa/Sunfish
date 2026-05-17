using Sunfish.Blocks.WorkProjects.Models;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Payload for <c>Work.MilestoneAchieved</c>. Idempotency-key
/// <c>milestone-achieved:{milestoneId}</c> (one-shot — already in catalog).
/// </summary>
public sealed record MilestoneAchievedEvent(
    MilestoneId MilestoneId,
    ProjectId ProjectId,
    DateOnly AchievedDate,
    decimal? Weight);
