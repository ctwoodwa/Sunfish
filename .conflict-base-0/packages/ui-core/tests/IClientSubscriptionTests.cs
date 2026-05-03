using System.Runtime.CompilerServices;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UICore.Tests;

public class IClientSubscriptionTests
{
    [Fact]
    public async Task ClientSubscription_EnumeratesAllMessages()
    {
        var expected = new[] { "a", "b", "c" };
        var subscription = new ClientSubscription<string>(ct => ProduceAsync(expected, ct));

        var received = new List<string>();
        await foreach (var message in subscription.SubscribeAsync())
        {
            received.Add(message);
        }

        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task ClientSubscription_HonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var subscription = new ClientSubscription<int>(InfiniteAsync);

        var received = new List<int>();
        var op = async () =>
        {
            await foreach (var message in subscription.SubscribeAsync(cts.Token))
            {
                received.Add(message);
                if (received.Count >= 3)
                {
                    cts.Cancel();
                }
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(op);
        Assert.Equal(3, received.Count);
    }

    [Fact]
    public async Task ClientSubscription_EmptyStream_CompletesImmediately()
    {
        var subscription = new ClientSubscription<int>(ct => ProduceAsync(Array.Empty<int>(), ct));

        var received = new List<int>();
        await foreach (var message in subscription.SubscribeAsync())
        {
            received.Add(message);
        }

        Assert.Empty(received);
    }

    private static async IAsyncEnumerable<T> ProduceAsync<T>(
        IEnumerable<T> items,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<int> InfiniteAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var i = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            yield return i++;
            await Task.Yield();
        }
    }
}
