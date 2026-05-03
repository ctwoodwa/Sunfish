using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Events;
using Sunfish.Kernel.SchemaRegistry.Compaction;
using Sunfish.Kernel.SchemaRegistry.Lenses;
using Sunfish.Kernel.SchemaRegistry.Migration;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Contract coverage for <see cref="CompactionScheduler"/> — periodic firing, ad-hoc
/// triggering, per-job serialization, history bounding, completion event, cancellation,
/// and failure surfacing.
/// </summary>
public class CompactionSchedulerTests
{
    private const string Kind = "record.updated";
    private static readonly EntityId SampleEntity = new("sunfish", "local", "record-1");

    private static KernelEvent MakeEvent(string schemaVersion)
    {
        return new KernelEvent(
            Id: EventId.NewId(),
            EntityId: SampleEntity,
            Kind: Kind,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: new Dictionary<string, object?>
            {
                [CopyTransformMigrator.SchemaVersionPayloadKey] = schemaVersion,
            });
    }

    private static CompactionScheduler MakeScheduler(
        IEventLog source,
        IEventLog target,
        LensGraph? graph = null,
        CopyTransformMigrator? migrator = null)
    {
        graph ??= new LensGraph();
        return new CompactionScheduler(
            resolveLogs: _ => (source, target),
            resolveLensGraph: () => graph,
            migrator: migrator ?? new CopyTransformMigrator(),
            time: TimeProvider.System);
    }

    [Fact]
    public async Task Schedule_PeriodicTimer_FiresAtLeastOnce()
    {
        var source = new InMemoryEventLog();
        await source.AppendAsync(MakeEvent("v1"), CancellationToken.None);
        var target = new InMemoryEventLog();

        await using var scheduler = MakeScheduler(source, target);

        using var done = new ManualResetEventSlim();
        scheduler.CompactionCompleted += (_, __) => done.Set();

        scheduler.Schedule(new CompactionJobDefinition(
            JobId: "job-tick",
            SourceLogName: "src",
            TargetLogName: "tgt",
            Cadence: TimeSpan.FromMilliseconds(100),
            TargetSchemaVersion: "v1"));

        Assert.True(done.Wait(TimeSpan.FromSeconds(5)), "Expected a compaction run within 5 seconds.");
    }

    [Fact]
    public async Task TriggerNowAsync_ProducesRunImmediately()
    {
        var source = new InMemoryEventLog();
        await source.AppendAsync(MakeEvent("v1"), CancellationToken.None);
        await source.AppendAsync(MakeEvent("v1"), CancellationToken.None);
        var target = new InMemoryEventLog();

        await using var scheduler = MakeScheduler(source, target);

        scheduler.Schedule(new CompactionJobDefinition(
            JobId: "job-trigger",
            SourceLogName: "src",
            TargetLogName: "tgt",
            Cadence: TimeSpan.FromHours(1), // far enough out that the timer won't fire in this test
            TargetSchemaVersion: "v1"));

        var run = await scheduler.TriggerNowAsync("job-trigger", CancellationToken.None);

        Assert.Equal(CompactionStatus.Succeeded, run.Status);
        Assert.Equal(2UL, run.EventsRead);
        Assert.Equal(2UL, run.EventsWritten);
    }

    [Fact]
    public async Task TwoJobs_RunIndependently()
    {
        var sourceA = new InMemoryEventLog();
        var sourceB = new InMemoryEventLog();
        await sourceA.AppendAsync(MakeEvent("v1"), CancellationToken.None);
        await sourceB.AppendAsync(MakeEvent("v1"), CancellationToken.None);
        await sourceB.AppendAsync(MakeEvent("v1"), CancellationToken.None);

        var targetA = new InMemoryEventLog();
        var targetB = new InMemoryEventLog();
        var graph = new LensGraph();

        await using var scheduler = new CompactionScheduler(
            resolveLogs: id => id == "job-a" ? (sourceA, targetA) : (sourceB, targetB),
            resolveLensGraph: () => graph,
            migrator: new CopyTransformMigrator(),
            time: TimeProvider.System);

        scheduler.Schedule(new CompactionJobDefinition("job-a", "a-src", "a-tgt", TimeSpan.FromHours(1), "v1"));
        scheduler.Schedule(new CompactionJobDefinition("job-b", "b-src", "b-tgt", TimeSpan.FromHours(1), "v1"));

        var runA = await scheduler.TriggerNowAsync("job-a", CancellationToken.None);
        var runB = await scheduler.TriggerNowAsync("job-b", CancellationToken.None);

        Assert.Equal(CompactionStatus.Succeeded, runA.Status);
        Assert.Equal(1UL, runA.EventsRead);
        Assert.Equal(CompactionStatus.Succeeded, runB.Status);
        Assert.Equal(2UL, runB.EventsRead);
    }

    [Fact]
    public async Task RunHistory_BoundedTo100PerJob()
    {
        var source = new InMemoryEventLog();
        var target = new InMemoryEventLog();

        await using var scheduler = MakeScheduler(source, target);
        scheduler.Schedule(new CompactionJobDefinition("job-cap", "src", "tgt", TimeSpan.FromHours(1), "v1"));

        for (var i = 0; i < 101; i++)
        {
            await scheduler.TriggerNowAsync("job-cap", CancellationToken.None);
        }

        var history = scheduler.RunHistory;
        Assert.Equal(CompactionScheduler.HistoryPerJobLimit, history.Count);
        Assert.All(history, run => Assert.Equal("job-cap", run.JobId));
    }

