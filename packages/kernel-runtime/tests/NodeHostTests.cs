using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Kernel.Runtime.Tests;

public sealed class NodeHostTests
{
    private static NodeHost NewHost()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var registry = new PluginRegistry(services);
        return new NodeHost(registry);
    }

    [Fact]
    public void New_host_is_Stopped()
    {
        var host = NewHost();
        Assert.Equal(NodeState.Stopped, host.State);
    }

    [Fact]
    public async Task Start_transitions_to_Running()
    {
        var host = NewHost();
        await host.StartAsync(CancellationToken.None);
        Assert.Equal(NodeState.Running, host.State);
    }

    [Fact]
    public async Task Stop_after_start_returns_to_Stopped()
    {
        var host = NewHost();
        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
        Assert.Equal(NodeState.Stopped, host.State);
    }

    [Fact]
    public async Task Start_while_running_throws()
    {
        var host = NewHost();
        await host.StartAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Stop_while_stopped_is_noop()
    {
        var host = NewHost();
        // No start — stop from fresh Stopped state completes without throwing.
        await host.StopAsync(CancellationToken.None);
        Assert.Equal(NodeState.Stopped, host.State);
    }

    [Fact]
    public async Task Start_then_stop_then_start_again_works()
    {
        var host = NewHost();
        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
        await host.StartAsync(CancellationToken.None);
        Assert.Equal(NodeState.Running, host.State);
    }

    [Fact]
    public async Task Start_with_cancelled_token_throws_and_returns_to_Stopped()
    {
        var host = NewHost();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.StartAsync(cts.Token));
        Assert.Equal(NodeState.Stopped, host.State);
    }

    [Fact]
    public void Host_exposes_its_plugin_registry()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var registry = new PluginRegistry(services);
        var host = new NodeHost(registry);
        Assert.Same(registry, host.Plugins);
    }
}
