using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sunfish.Anchor.Services;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// Wave 6.3.F follow-up: verify that the manual IHostedService pump used by
/// <c>App.CreateWindow</c> actually drives <c>StartAsync</c> /
/// <c>StopAsync</c> on every registered hosted service. MAUI's MauiApp does
/// not implement IHost, so without this helper <c>AddHostedService&lt;T&gt;()</c>
/// on a MauiAppBuilder would register-but-never-run the service.
/// </summary>
public sealed class MauiHostedServiceLifetimeTests
{
    [Fact]
    public async Task StartAllAsync_invokes_StartAsync_on_every_registered_hosted_service()
    {
        var probeA = new RecordingHostedService("A");
        var probeB = new RecordingHostedService("B");
        var services = new ServiceCollection();
        services.AddSingleton<IHostedService>(probeA);
        services.AddSingleton<IHostedService>(probeB);
        using var provider = services.BuildServiceProvider();

        await MauiHostedServiceLifetime.StartAllAsync(provider, CancellationToken.None);

        Assert.Equal(1, probeA.StartCallCount);
        Assert.Equal(1, probeB.StartCallCount);
    }

    [Fact]
    public async Task StartAllAsync_invokes_in_registration_order()
    {
        var order = new List<string>();
        var probeA = new RecordingHostedService("A", onStart: n => order.Add(n));
        var probeB = new RecordingHostedService("B", onStart: n => order.Add(n));
        var services = new ServiceCollection();
        services.AddSingleton<IHostedService>(probeA);
        services.AddSingleton<IHostedService>(probeB);
        using var provider = services.BuildServiceProvider();

        await MauiHostedServiceLifetime.StartAllAsync(provider, CancellationToken.None);

        Assert.Equal(new[] { "A", "B" }, order);
    }

    [Fact]
    public async Task StopAllAsync_invokes_StopAsync_on_every_registered_hosted_service()
    {
        var probeA = new RecordingHostedService("A");
        var probeB = new RecordingHostedService("B");
        var services = new ServiceCollection();
        services.AddSingleton<IHostedService>(probeA);
        services.AddSingleton<IHostedService>(probeB);
        using var provider = services.BuildServiceProvider();

        await MauiHostedServiceLifetime.StopAllAsync(provider, CancellationToken.None);

        Assert.Equal(1, probeA.StopCallCount);
        Assert.Equal(1, probeB.StopCallCount);
    }

    [Fact]
    public async Task StopAllAsync_invokes_in_reverse_registration_order()
    {
        var order = new List<string>();
        var probeA = new RecordingHostedService("A", onStop: n => order.Add(n));
        var probeB = new RecordingHostedService("B", onStop: n => order.Add(n));
        var services = new ServiceCollection();
        services.AddSingleton<IHostedService>(probeA);
        services.AddSingleton<IHostedService>(probeB);
        using var provider = services.BuildServiceProvider();

        await MauiHostedServiceLifetime.StopAllAsync(provider, CancellationToken.None);

        Assert.Equal(new[] { "B", "A" }, order);
    }

    [Fact]
    public async Task StartAllAsync_propagates_exception_from_hosted_service()
    {
        var bad = new RecordingHostedService("boom", throwOnStart: true);
        var services = new ServiceCollection();
        services.AddSingleton<IHostedService>(bad);
        using var provider = services.BuildServiceProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => MauiHostedServiceLifetime.StartAllAsync(provider, CancellationToken.None));
    }

    [Fact]
    public async Task StopAllAsync_swallows_exceptions_and_continues()
    {
        var order = new List<string>();
        var bad = new RecordingHostedService("bad", onStop: n => order.Add(n), throwOnStop: true);
        var good = new RecordingHostedService("good", onStop: n => order.Add(n));
        var services = new ServiceCollection();
        services.AddSingleton<IHostedService>(bad);
        services.AddSingleton<IHostedService>(good);
        using var provider = services.BuildServiceProvider();

        // Reverse order: good runs first (registered last), then bad.
        await MauiHostedServiceLifetime.StopAllAsync(provider, CancellationToken.None);

        Assert.Equal(new[] { "good", "bad" }, order);
        Assert.Equal(1, bad.StopCallCount);
        Assert.Equal(1, good.StopCallCount);
    }

    [Fact]
    public async Task StartAllAsync_no_hosted_services_is_noop()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        // Should not throw when nothing is registered.
        await MauiHostedServiceLifetime.StartAllAsync(provider, CancellationToken.None);
        await MauiHostedServiceLifetime.StopAllAsync(provider, CancellationToken.None);
    }

    [Fact]
    public async Task StartAllAsync_throws_on_null_provider()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => MauiHostedServiceLifetime.StartAllAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task StopAllAsync_throws_on_null_provider()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => MauiHostedServiceLifetime.StopAllAsync(null!, CancellationToken.None));
    }

    private sealed class RecordingHostedService : IHostedService
    {
        private readonly string _name;
        private readonly Action<string>? _onStart;
        private readonly Action<string>? _onStop;
        private readonly bool _throwOnStart;
        private readonly bool _throwOnStop;

        public RecordingHostedService(
            string name,
            Action<string>? onStart = null,
            Action<string>? onStop = null,
            bool throwOnStart = false,
            bool throwOnStop = false)
        {
            _name = name;
            _onStart = onStart;
            _onStop = onStop;
            _throwOnStart = throwOnStart;
            _throwOnStop = throwOnStop;
        }

        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCallCount++;
            _onStart?.Invoke(_name);
            if (_throwOnStart)
            {
                throw new InvalidOperationException($"{_name} refused to start");
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCallCount++;
            _onStop?.Invoke(_name);
            if (_throwOnStop)
            {
                throw new InvalidOperationException($"{_name} refused to stop");
            }
            return Task.CompletedTask;
        }
    }
}
