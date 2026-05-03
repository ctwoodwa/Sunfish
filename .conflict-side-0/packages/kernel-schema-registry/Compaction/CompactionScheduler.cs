using Sunfish.Kernel.Events;
using Sunfish.Kernel.SchemaRegistry.Lenses;
using Sunfish.Kernel.SchemaRegistry.Migration;

namespace Sunfish.Kernel.SchemaRegistry.Compaction;

/// <summary>
/// Default in-memory <see cref="ICompactionScheduler"/>. Uses a <see cref="PeriodicTimer"/>
/// per job and serializes runs of the same job behind a per-job <see cref="SemaphoreSlim"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolution model:</b> the scheduler itself does not know how to turn a
/// <see cref="CompactionJobDefinition.SourceLogName"/> into an <see cref="IEventLog"/> —
/// that's a deployment concern. The caller supplies a resolver delegate at construction
/// time. This keeps the scheduler agnostic to whether logs are in-memory, file-backed, or
/// remote, and mirrors the <see cref="CopyTransformMigrator"/> contract which also takes
/// log instances by parameter rather than by name.
/// </para>
/// <para>
/// <b>Concurrency:</b> each job has its own <see cref="SemaphoreSlim"/> with a count of
/// one, so a second timer tick (or a concurrent <see cref="TriggerNowAsync"/>) that lands
/// while a run is already executing will queue behind it. Runs of <i>different</i> jobs
/// never block each other.
/// </para>
/// <para>
/// <b>Run history bounding:</b> per-job FIFO bounded at
/// <see cref="HistoryPerJobLimit"/>. When a new run is enqueued beyond the cap, the oldest
/// run for that job is evicted. Callers that need long-term retention should persist
/// runs out-of-band.
/// </para>
/// </remarks>
public sealed class CompactionScheduler : ICompactionScheduler, IAsyncDisposable
{
    /// <summary>Maximum run history retained per job.</summary>
    public const int HistoryPerJobLimit = 100;

    private readonly Func<string, (IEventLog Source, IEventLog Target)> _resolveLogs;
    private readonly Func<LensGraph> _resolveLensGraph;
    private readonly CopyTransformMigrator _migrator;
    private readonly TimeProvider _time;

    private readonly object _gate = new();
    private readonly Dictionary<string, CompactionJobDefinition> _jobs = new();
    private readonly Dictionary<string, SemaphoreSlim> _jobLocks = new();
    private readonly Dictionary<string, Task> _timerLoops = new();
    private readonly Dictionary<string, CancellationTokenSource> _timerCts = new();
    private readonly Dictionary<string, LinkedList<CompactionRun>> _historyByJob = new();

    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<CompactionCompletedEventArgs>? CompactionCompleted;

    /// <summary>
    /// Create a scheduler that resolves log instances and the lens graph lazily at run
    /// time. Uses a fresh <see cref="CopyTransformMigrator"/> internally and
    /// <see cref="TimeProvider.System"/> for timestamps.
    /// </summary>
    /// <param name="resolveLogs">Given a <see cref="CompactionJobDefinition.JobId"/>, return the source and target <see cref="IEventLog"/> for that job.</param>
    /// <param name="resolveLensGraph">Return the lens graph to use for transformations.</param>
    public CompactionScheduler(
        Func<string, (IEventLog Source, IEventLog Target)> resolveLogs,
        Func<LensGraph> resolveLensGraph)
        : this(resolveLogs, resolveLensGraph, new CopyTransformMigrator(), TimeProvider.System)
    {
    }

    /// <summary>Create a scheduler with all dependencies supplied — test-friendly overload.</summary>
    public CompactionScheduler(
        Func<string, (IEventLog Source, IEventLog Target)> resolveLogs,
        Func<LensGraph> resolveLensGraph,
        CopyTransformMigrator migrator,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(resolveLogs);
        ArgumentNullException.ThrowIfNull(resolveLensGraph);
        ArgumentNullException.ThrowIfNull(migrator);
        ArgumentNullException.ThrowIfNull(time);

        _resolveLogs = resolveLogs;
        _resolveLensGraph = resolveLensGraph;
        _migrator = migrator;
        _time = time;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<CompactionJobDefinition> Jobs
    {
        get
        {
            lock (_gate)
            {
                return _jobs.Values.ToList();
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CompactionRun> RunHistory
    {
        get
        {
            lock (_gate)
            {
                // Flatten per-job lists; sort newest-first.
                return _historyByJob.Values
                    .SelectMany(l => l)
                    .OrderByDescending(r => r.StartedAt)
                    .ToList();
            }
        }
    }

    /// <inheritdoc />
    public void Schedule(CompactionJobDefinition job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrEmpty(job.JobId);

        CancellationTokenSource? oldCts = null;
        Task? oldLoop = null;

        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CompactionScheduler));
            }

            if (_timerCts.TryGetValue(job.JobId, out var existingCts))
            {
                // Re-scheduling — cancel prior loop first, outside the lock.
                oldCts = existingCts;
                _timerCts.Remove(job.JobId);
                _ = _timerLoops.TryGetValue(job.JobId, out oldLoop);
                _timerLoops.Remove(job.JobId);
            }

            _jobs[job.JobId] = job;
            _jobLocks.TryAdd(job.JobId, new SemaphoreSlim(1, 1));
            _historyByJob.TryAdd(job.JobId, new LinkedList<CompactionRun>());

            var cts = new CancellationTokenSource();
            _timerCts[job.JobId] = cts;
            _timerLoops[job.JobId] = Task.Run(() => RunTimerLoopAsync(job.JobId, job.Cadence, cts.Token));
        }

        // Dispose prior loop outside the gate.
        if (oldCts is not null)
        {
            try { oldCts.Cancel(); } catch (ObjectDisposedException) { }
            oldCts.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<CompactionRun> TriggerNowAsync(string jobId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(jobId);

        CompactionJobDefinition job;
        SemaphoreSlim jobLock;

        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CompactionScheduler));
            }