    [Fact]
    public async Task CompactionCompleted_EventFires()
    {
        var source = new InMemoryEventLog();
        await source.AppendAsync(MakeEvent("v1"), CancellationToken.None);
        var target = new InMemoryEventLog();

        await using var scheduler = MakeScheduler(source, target);

        CompactionRun? observed = null;
        using var fired = new ManualResetEventSlim();
        scheduler.CompactionCompleted += (_, args) =>
        {
            observed = args.Run;
            fired.Set();
        };

        scheduler.Schedule(new CompactionJobDefinition("job-evt", "src", "tgt", TimeSpan.FromHours(1), "v1"));
        await scheduler.TriggerNowAsync("job-evt", CancellationToken.None);

        Assert.True(fired.Wait(TimeSpan.FromSeconds(2)));
        Assert.NotNull(observed);
        Assert.Equal("job-evt", observed!.JobId);
        Assert.Equal(CompactionStatus.Succeeded, observed.Status);
    }

    /// <summary>
    /// Concurrent TriggerNowAsync calls for the same job must serialize — the scheduler's
    /// per-job SemaphoreSlim guarantees at most one run per job at a time.
    /// </summary>
    [Fact]
    public async Task TriggerNowAsync_SameJob_SerializedByLock()
    {
        var source = new InMemoryEventLog();
        await source.AppendAsync(MakeEvent("v1"), CancellationToken.None);
        var target = new InMemoryEventLog();

        var observedMax = 0;
        var current = 0;
        var migrator = new ObservingMigrator(
            onEnter: () =>
            {
                var value = Interlocked.Increment(ref current);
                // Track the concurrent peak observed inside MigrateAsync.
                int old;
                do
                {
                    old = Volatile.Read(ref observedMax);
                    if (value <= old) break;
                }
                while (Interlocked.CompareExchange(ref observedMax, value, old) != old);
            },
            onExit: () => Interlocked.Decrement(ref current),
            dwell: TimeSpan.FromMilliseconds(50));

        await using var scheduler = MakeScheduler(source, target, migrator: migrator);
        scheduler.Schedule(new CompactionJobDefinition("job-race", "src", "tgt", TimeSpan.FromHours(1), "v1"));

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => scheduler.TriggerNowAsync("job-race", CancellationToken.None))
            .ToArray();

        var runs = await Task.WhenAll(tasks);

        Assert.All(runs, run => Assert.Equal(CompactionStatus.Succeeded, run.Status));
        Assert.Equal(1, observedMax);
    }

    [Fact]
    public async Task Cancellation_MarksRunCancelled()
    {
        var source = new InMemoryEventLog();
        await source.AppendAsync(MakeEvent("v1"), CancellationToken.None);
        var target = new InMemoryEventLog();

        var migrator = new ObservingMigrator(
            onEnter: () => { },
            onExit: () => { },
            dwell: TimeSpan.FromSeconds(5),
            honorCancellation: true);

        await using var scheduler = MakeScheduler(source, target, migrator: migrator);
        scheduler.Schedule(new CompactionJobDefinition("job-cancel", "src", "tgt", TimeSpan.FromHours(1), "v1"));

        using var cts = new CancellationTokenSource();
        var triggerTask = scheduler.TriggerNowAsync("job-cancel", cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        var run = await triggerTask;
        Assert.Equal(CompactionStatus.Cancelled, run.Status);
    }

    [Fact]
    public async Task Exception_DuringMigration_MarksRunFailed()
    {
        var source = new InMemoryEventLog();
        var target = new InMemoryEventLog();
        var migrator = new ThrowingMigrator();

        await using var scheduler = MakeScheduler(source, target, migrator: migrator);
        scheduler.Schedule(new CompactionJobDefinition("job-fail", "src", "tgt", TimeSpan.FromHours(1), "v1"));

        var run = await scheduler.TriggerNowAsync("job-fail", CancellationToken.None);

        Assert.Equal(CompactionStatus.Failed, run.Status);
        Assert.NotNull(run.ErrorMessage);
        Assert.Contains("boom", run.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Migrator stub that observes enter/exit plus an optional dwell, used to assert serialization and cancellation.</summary>
    private sealed class ObservingMigrator : CopyTransformMigrator
    {
        private readonly Action _onEnter;
        private readonly Action _onExit;
        private readonly TimeSpan _dwell;
        private readonly bool _honorCancellation;

        public ObservingMigrator(Action onEnter, Action onExit, TimeSpan dwell, bool honorCancellation = false)
        {
            _onEnter = onEnter;
            _onExit = onExit;
            _dwell = dwell;
            _honorCancellation = honorCancellation;
        }

        public override async Task<MigrationResult> MigrateAsync(
            IEventLog sourceLog,
            IEventLog targetLog,
            LensGraph lensGraph,
            string targetSchemaVersion,
            CancellationToken ct)
        {
            _onEnter();
            try
            {
                if (_honorCancellation)
                {
                    await Task.Delay(_dwell, ct).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(_dwell, CancellationToken.None).ConfigureAwait(false);
                }
                return new MigrationResult(0, 0, 0, Array.Empty<string>());
            }
            finally
            {
                _onExit();
            }
        }
    }

    private sealed class ThrowingMigrator : CopyTransformMigrator
    {
        public override Task<MigrationResult> MigrateAsync(
            IEventLog sourceLog,
            IEventLog targetLog,
            LensGraph lensGraph,
            string targetSchemaVersion,
            CancellationToken ct)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
