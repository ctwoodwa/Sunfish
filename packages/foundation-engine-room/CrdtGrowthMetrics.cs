using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Per-document CRDT growth + compaction-eligibility snapshot per ADR
/// 0079 §1. <see cref="IEngineRoomDataProvider.GetCrdtGrowthMetricsAsync(TenantId, System.Threading.CancellationToken)"/>
/// streams these for triage; the Damage Control panel filters by
/// <see cref="CompactionEligible"/>.
/// </summary>
/// <param name="DocumentId">Opaque document identifier (provider-internal format).</param>
/// <param name="TenantId">Owning tenant.</param>
/// <param name="TotalByteEstimate">Approximate on-disk byte size for the CRDT document.</param>
/// <param name="TombstoneCount">Number of tombstone entries (deleted-but-retained).</param>
/// <param name="CompactionEligible">True when the document meets compaction thresholds; UI surfaces this in the Damage Control panel.</param>
/// <param name="MeasuredAt">Wall-clock time the metrics were measured.</param>
public sealed record CrdtGrowthMetrics(
    string DocumentId,
    TenantId TenantId,
    long TotalByteEstimate,
    int TombstoneCount,
    bool CompactionEligible,
    DateTimeOffset MeasuredAt);
