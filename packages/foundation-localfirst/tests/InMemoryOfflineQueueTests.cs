using Sunfish.Foundation.LocalFirst;

namespace Sunfish.Foundation.LocalFirst.Tests;

public class InMemoryOfflineQueueTests
{
    [Fact]
    public async Task Enqueue_and_peek_preserve_order()
    {
        var queue = new InMemoryOfflineQueue();
        var op1 = NewOp("a");
        var op2 = NewOp("b");
        var op3 = NewOp("c");

        await queue.EnqueueAsync(op1);
        await queue.EnqueueAsync(op2);
        await queue.EnqueueAsync(op3);

        var pending = await queue.PeekPendingAsync();

        Assert.Equal(new[] { op1.Id, op2.Id, op3.Id }, pending.Select(o => o.Id).ToArray());
        Assert.Equal(3, await queue.CountAsync());
    }

    [Fact]
    public async Task Acknowledge_removes_from_queue()
    {
        var queue = new InMemoryOfflineQueue();
        var op = NewOp("a");
        await queue.EnqueueAsync(op);

        await queue.AcknowledgeAsync(op.Id);

        Assert.Equal(0, await queue.CountAsync());
        Assert.Empty(await queue.PeekPendingAsync());
    }

    [Fact]
    public async Task Enqueue_rejects_duplicate_ids()
    {
        var queue = new InMemoryOfflineQueue();
        var op = NewOp("a");
        await queue.EnqueueAsync(op);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await queue.EnqueueAsync(op));
    }

    [Fact]
    public async Task PeekPendingAsync_caps_at_max()
    {
        var queue = new InMemoryOfflineQueue();
        for (var i = 0; i < 5; i++)
        {
            await queue.EnqueueAsync(NewOp($"k{i}"));
        }

        var pending = await queue.PeekPendingAsync(max: 2);

        Assert.Equal(2, pending.Count);
    }

    private static OfflineOperation NewOp(string kind) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        Payload = Encoding.UTF8.GetBytes(kind),
    };
}
