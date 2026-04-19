using System.Runtime.CompilerServices;
using Bunit;
using Microsoft.AspNetCore.Components;
using Sunfish.Components.Blazor.Async;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Components.Blazor.Tests;

public class InMemoryClientTaskDispatcherTests : BunitContext
{
    [Fact]
    public async Task DispatchAsync_Task_InvokesCallbackWithMessage()
    {
        var dispatcher = new InMemoryClientTaskDispatcher();
        var receiver = new MessageReceiver<string>();
        var callback = EventCallback.Factory.Create<string>(receiver, receiver.Handle);

        var task = ClientTask<string>.FromResult("hello");

        await dispatcher.DispatchAsync(task, callback);

        Assert.Equal(new[] { "hello" }, receiver.Messages);
    }

    [Fact]
    public async Task DispatchAsync_Subscription_InvokesCallbackPerMessage()
    {
        var dispatcher = new InMemoryClientTaskDispatcher();
        var receiver = new MessageReceiver<int>();
        var callback = EventCallback.Factory.Create<int>(receiver, receiver.Handle);

        var subscription = new ClientSubscription<int>(ProduceAsync);

        await dispatcher.DispatchAsync(subscription, callback);

        Assert.Equal(new[] { 1, 2, 3 }, receiver.Messages);
    }

    private static async IAsyncEnumerable<int> ProduceAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var value in new[] { 1, 2, 3 })
        {
            ct.ThrowIfCancellationRequested();
            yield return value;
            await Task.Yield();
        }
    }

    private sealed class MessageReceiver<T>
    {
        public List<T> Messages { get; } = new();

        public void Handle(T message) => Messages.Add(message);
    }
}
