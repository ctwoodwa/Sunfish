using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Extensions;
using Sunfish.Foundation.Macaroons;
using Sunfish.Foundation.PolicyEvaluator;
using Xunit;

namespace Sunfish.Foundation.Tests.Extensions;

/// <summary>
/// Exercises the <see cref="SunfishDecentralizationExtensions.AddSunfishDecentralization"/> DI
/// extension — always-on primitives, dev-key-material gating, the startup warning service, and
/// the <see cref="DecentralizationOptions.PolicyModel"/> callback.
/// </summary>
public class AddSunfishDecentralizationTests
{
    private static SunfishBuilder BuildBase() => new ServiceCollection().AddSunfish();

    private static ServiceProvider BuildProvider(bool enableDevKeys = false, Action<PolicyModelBuilder>? policy = null)
    {
        var builder = BuildBase();
        builder.AddSunfishDecentralization(o =>
        {
            o.EnableDevKeyMaterial = enableDevKeys;
            o.PolicyModel = policy;
        });
        // Supply a minimal logger factory so resolving ILogger<T> works without the hosting
        // package's default configuration.
        builder.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void Register_ResolvesIOperationVerifier()
    {
        using var provider = BuildProvider();
        var verifier = provider.GetRequiredService<IOperationVerifier>();
        Assert.IsType<Ed25519Verifier>(verifier);
    }

    [Fact]
    public void Register_ResolvesICapabilityGraph()
    {
        using var provider = BuildProvider();
        var graph = provider.GetRequiredService<ICapabilityGraph>();
        Assert.IsType<InMemoryCapabilityGraph>(graph);
    }

    [Fact]
    public void Register_ResolvesIPermissionEvaluator()
    {
        using var provider = BuildProvider();
        var evaluator = provider.GetRequiredService<IPermissionEvaluator>();
        Assert.IsType<ReBACPolicyEvaluator>(evaluator);
    }

    [Fact]
    public void Register_ResolvesIMacaroonVerifier_OnlyWithDevKeys()
    {
        using (var noDev = BuildProvider(enableDevKeys: false))
        {
            Assert.Null(noDev.GetService<IMacaroonVerifier>());
            Assert.Null(noDev.GetService<IMacaroonIssuer>());
            Assert.Null(noDev.GetService<IRootKeyStore>());
        }

        using var dev = BuildProvider(enableDevKeys: true);
        var verifier = dev.GetRequiredService<IMacaroonVerifier>();
        var issuer = dev.GetRequiredService<IMacaroonIssuer>();
        Assert.IsType<DefaultMacaroonVerifier>(verifier);
        Assert.IsType<DefaultMacaroonIssuer>(issuer);
    }

    [Fact]
    public void Register_WithoutDevKeys_DoesNotRegisterDevKeyStore()
    {
        using var provider = BuildProvider(enableDevKeys: false);
        Assert.Null(provider.GetService<DevKeyStore>());
    }

    [Fact]
    public void Register_WithDevKeys_RegistersDevKeyStore()
    {
        using var provider = BuildProvider(enableDevKeys: true);
        var store = provider.GetRequiredService<DevKeyStore>();
        Assert.NotNull(store);
    }

    [Fact]
    public async Task Register_WithDevKeys_EmitsStartupWarning()
    {
        // NSubstitute cannot proxy ILogger<Internal> for a strong-named abstractions assembly
        // (Castle.DynamicProxy requires [InternalsVisibleTo("DynamicProxyGenAssembly2", ...)] on
        // the target assembly for that). A hand-rolled recording logger is smaller, faster, and
        // has no reflection-proxy dependency at all.
        var recorder = new RecordingLoggerProvider();

        var builder = BuildBase();
        builder.AddSunfishDecentralization(o => o.EnableDevKeyMaterial = true);
        builder.Services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddProvider(recorder);
            b.SetMinimumLevel(LogLevel.Trace);
        });

        using var provider = builder.Services.BuildServiceProvider();

        var hosted = provider.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<SunfishDecentralizationExtensions.DevKeyMaterialWarningService>()
            .Single();

        await hosted.StartAsync(CancellationToken.None);

        var warning = Assert.Single(recorder.Records, r => r.Level == LogLevel.Warning);
        Assert.Contains("DEV KEY MATERIAL ACTIVE", warning.Message);
    }

    /// <summary>Minimal <see cref="ILoggerProvider"/> that captures every log call in memory.</summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<(string Category, LogLevel Level, string Message)> Records { get; } = new();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Records);

        public void Dispose() { }

        private sealed class RecordingLogger : ILogger
        {
            private readonly string _category;
            private readonly List<(string, LogLevel, string)> _records;

            public RecordingLogger(string category, List<(string, LogLevel, string)> records)
            {
                _category = category;
                _records = records;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (_records)
                {
                    _records.Add((_category, logLevel, formatter(state, exception)));
                }
            }
        }
    }

    [Fact]
    public void Register_InvokesPolicyModelCallback()
    {
        using var provider = BuildProvider(policy: mb => mb.Type("widget"));
        var model = provider.GetRequiredService<PolicyModel>();
        Assert.True(model.Types.ContainsKey("widget"));
    }

    [Fact]
    public void Register_RegisteredPolicyModelIsSingleton()
    {
        using var provider = BuildProvider(policy: mb => mb.Type("widget"));
        var first = provider.GetRequiredService<PolicyModel>();
        var second = provider.GetRequiredService<PolicyModel>();
        Assert.Same(first, second);
    }
}
