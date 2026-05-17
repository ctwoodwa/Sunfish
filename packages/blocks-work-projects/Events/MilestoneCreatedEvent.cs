using Sunfish.Blocks.WorkProjects.Models;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Payload for <c>Work.MilestoneCreated</c>. Idempotency-key
/// <c>milestone-created:{milestoneId}</c> (one-shot).
/// </summary>
public sealed record MilestoneCreatedEvent(
    MilestoneId MilestoneId,
    ProjectId ProjectId,
    string Code,
    MilestoneKind Kind,
    DateOnly PlannedDate,
    decimal? PaymentAmount,
    string? PaymentCurrency,
    bool TriggersInvoice);
