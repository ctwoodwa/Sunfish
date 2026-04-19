namespace Sunfish.Foundation.LocalFirst;

/// <summary>Result of one sync cycle.</summary>
public sealed record SyncResult
{
    /// <summary>Number of local operations pushed to the remote.</summary>
    public int SentCount { get; init; }

    /// <summary>Number of remote operations pulled locally.</summary>
    public int ReceivedCount { get; init; }

    /// <summary>Number of conflicts encountered and resolved.</summary>
    public int ConflictCount { get; init; }

    /// <summary>Completion timestamp in UTC.</summary>
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Non-fatal errors observed during the cycle.</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>High-level classification of a sync-pipeline event.</summary>
public enum SyncEventKind
{
    /// <summary>Sync cycle started.</summary>
    Started = 0,

    /// <summary>Progress update during a cycle.</summary>
    Progress = 1,

    /// <summary>Conflict detected and being resolved.</summary>
    ConflictDetected = 2,

    /// <summary>Sync cycle completed successfully.</summary>
    Completed = 3,

    /// <summary>Sync cycle failed.</summary>
    Failed = 4,
}

/// <summary>One event observable on the sync pipeline.</summary>
public sealed record SyncEvent
{
    /// <summary>Event classification.</summary>
    public required SyncEventKind Kind { get; init; }

    /// <summary>When the event occurred (UTC).</summary>
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Optional human-readable detail.</summary>
    public string? Detail { get; init; }
}

/// <summary>
/// Orchestrates local ↔ remote sync cycles. Triggered on-demand (user action,
/// connectivity restored, scheduler) — the engine does not assume a network
/// transport or a peer topology.
/// </summary>
public interface ISyncEngine
{
    /// <summary>Runs a single sync cycle and returns its outcome.</summary>
    ValueTask<SyncResult> SyncOnceAsync(CancellationToken cancellationToken = default);

    /// <summary>Streams sync-pipeline events. Consumers subscribe for progress UX.</summary>
    IAsyncEnumerable<SyncEvent> StreamEventsAsync(CancellationToken cancellationToken = default);
}
