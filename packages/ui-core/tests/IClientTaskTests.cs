using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UICore.Tests;

public class IClientTaskTests
{
    [Fact]
    public async Task ClientTask_FromResult_ProducesExpectedMessage()
    {
        var task = ClientTask<string>.FromResult("hello");

        var message = await task.ExecuteAsync();

        Assert.Equal("hello", message);
    }

    [Fact]
    public async Task ClientTask_None_ProducesDefault()
    {
        var stringTask = ClientTask<string>.None();
        var intTask = ClientTask<int>.None();

        var stringMessage = await stringTask.ExecuteAsync();
        var intMessage = await intTask.ExecuteAsync();

        Assert.Null(stringMessage);
        Assert.Equal(0, intMessage);
    }

    [Fact]
    public async Task ClientTask_WithFactory_InvokesLazily()
    {
        var invocations = 0;
        var task = new ClientTask<int>(_ =>
        {
            invocations++;
            return ValueTask.FromResult(42);
        });

        Assert.Equal(0, invocations);

        var first = await task.ExecuteAsync();
        var second = await task.ExecuteAsync();

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(2, invocations);
    }
}
