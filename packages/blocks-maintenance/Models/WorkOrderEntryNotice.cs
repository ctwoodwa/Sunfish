using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Right-of-entry notice attached to a <see cref="WorkOrder"/>. Multiple
/// notices may exist per work order (e.g., proposed entry + revised entry
/// + actual entry). Per ADR 0053 §"Decision".
/// </summary>
public sealed record WorkOrderEntryNotice
{
    /// <summary>Stable identifier for this notice.</summary>
    public required WorkOrderEntryNoticeId Id { get; init; }

    /// <summary>The work order this notice attaches to.</summary>
    public required WorkOrderId WorkOrder { get; init; }

    /// <summary>Wall-clock time of the planned entry.</summary>
    public required DateTimeOffset PlannedEntryUtc { get; init; }

    /// <summary>Free-text reason for entry surfaced to the notified parties.</summary>
    public required string EntryReason { get; init; }

    /// <summary>Actor that issued the notice (typically the operator).</summary>
    public required ActorId NotifiedBy { get; init; }

    /// <summary>Wall-clock time the notice was issued.</summary>
    public required DateTimeOffset NotifiedAt { get; init; }

    /// <summary>Actor ids of the notified parties (tenants, occupants, etc.). Empty when no parties have been determined yet.</summary>
    public IReadOnlyList<ActorId> NotifiedParties { get; init; } = Array.Empty<ActorId>();
}
