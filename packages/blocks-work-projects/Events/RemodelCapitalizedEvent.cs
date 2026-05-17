using Sunfish.Blocks.WorkProjects.Models;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Payload for <c>Work.RemodelCapitalized</c> per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c>
/// §3.2 catalog. The financial cluster reactor consumes this event to
/// post a capital-asset <c>JournalEntry</c> (debit FixedAsset/CIP;
/// credit cost-clearing). Idempotency-key format
/// <c>remodel-capitalized:{remodelProjectId}</c> (one-shot —
/// capitalization is terminal).
/// </summary>
public sealed record RemodelCapitalizedEvent(
    RemodelProjectId RemodelProjectId,
    ProjectId ProjectId,
    Guid? PropertyId,
    Guid CapitalizationAccountId,
    decimal CapitalizedAmount,
    string Currency,
    DateOnly PlacedInServiceDate);
