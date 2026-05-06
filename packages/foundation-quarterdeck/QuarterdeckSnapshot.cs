using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Aggregate snapshot returned by
/// <see cref="IQuarterdeckDataProvider.GetSnapshotAsync"/> per
/// ADR 0080 §2.3. Every user session begins on the Quarterdeck;
/// <see cref="QuarterdeckSnapshot"/> is the entry-point payload —
/// OOD watch + Mission Envelope + recent Standing Orders + pending
/// alerts + KPI cards + permission-pre-resolved department links.
/// </summary>
/// <param name="OodWatch">
/// Both OOD-role watch summaries; null actor names indicate inactive
/// watches.
/// </param>
/// <param name="MissionEnvelope">Coarse-grained Mission-Envelope projection.</param>
/// <param name="RecentOrders">
/// Most-recent up to 5 Standing Orders, newest first. Empty list when
/// no orders exist.
/// </param>
/// <param name="PendingAlerts">
/// Aggregate of all permission-filtered alerts from registered alert
/// sources, sorted by <see cref="AlertSeverity"/> ascending then by
/// <see cref="QuarterdeckAlert.IssuedAt"/> descending. Expired
/// non-acknowledgement-required alerts are omitted per §2.3 rule 8.
/// </param>
/// <param name="KpiCards">
/// Aggregate of all department KPI cards from registered KPI sources.
/// Denied cards render with <see cref="DepartmentStatus.Denied"/> +
/// neutral value (denied-not-hidden invariant).
/// </param>
/// <param name="DepartmentLinks">
/// Pre-resolved department link list — one entry per ship location the
/// surface knows about, with the actor's access decision stamped on
/// each. The Quarterdeck UI does not re-resolve permissions.
/// </param>
/// <param name="SnapshotAt">
/// Wall-clock timestamp when the snapshot was assembled. Drives stale
/// detection on the subscriber side.
/// </param>
public sealed record QuarterdeckSnapshot(
    OodWatchSummary OodWatch,
    MissionEnvelopeSummary MissionEnvelope,
    IReadOnlyList<StandingOrderSummary> RecentOrders,
    IReadOnlyList<QuarterdeckAlert> PendingAlerts,
    IReadOnlyList<DepartmentKpi> KpiCards,
    IReadOnlyList<DepartmentLink> DepartmentLinks,
    DateTimeOffset SnapshotAt);
