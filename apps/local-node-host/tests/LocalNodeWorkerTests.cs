using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Kernel.Runtime.DependencyInjection;

namespace Sunfish.LocalNodeHost.Tests;

/// <summary>
/// Integration-style tests for <see cref="LocalNodeWorker"/>. We exercise the
/// worker against a <see cref="HostApplicationBuilder"/>-composed service
/// provider using the real <c>AddSunfishKernelRuntime</c> extension, then
/// start the host and observe lifecycle transitions + log capture.
/// </summary>
public sealed class LocalNodeWorkerTests
{
    /// <summary>
    /// Build a minimal test host that composes the real kernel runtime (plugin
    /// registry + NodeHost) plus the <see cref="LocalNodeWorker"/>. We swap
    /// <see cref="ILoggerFactory"/> with a capture factory so tests can assert
    /// on lifecycle log messages without pulling in a third-party logging
    /// framework.
    /// </summary>
    private static IHost BuildHost(
        IEnumerable<ILocalNodePlugin> plugins,
        CaptureLoggerProvider capture)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
        });

        builder.Services.AddSunfishKernelRuntime();

        // Inject the test plugin set as a concrete IEnumerable<ILocalNodePlugin>.
        foreach (var plugin in plugins)
        {
            builder.Services.AddSingleton(plugin);
        }

        // Capture logger wiring. We deliberately do NOT call AddLogging() from
        // Microsoft.Extensions.Logging so the assembly dependency stays at
        // Logging.Abstractions only — the capture logger types below implement
        // the abstraction directly.
        builder.Services.AddSingleton(capture);
        builder.Services.AddSingleton<ILoggerFactory, CaptureLoggerFactory>();
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(CaptureLogger<>));

        builder.Services.AddHostedService<LocalNodeWorker>();

        return builder.Build();
    }

    [Fact]
    public async Task Worker_starts_and_loads_plugins()
    {
        var capture = new CaptureLoggerProvider();
        var log = new List<string>();
        var plugins = new ILocalNodePlugin[]
        {
            new RecordingPlugin("plugin.a", Array.Empty<string>(), log),
            new RecordingPlugin("plugin.b", new[] { "plugin.a" }, log),
        };

        using var host = BuildHost(plugins, capture);
        var registry = host.Services.GetRequiredService<IPluginRegistry>();
        var nodeHost = host.Services.GetRequiredService<INodeHost>();

        await host.StartAsync();

        // Give ExecuteAsync a moment to schedule past the await points.
        await WaitForStateAsync(nodeHost, NodeState.Running);

        Assert.Equal(2, registry.LoadedPlugins.Count);
        Assert.Contains("load:plugin.a", log);
        Assert.Contains("load:plugin.b", log);

        await host.StopAsync();
    }

    [Fact]
    public async Task Worker_starts_even_with_zero_plugins()
    {
        var capture = new CaptureLoggerProvider();
        using var host = BuildHost(Array.Empty<ILocalNodePlugin>(), capture);
        var registry = host.Services.GetRequiredService<IPluginRegistry>();
        var nodeHost = host.Services.GetRequiredService<INodeHost>();

        await host.StartAsync();
        await WaitForStateAsync(nodeHost, NodeState.Running);

        Assert.Empty(registry.LoadedPlugins);
        Assert.Equal(NodeState.Running, nodeHost.State);

        await host.StopAsync();
    }

    [Fact]
    public async Task Worker_transitions_NodeHost_Stopped_Starting_Running_on_start()
    {
        var capture = new CaptureLoggerProvider();
        using var host = BuildHost(Array.Empty<ILocalNodePlugin>(), capture);
        var nodeHost = host.Services.GetRequiredService<INodeHost>();

        // Before StartAsync is called, the host is freshly Stopped.
        Assert.Equal(NodeState.Stopped, nodeHost.State);

        await host.StartAsync();
        await WaitForStateAsync(nodeHost, NodeState.Running);

        Assert.Equal(NodeState.Running, nodeHost.State);

        await host.StopAsync();
    }

    [Fact]
    public async Task Worker_transitions_NodeHost_Running_Stopping_Stopped_on_cancellation()
    {
        var capture = new CaptureLoggerProvider();
        var log = new List<string>();
        var plugins = new ILocalNodePlugin[]
        {
            new RecordingPlugin("plugin.a", Array.Empty<string>(), log),
        };

        using var host = BuildHost(plugins, capture);
        var registry = host.Services.GetRequiredService<IPluginRegistry>();
        var nodeHost = host.Services.GetRequiredService<INodeHost>();

        await host.StartAsync();
        await WaitForStateAsync(nodeHost, NodeState.Running);

        await host.StopAsync();

        Assert.Equal(NodeState.Stopped, nodeHost.State);
        Assert.Empty(registry.LoadedPlugins); // unloaded
        Assert.Contains("load:plugin.a", log);
        Assert.Contains("unload:plugin.a", log);
    }

    [Fact]
    public async Task Worker_logs_expected_lifecycle_events()
    {
        var capture = new CaptureLoggerProvider();
        using var host = BuildHost(Array.Empty<ILocalNodePlugin>(), capture);
        var nodeHost = host.Services.GetRequiredService<INodeHost>();

        await host.StartAsync();
        await WaitForStateAsync(nodeHost, NodeState.Running);
        await host.StopAsync();

        var messages = capture.Messages.ToList();
        Assert.Contains(messages, m => m.Contains("Sunfish local-node host starting", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("Loaded 0 plugin(s)", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("Sunfish local-node host stopping", StringComparison.Ordinal));
    }

    // ---- Helpers --------------------------------------------------------

    /// <summary>
    /// Spin (with a short delay) until the node host transitions to <paramref name="expected"/>
    /// or a 5s watchdog trips. The worker's ExecuteAsync is asynchronous relative to
    /// <c>IHost.StartAsync</c> — the latter returns once hosted services are scheduled,
    /// not once they finish their boot work — so we need a bounded poll.
    /// </summary>
    private static async Task WaitForStateAsync(INodeHost host, NodeState expected, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (host.State != expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
        Assert.Equal(expected, host.State);
    }

    /// <summary>Recording plugin — appends to a shared log so tests can assert ordering.</summary>
    private sealed class RecordingPlugin : ILocalNodePlugin
    {
        private readonly List<string> _log;

        public RecordingPlugin(string id, IReadOnlyCollection<string> deps, List<string> log)
        {
            Id = id;
            Dependencies = deps;
            _log = log;
        }

        public string Id { get; }
        public string Version => "1.0.0";
        public IReadOnlyCollection<string> Dependencies { get; }

        public Task OnLoadAsync(IPluginContext context, CancellationToken ct)
        {
            lock (_log) { _log.Add($"load:{Id}"); }
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync(CancellationToken ct)
        {
            lock (_log) { _log.Add($"unload:{Id}"); }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Minimal logger implementation that appends every formatted log message to
    /// a shared list. Keeps the test assembly dependency-free beyond
    /// Microsoft.Extensions.Logging.Abstractions.
    /// </summary>
    private sealed class CaptureLoggerProvider
    {
        private readonly List<string> _messages = new();
        private readonly object _gate = new();

        public IReadOnlyList<string> Messages
        {
            get { lock (_gate) { return _messages.ToArray(); } }
        }

        public void Append(string category, LogLevel level, string message)
        {
            lock (_gate)
            {
                _messages.Add(message);
            }
        }
    }

    private sealed class CaptureLoggerFactory : ILoggerFactory
    {
        private readonly CaptureLoggerProvider _provider;
        public CaptureLoggerFactory(CaptureLoggerProvider provider) => _provider = provider;
        public void AddProvider(ILoggerProvider provider) { /* not used by tests */ }
        public ILogger CreateLogger(string categoryName) => new CaptureLoggerImpl(categoryName, _provider);
        public void Dispose() { }
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        private readonly CaptureLoggerImpl _inner;
        public CaptureLogger(CaptureLoggerProvider provider)
            => _inner = new CaptureLoggerImpl(typeof(T).FullName ?? typeof(T).Name, provider);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _inner.Log(logLevel, eventId, state, exception, formatter);
    }

    private sealed class CaptureLoggerImpl : ILogger
    {
        private readonly string _category;
        private readonly CaptureLoggerProvider _provider;
        public CaptureLoggerImpl(string category, CaptureLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter is null) return;
            _provider.Append(_category, logLevel, formatter(state, exception));
        }
    }
}
