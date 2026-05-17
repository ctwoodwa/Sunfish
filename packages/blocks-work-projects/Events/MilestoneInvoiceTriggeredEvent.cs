using Sunfish.Blocks.WorkProjects.Models;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Payload for <c>Work.MilestoneInvoiceTriggered</c>. Idempotency-key
/// <c>milestone-invoice:{milestoneId}</c> (one-shot — emitted exactly
/// once when an invoice-triggering milestone is achieved). Consumed by
/// <c>blocks-financial-ar</c>.
/// </summary>
public sealed record MilestoneInvoiceTriggeredEvent(
    MilestoneId MilestoneId,
    ProjectId ProjectId,
    decimal PaymentAmount,
    string PaymentCurrency,
    Guid CustomerPartyId);
