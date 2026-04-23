namespace Sunfish.Kernel.Crdt.SnapshotScheduling;

/// <summary>
/// Snapshot when op-count AND time thresholds are both exceeded.
/// </summary>
/// <remarks>
/// Both thresholds must be exceeded before a snapshot fires. Using <i>AND</i> rather
/// than <i>OR</i> matches paper §9's intent: shallow snapshots are reserved for extreme
/// cases; a merely old but quiet document does not need one.
/// </remarks>
public sealed class ThresholdShallowSnapshotPolicy : IShallowSnapshotPolicy
{
    /// <summary>Minimum operation count before a snapshot is eligible. Default 10 000.</summary>
    public ulong OperationThreshold { get; init; } = 10_000;

    /// <summary>Minimum interval between consecutive snapshots. Default 24 hours.</summary>
    public TimeSpan MinIntervalBetweenSnapshots { get; init; } = TimeSpan.FromHours(24);

    /// <inheritdoc />
    public bool ShouldTakeShallowSnapshot(ICrdtDocument doc, DocumentStatistics stats, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(stats);

        if (stats.OperationCount < OperationThreshold) return false;

        if (stats.LastShallowSnapshotAt is { } last)
        {
            if (now - last < MinIntervalBetweenSnapshots) return false;
        }

        return true;
    }
}