            if (!_jobs.TryGetValue(jobId, out var found))
            {
                throw new InvalidOperationException($"No compaction job registered with id '{jobId}'.");
            }
            job = found;
            jobLock = _jobLocks[jobId];
        }

        return await ExecuteRunAsync(job, jobLock, ct).ConfigureAwait(false);
    }

    private async Task RunTimerLoopAsync(string jobId, TimeSpan cadence, CancellationToken ct)
    {
        // Guard pathological cadences — PeriodicTimer would throw.
        if (cadence <= TimeSpan.Zero)
        {
            return;
        }

        using var timer = new PeriodicTimer(cadence);

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                CompactionJobDefinition job;
                SemaphoreSlim jobLock;

                lock (_gate)
                {
                    if (!_jobs.TryGetValue(jobId, out var found))
                    {
                        return;
                    }
                    job = found;
                    jobLock = _jobLocks[jobId];
                }

                try
                {
                    await ExecuteRunAsync(job, jobLock, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // Exceptions are already captured as Failed runs inside ExecuteRunAsync.
                    // Swallow here so the timer loop keeps firing on subsequent ticks.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Schedule-replace or dispose.
        }
    }

    private async Task<CompactionRun> ExecuteRunAsync(
        CompactionJobDefinition job,
        SemaphoreSlim jobLock,
        CancellationToken ct)
    {
        await jobLock.WaitAsync(ct).ConfigureAwait(false);

        var startedAt = _time.GetUtcNow();
        var running = new CompactionRun(
            JobId: job.JobId,
            StartedAt: startedAt,
            CompletedAt: null,
            Status: CompactionStatus.Running,
            EventsRead: 0,
            EventsWritten: 0,
            EventsDropped: 0,
            ErrorMessage: null);

        CompactionRun terminal;

        try
        {
            var (source, target) = _resolveLogs(job.JobId);
            var lensGraph = _resolveLensGraph();

            var migration = await _migrator
                .MigrateAsync(source, target, lensGraph, job.TargetSchemaVersion, ct)
                .ConfigureAwait(false);

            terminal = running with
            {
                CompletedAt = _time.GetUtcNow(),
                Status = CompactionStatus.Succeeded,
                EventsRead = migration.EventsRead,
                EventsWritten = migration.EventsWritten,
                EventsDropped = migration.EventsDropped,
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            terminal = running with
            {
                CompletedAt = _time.GetUtcNow(),
                Status = CompactionStatus.Cancelled,
            };
        }
        catch (Exception ex)
        {
            terminal = running with
            {
                CompletedAt = _time.GetUtcNow(),
                Status = CompactionStatus.Failed,
                ErrorMessage = ex.Message,
            };
        }
        finally
        {
            jobLock.Release();
        }

        RecordHistory(terminal);

        try
        {
            CompactionCompleted?.Invoke(this, new CompactionCompletedEventArgs(terminal));
        }
        catch
        {
            // Event-handler exceptions must not tear down the scheduler.
        }

        return terminal;
    }

    private void RecordHistory(CompactionRun run)
    {
        lock (_gate)
        {
            if (!_historyByJob.TryGetValue(run.JobId, out var bucket))
            {
                bucket = new LinkedList<CompactionRun>();
                _historyByJob[run.JobId] = bucket;
            }

            // Newest first — AddFirst + trim from the tail.
            bucket.AddFirst(run);
            while (bucket.Count > HistoryPerJobLimit)
            {
                bucket.RemoveLast();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        List<CancellationTokenSource> ctsToDispose;
        List<Task> loops;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            ctsToDispose = _timerCts.Values.ToList();
            loops = _timerLoops.Values.ToList();
            _timerCts.Clear();
            _timerLoops.Clear();
        }

        foreach (var cts in ctsToDispose)
        {
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
        }

        foreach (var loop in loops)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch
            {
                // Best-effort shutdown.
            }
        }

        foreach (var cts in ctsToDispose)
        {
            cts.Dispose();
        }
    }
}
