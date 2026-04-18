using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Sunfish.Federation.BlobReplication.Kubo;
using Xunit;

namespace Sunfish.Federation.BlobReplication.Tests;

/// <summary>
/// Shared Testcontainers fixture that boots a single <c>ipfs/kubo:v0.28.0</c> container for the
/// lifetime of the test run. Tests that depend on it belong to the <c>"Kubo"</c> collection so
/// xUnit instantiates exactly one container across the whole assembly.
/// </summary>
/// <remarks>
/// <para>
/// Kubo's HTTP RPC is POST-only for every endpoint, including <c>/api/v0/version</c> which we use
/// as the readiness probe. Using GET here returns 405 and the container never becomes ready.
/// </para>
/// <para>
/// On environments where Testcontainers' Ryuk reaper misbehaves (some Podman-on-Windows setups),
/// set the environment variable <c>TESTCONTAINERS_RYUK_DISABLED=true</c> before running tests.
/// This fixture also sets it defensively via a static constructor.
/// </para>
/// </remarks>
public sealed class KuboSingleNodeFixture : IAsyncLifetime
{
    static KuboSingleNodeFixture()
    {
        // Defensive — Podman on Windows sometimes throttles the Ryuk reaper pipe.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED")))
        {
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
        }
    }

    private IContainer? _container;

    /// <summary>Base URL of the Kubo RPC API (e.g. <c>http://127.0.0.1:{hostPort}/</c>).</summary>
    public Uri RpcEndpoint { get; private set; } = new("http://127.0.0.1:5001/");

    /// <summary>An <see cref="HttpClient"/> pre-configured with <see cref="RpcEndpoint"/>.</summary>
    public HttpClient HttpClient { get; private set; } = null!;

    /// <summary>A ready <see cref="IKuboHttpClient"/> for tests to use directly.</summary>
    public IKuboHttpClient KuboClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder()
            .WithImage("ipfs/kubo:v0.28.0")
            .WithPortBinding(5001, assignRandomHostPort: true)
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithEnvironment("IPFS_PROFILE", "test")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(req => req
                    .ForPath("/api/v0/version")
                    .ForPort(5001)
                    .WithMethod(HttpMethod.Post)))
            .Build();

        await _container.StartAsync().ConfigureAwait(false);

        var hostPort = _container.GetMappedPublicPort(5001);
        RpcEndpoint = new Uri($"http://127.0.0.1:{hostPort}/");

        HttpClient = new HttpClient { BaseAddress = RpcEndpoint };
        KuboClient = new KuboHttpClient(HttpClient);
    }

    public async Task DisposeAsync()
    {
        HttpClient?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>xUnit collection marker that binds tests to the single shared Kubo container.</summary>
[CollectionDefinition("Kubo")]
public sealed class KuboCollection : ICollectionFixture<KuboSingleNodeFixture>
{
}
