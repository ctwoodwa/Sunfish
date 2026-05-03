namespace Sunfish.Kernel.SchemaRegistry.Compaction;

/// <summary>
/// Scheduler for periodic stream-compaction jobs. Paper §7.2: <i>"this architecture treats
/// upcasters as a short- to medium-term tool and mandates periodic stream compaction: a
/// background copy-transform job replays the original stream, applies all lenses and
/// upcasters, and writes a compact current-version stream."</i>
/// </summary>
/// <remarks>
/// <para>
/// A single <see cref="ICompactionScheduler"/> can host many independent jobs — one per
/// stream-pair. Each job is identified by a caller-chosen <see cref="CompactionJobDefinition.JobId"/>,
/// fires on its own cadence, and is serialized with itself (two runs of the same job never
/// execute concurrently). Runs of <i>different</i> jobs may execute in parallel.
/// </para>
/// <para>
/// Run history is kept in-memory and bounded to the most recent 100 runs per job.
/// </para>
/// </remarks>
public interface ICompactionScheduler
{
    /// <summary>Register a periodic compaction job. Re-registering the same <c>JobId</c> overwrites the prior definition.</summary>
    void Schedule(CompactionJobDefinition job);

    /// <summary>Trigger a compaction run immediately (out-of-cycle) for the named job.</summary>
    Task<CompactionRun> TriggerNowAsync(string jobId, CancellationToken ct);

    /// <summary>All currently-scheduled jobs.</summary>
    IReadOnlyCollection<CompactionJobDefinition> Jobs { get; }

    /// <summary>Runs completed or failed; newest first. Bounded to the most recent 100 per job.</summary>
    IReadOnlyList<CompactionRun> RunHistory { get; }

    /// <summary>Fired after a run reaches a terminal status (<see cref="CompactionStatus.Succeeded"/>, <see cref="CompactionStatus.Failed"/>, or <see cref="CompactionStatus.Cancelled"/>).</summary>
    event EventHandler<CompactionCompletedEventArgs>? CompactionCompleted;
}

/// <summary>
/// Definition of a periodic compaction job.
/// </summary>
/// <param name="JobId">Caller-chosen job identifier. Used to serialize runs of the same job and to key run history.</param>
/// <param name="SourceLogName">Human-readable source-log label, used only for reporting (the actual log instances are resolved internally).</param>
/// <param name="TargetLogName">Human-readable target-log label.</param>
/// <param name="Cadence">How often the job fires (e.g. <c>TimeSpan.FromDays(7)</c>).</param>
/// <param name="TargetSchemaVersion">Schema version the migrated stream should be stamped to.</param>
public sealed record CompactionJobDefinition(
    string JobId,
    string SourceLogName,
    string TargetLogName,
    TimeSpan Cadence,
    string TargetSchemaVersion);

/// <summary>
/// Outcome of a single compaction run (historic or in-flight).
/// </summary>
/// <param name="JobId">The <see cref="CompactionJobDefinition.JobId"/> this run belongs to.</param>
/// <param name="StartedAt">When the run began.</param>
/// <param name="CompletedAt">When the run reached a terminal status; <see langword="null"/> while the run is still in-flight.</param>
/// <param name="Status">Terminal status, or <see cref="CompactionStatus.Running"/> while in-flight.</param>
/// <param name="EventsRead">Events read from the source log.</param>
/// <param name="EventsWritten">Events written to the target log.</param>
/// <param name="EventsDropped">Events intentionally dropped (bad lens output, etc.).</param>
/// <param name="ErrorMessage">Populated when <see cref="Status"/> is <see cref="CompactionStatus.Failed"/>; otherwise <see langword="null"/>.</param>
public sealed record CompactionRun(
    string JobId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    CompactionStatus Status,
    ulong EventsRead,
    ulong EventsWritten,
    ulong EventsDropped,
    string? ErrorMessage);

/// <summary>Terminal status of a <see cref="CompactionRun"/> (or <see cref="Running"/> while in-flight).</summary>
public enum CompactionStatus
{
    /// <summary>Run is in-flight.</summary>
    Running,

    /// <summary>Run completed without error.</summary>
    Succeeded,

    /// <summary>Run threw an exception; see <see cref="CompactionRun.ErrorMessage"/>.</summary>
    Failed,

    /// <summary>Run was cancelled via its cancellation token.</summary>
    Cancelled,
}

/// <summary>Event args for <see cref="ICompactionScheduler.CompactionCompleted"/>.</summary>
/// <param name="Run">The run that just completed.</param>
public sealed record CompactionCompletedEventArgs(CompactionRun Run);
