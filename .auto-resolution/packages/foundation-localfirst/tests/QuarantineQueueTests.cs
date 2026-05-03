using System.Collections;
using System.Linq;
using Sunfish.Foundation.LocalFirst.Quarantine;
using Sunfish.Kernel.Events;

namespace Sunfish.Foundation.LocalFirst.Tests;

public enum QuarantineBackend
{
    InMemory,
    EventLogBacked,
}

public sealed class QuarantineQueueTests
{
    [Theory]
    [ClassData(typeof(QuarantineQueueBackends))]
    public async Task Enqueue_returns_non_empty_guid_and_pending_status(QuarantineBackend backend)
    {
        var queue = Create(backend);
        var id = await queue.EnqueueAsync(NewEntry("lease.created"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        var record = await queue.GetAsync(id, CancellationToken.None);
        Assert.NotNull(record);
        Assert.Equal(QuarantineStatus.Pending, record!.Status);
        Assert.Equal("alice", record.StatusChangedByActor);
    }

    [Theory]
    [ClassData(typeof(QuarantineQueueBackends))]
    public async Task Promote_transitions_pending_to_accepted(QuarantineBackend backend)
    {
        var queue = Create(backend);
        var id = await queue.EnqueueAsync(NewEntry("lease.created"), CancellationToken.None);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await queue.PromoteAsync(id, actor: "reviewer-1", CancellationToken.None);

        var record = await queue.GetAsync(id, CancellationToken.None);
        Assert.NotNull(record);
        Assert.Equal(QuarantineStatus.Accepted, record!.Status);
        Assert.Equal("reviewer-1", record.StatusChangedByActor);
        Assert.Null(record.RejectionReason);
        Assert.True(record.StatusChangedAt >= before);
    }

    [Theory]
    [ClassData(typeof(QuarantineQueueBackends))]
    public async Task Reject_transitions_pending_to_rejected_with_reason(QuarantineBackend backend)
    {
        var queue = Create(backend);
        var id = await queue.EnqueueAsync(NewEntry("lease.created"), CancellationToken.None);

        await queue.RejectAsync(id, actor: "reviewer-2", reason: "policy violation", CancellationToken.None);

        var record = await queue.GetAsync(id, CancellationToken.None);
        Assert.NotNull(record);
        Assert.Equal(QuarantineStatus.Rejected, record!.Status);
        Assert.Equal("reviewer-2", record.StatusChangedByActor);
        Assert.Equal("policy violation", record.RejectionReason);
    }

    [Theory]
    [ClassData(typeof(QuarantineQueueBackends))]
    public async Task Promote_of_non_pending_throws(QuarantineBackend backend)
    {
        var queue = Create(backend);
        var id = await queue.EnqueueAsync(NewEntry("lease.created"), CancellationToken.None);
        await queue.PromoteAsync(id, "reviewer-1", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => queue.PromoteAsync(id, "reviewer-1", CancellationToken.None));
    }

    [Theory]
    [ClassData(typeof(QuarantineQueueBackends))]
    public async Task Reject_of_non_pending_throws(QuarantineBackend backend)
    {
        var queue = Create(backend);
        var id = await queue.EnqueueAsync(NewEntry("lease.created"), CancellationToken.None);
        await queue.RejectAsync(id, "reviewer-1", "nope", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => queue.RejectAsync(id, "reviewer-1", "nope again", CancellationToken.None));
    }

    [Theory]
    [ClassData(typeof(QuarantineQueueBackends))]
    public async Task Get_for_unknown_id_returns_null(QuarantineBackend backend)
    {
        var queue = Create(backend);
        var record = await queue.GetAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(record);
    }

    [Theory]
    [ClassData(typeof(QuarantineQueueBackends))]
    public async Task ReadByStatus_filters_by_status(QuarantineBackend backend)
    {
        var queue = Create(backend);
        var a = await queue.EnqueueAsync(NewEntry("a"), CancellationToken.None);
        var b = await queue.EnqueueAsync(NewEntry("b"), CancellationToken.None);
        var c = await queue.EnqueueAsync(NewEntry("c"), CancellationToken.None);
        await queue.PromoteAsync(a, "reviewer", CancellationToken.None);
        await queue.RejectAsync(b, "reviewer", "bad", CancellationToken.None);

        var pending = await CollectAsync(queue.ReadByStatusAsync(QuarantineStatus.Pending, CancellationToken.None));
        var accepted = await CollectAsync(queue.ReadByStatusAsync(QuarantineStatus.Accepted, CancellationToken.None));
        var rejected = await CollectAsync(queue.ReadByStatusAsync(QuarantineStatus.Rejected, CancellationToken.None));

        Assert.Single(pending);
        Assert.Equal(c, pending[0].Id);
        Assert.Single(accepted);
        Assert.Equal(a, accepted[0].Id);
        Assert.Single(rejected);
        Assert.Equal(b, rejected[0].Id);
    }

    [Theory]
    [ClassData(typeof(QuarantineQueueBackends))]
    public async Task Concurrent_enqueues_produce_unique_guids(QuarantineBackend backend)
    {
        var queue = Create(backend);
        const int count = 20;
        var tasks = Enumerable.Range(0, count)
            .Select(i => queue.EnqueueAsync(NewEntry($"k{i}"), CancellationToken.None))
            .ToArray();

        var ids = await Task.WhenAll(tasks);

        Assert.Equal(count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id));
    }

    [Fact]
    public async Task EventLogBacked_rehydrates_from_replay()
    {
        var log = new InMemoryEventLog();
        var queue1 = new EventLogBackedQuarantineQueue(log);

        var promoteId = await queue1.EnqueueAsync(NewEntry("promote-me"), CancellationToken.None);
        var rejectId = await queue1.EnqueueAsync(NewEntry("reject-me"), CancellationToken.None);
        var pendingId = await queue1.EnqueueAsync(NewEntry("leave-pending"), CancellationToken.None);
        await queue1.PromoteAsync(promoteId, "reviewer", CancellationToken.None);
        await queue1.RejectAsync(rejectId, "reviewer", "nope", CancellationToken.None);

        var queue2 = new EventLogBackedQuarantineQueue(log);

        var promoted = await queue2.GetAsync(promoteId, CancellationToken.None);
        var rejected = await queue2.GetAsync(rejectId, CancellationToken.None);
        var pending = await queue2.GetAsync(pendingId, CancellationToken.None);

        Assert.NotNull(promoted);
        Assert.Equal(QuarantineStatus.Accepted, promoted!.Status);
        Assert.Equal("reviewer", promoted.StatusChangedByActor);

        Assert.NotNull(rejected);
        Assert.Equal(QuarantineStatus.Rejected, rejected!.Status);
        Assert.Equal("nope", rejected.RejectionReason);

        Assert.NotNull(pending);
        Assert.Equal(QuarantineStatus.Pending, pending!.Status);
        Assert.Equal("payload-leave-pending", Encoding.UTF8.GetString(pending.Entry.Payload.Span));
    }

    private static IQuarantineQueue Create(QuarantineBackend backend) => backend switch
    {
        QuarantineBackend.InMemory => new InMemoryQuarantineQueue(),
        QuarantineBackend.EventLogBacked => new EventLogBackedQuarantineQueue(new InMemoryEventLog()),
        _ => throw new ArgumentOutOfRangeException(nameof(backend)),
    };

    private static QuarantineEntry NewEntry(string kind) => new(
        Kind: kind,
        Stream: $"stream-{kind}",
        Payload: Encoding.UTF8.GetBytes($"payload-{kind}"),
        QueuedAt: DateTimeOffset.UtcNow,
        QueuedByActor: "alice");

    private static async Task<List<QuarantineRecord>> CollectAsync(IAsyncEnumerable<QuarantineRecord> source)
    {
        var list = new List<QuarantineRecord>();
        await foreach (var record in source.ConfigureAwait(false))
        {
            list.Add(record);
        }
        return list;
    }
}

public sealed class QuarantineQueueBackends : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[] { QuarantineBackend.InMemory };
        yield return new object[] { QuarantineBackend.EventLogBacked };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
