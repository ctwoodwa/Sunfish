using Microsoft.Extensions.Options;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Events;

namespace Sunfish.Kernel.EventBus.Tests;

/// <summary>
/// Parameterized contract tests that run against every <see cref="IEventLog"/> implementation.
/// Each test is provided an <see cref="IEventLogFactory"/> via <see cref="EventLogFactories"/> so
/// the same assertion runs against the in-memory log and the file-backed log, guaranteeing
/// behavioural parity (paper §2.5 / §8).
/// </summary>
public class EventLogContractTests
{
    public static IEnumerable<object[]> EventLogFactories()
    {
        yield return new object[] { new InMemoryFactory() };
        yield return new object[] { new FileBackedFactory() };
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task AppendAsync_AssignsSequentialSequencesStartingAtOne(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        var s1 = await log.AppendAsync(BuildEvent("A"), default);
        var s2 = await log.AppendAsync(BuildEvent("B"), default);
        var s3 = await log.AppendAsync(BuildEvent("C"), default);

        Assert.Equal(1UL, s1);
        Assert.Equal(2UL, s2);
        Assert.Equal(3UL, s3);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task CurrentSequence_IsZeroOnEmptyLog(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        Assert.Equal(0UL, handle.Log.CurrentSequence);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task CurrentSequence_UpdatesAtomicallyWithAppend(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        await log.AppendAsync(BuildEvent("A"), default);
        Assert.Equal(1UL, log.CurrentSequence);

        await log.AppendAsync(BuildEvent("B"), default);
        Assert.Equal(2UL, log.CurrentSequence);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ReadAfterAsync_ZeroYieldsAllEvents(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        await log.AppendAsync(BuildEvent("A"), default);
        await log.AppendAsync(BuildEvent("B"), default);
        await log.AppendAsync(BuildEvent("C"), default);

        var entries = await ToListAsync(log.ReadAfterAsync(0, default));
        Assert.Equal(3, entries.Count);
        Assert.Equal(new ulong[] { 1, 2, 3 }, entries.Select(e => e.Sequence));
        Assert.Equal(new[] { "A", "B", "C" }, entries.Select(e => (string)e.Event.Payload["tag"]!));
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ReadAfterAsync_NYieldsEventsAfterN(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        for (var i = 0; i < 5; i++)
        {
            await log.AppendAsync(BuildEvent($"e{i}"), default);
        }

        var afterTwo = await ToListAsync(log.ReadAfterAsync(2, default));
        Assert.Equal(new ulong[] { 3, 4, 5 }, afterTwo.Select(e => e.Sequence));
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ReadAfterAsync_PastEndYieldsEmpty(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        await log.AppendAsync(BuildEvent("A"), default);
        var entries = await ToListAsync(log.ReadAfterAsync(100, default));
        Assert.Empty(entries);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ReadRangeAsync_InclusiveBoundsAreRespected(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        for (var i = 0; i < 5; i++)
        {
            await log.AppendAsync(BuildEvent($"e{i}"), default);
        }

        var range = await ToListAsync(log.ReadRangeAsync(2, 4, default));
        Assert.Equal(new ulong[] { 2, 3, 4 }, range.Select(e => e.Sequence));
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ReadRangeAsync_SinglePoint(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        for (var i = 0; i < 5; i++)
        {
            await log.AppendAsync(BuildEvent($"e{i}"), default);
        }

        var range = await ToListAsync(log.ReadRangeAsync(3, 3, default));
        Assert.Single(range);
        Assert.Equal(3UL, range[0].Sequence);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ConcurrentAppends_ProduceContiguousSequenceNumbers(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;
        const int n = 50;

        // Fire n appends in parallel and verify the set of returned sequence numbers is exactly
        // {1..n} with no gaps and no duplicates — that is the appending-serialization guarantee.
        var tasks = Enumerable.Range(0, n)
            .Select(i => log.AppendAsync(BuildEvent($"e{i}"), default))
            .ToArray();

        var seqs = await Task.WhenAll(tasks);
        var ordered = seqs.OrderBy(s => s).ToArray();
        Assert.Equal(Enumerable.Range(1, n).Select(i => (ulong)i), ordered);
        Assert.Equal((ulong)n, log.CurrentSequence);

        // Read back and confirm we have n distinct events.
        var entries = await ToListAsync(log.ReadAfterAsync(0, default));
        Assert.Equal(n, entries.Count);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task WriteSnapshot_ReadLatestSnapshot_Roundtrips(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var snap = new Snapshot(
            AggregateId: "property:acme/1",
            EpochId: "epoch-0",
            SchemaVersion: "v1",
            LastEventSeq: 42,
            Payload: payload,
            CreatedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        await log.WriteSnapshotAsync(snap, default);
        var loaded = await log.ReadLatestSnapshotAsync("property:acme/1", "epoch-0", "v1", default);

        Assert.NotNull(loaded);
        Assert.Equal(snap.AggregateId, loaded!.AggregateId);
        Assert.Equal(snap.EpochId, loaded.EpochId);
        Assert.Equal(snap.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(snap.LastEventSeq, loaded.LastEventSeq);
        Assert.Equal(snap.Payload, loaded.Payload);
        Assert.Equal(snap.CreatedAt, loaded.CreatedAt);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ReadLatestSnapshot_ReturnsNewestByCreatedAt(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        var older = new Snapshot("agg-1", "epoch-0", "v1", 10, new byte[] { 1 },
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = new Snapshot("agg-1", "epoch-0", "v1", 20, new byte[] { 2 },
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

        // Write out-of-order to prove ordering is by CreatedAt, not by write time.
        await log.WriteSnapshotAsync(newer, default);
        await log.WriteSnapshotAsync(older, default);

        var loaded = await log.ReadLatestSnapshotAsync("agg-1", "epoch-0", "v1", default);
        Assert.NotNull(loaded);
        Assert.Equal(20UL, loaded!.LastEventSeq);
        Assert.Equal(new byte[] { 2 }, loaded.Payload);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ReadLatestSnapshot_IsolatedByAggregateEpochSchemaTuple(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        // Three snapshots with every combination of (agg, epoch, schema) that differs by one axis.
        var a = new Snapshot("agg-A", "epoch-0", "v1", 1, new byte[] { 0xA },
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var b = new Snapshot("agg-B", "epoch-0", "v1", 2, new byte[] { 0xB },
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var c = new Snapshot("agg-A", "epoch-1", "v1", 3, new byte[] { 0xC },
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var d = new Snapshot("agg-A", "epoch-0", "v2", 4, new byte[] { 0xD },
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        await log.WriteSnapshotAsync(a, default);
        await log.WriteSnapshotAsync(b, default);
        await log.WriteSnapshotAsync(c, default);
        await log.WriteSnapshotAsync(d, default);

        Assert.Equal(new byte[] { 0xA }, (await log.ReadLatestSnapshotAsync("agg-A", "epoch-0", "v1", default))!.Payload);
        Assert.Equal(new byte[] { 0xB }, (await log.ReadLatestSnapshotAsync("agg-B", "epoch-0", "v1", default))!.Payload);
        Assert.Equal(new byte[] { 0xC }, (await log.ReadLatestSnapshotAsync("agg-A", "epoch-1", "v1", default))!.Payload);
        Assert.Equal(new byte[] { 0xD }, (await log.ReadLatestSnapshotAsync("agg-A", "epoch-0", "v2", default))!.Payload);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ReadLatestSnapshot_ReturnsNullWhenNoneExists(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;
        var loaded = await log.ReadLatestSnapshotAsync("missing", "epoch-0", "v1", default);
        Assert.Null(loaded);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task Events_RoundTripPayloadTypes(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        var evt = new KernelEvent(
            Id: EventId.NewId(),
            EntityId: EntityId.Parse("property:acme/42"),
            Kind: "entity.updated",
            OccurredAt: new DateTimeOffset(2026, 4, 22, 12, 30, 0, TimeSpan.Zero),
            Payload: new Dictionary<string, object?>
            {
                ["s"] = "hello",
                ["i"] = 123,
                ["b"] = true,
                ["n"] = null,
                ["d"] = 3.14,
            });

        await log.AppendAsync(evt, default);
        var entries = await ToListAsync(log.ReadAfterAsync(0, default));

        Assert.Single(entries);
        var round = entries[0].Event;
        Assert.Equal(evt.Id, round.Id);
        Assert.Equal(evt.EntityId, round.EntityId);
        Assert.Equal(evt.Kind, round.Kind);
        Assert.Equal(evt.OccurredAt, round.OccurredAt);
        Assert.Equal("hello", (string)round.Payload["s"]!);
        Assert.Equal(123, (int)round.Payload["i"]!);
        Assert.True((bool)round.Payload["b"]!);
        Assert.Null(round.Payload["n"]);
        Assert.Equal(3.14, (double)round.Payload["d"]!);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task ReadRangeAsync_EmptyWhenFromBeyondEnd(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        await log.AppendAsync(BuildEvent("only"), default);
        var entries = await ToListAsync(log.ReadRangeAsync(10, 20, default));
        Assert.Empty(entries);
    }

    [Theory]
    [MemberData(nameof(EventLogFactories))]
    public async Task Snapshot_PayloadBytesArePreservedByteForByte(IEventLogFactory factory)
    {
        await using var handle = factory.Create();
        var log = handle.Log;

        var bytes = new byte[256];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)i;

        var snap = new Snapshot("agg", "epoch-0", "v1", 1, bytes,
            new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        await log.WriteSnapshotAsync(snap, default);

        var loaded = await log.ReadLatestSnapshotAsync("agg", "epoch-0", "v1", default);
        Assert.NotNull(loaded);
        Assert.Equal(bytes, loaded!.Payload);
    }

    // ---- helpers ----

    private static KernelEvent BuildEvent(string tag)
        => new(
            Id: EventId.NewId(),
            EntityId: EntityId.Parse("property:acme/1"),
            Kind: "entity.updated",
            OccurredAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Payload: new Dictionary<string, object?> { ["tag"] = tag });

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var x in source) list.Add(x);
        return list;
    }

    // ---- factories ----

    public interface IEventLogFactory
    {
        EventLogHandle Create();
    }

    public sealed class EventLogHandle : IAsyncDisposable
    {
        public EventLogHandle(IEventLog log, Func<ValueTask> dispose)
        {
            Log = log;
            _dispose = dispose;
        }
        public IEventLog Log { get; }
        private readonly Func<ValueTask> _dispose;
        public ValueTask DisposeAsync() => _dispose();
    }

    private sealed class InMemoryFactory : IEventLogFactory
    {
        public EventLogHandle Create() => new(new InMemoryEventLog(), () => ValueTask.CompletedTask);
        public override string ToString() => "InMemory";
    }

    private sealed class FileBackedFactory : IEventLogFactory
    {
        public EventLogHandle Create()
        {
            var dir = Path.Combine(Path.GetTempPath(), "sunfish-eventlog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var opts = Options.Create(new EventLogOptions { Directory = dir, EpochId = "epoch-0" });
            var log = new FileBackedEventLog(opts);
            return new EventLogHandle(log, async () =>
            {
                await log.DisposeAsync();
                try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
            });
        }
        public override string ToString() => "FileBacked";
    }
}
