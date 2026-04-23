using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Orchestration;
using Sunfish.Bridge.Services;
using Sunfish.Foundation.Authorization;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Security.Keys;
using Xunit;

namespace Sunfish.Bridge.Tests.Integration.Wave52;

/// <summary>
/// Wave 5.2.E exit-criterion tests. Exercises the full spawn → health →
/// lifecycle → restart-rebuild loop end-to-end against a Bridge SaaS-posture
/// composition wired up directly (no Aspire AppHost) to avoid Podman/Docker
/// and Postgres requirements on the CI agent.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why direct composition, not DistributedApplicationTestingBuilder.</b>
/// The decomposition plan §5.2.E scope calls for exercising
/// <see cref="ITenantProcessSupervisor"/> + <see cref="TenantHealthMonitor"/>
/// + <see cref="TenantLifecycleCoordinator"/> against three tenants. Aspire's
/// <c>DistributedApplicationTestingBuilder</c> boots the full graph — Postgres
/// + Redis + RabbitMQ + DAB + bridge-web — which requires a container runtime
/// the CI agent does not have. The plan's carve-out explicitly permits falling
/// back to a direct composition; we take that path here. <c>HealthCheckTests</c>
/// in this same project keeps a <c>[Fact(Skip=…)]</c> covering the
/// container-runtime path for local operators.
/// </para>
/// <para>
/// <b>Fake ProcessStarter.</b> <see cref="InProcessKestrelProcessStarter"/>
/// stands in for <see cref="SystemDiagnosticsProcessStarter"/>. On each spawn
/// it reads <c>ASPNETCORE_URLS</c> and <c>LocalNode__DataDirectory</c> from the
/// <see cref="ProcessStartInfo"/> environment, starts a lightweight Kestrel
/// web host that serves <c>/health</c> → 200 at that URL, and returns an
/// <see cref="IProcessHandle"/> whose <see cref="IProcessHandle.Kill"/>
/// shuts the web host down. This lets the supervisor's health-probe path
/// (<c>WaitForHealthyAsync</c>) actually observe a 200 and transition the
/// tenant to <c>Running</c> without executing the real local-node-host dll.
/// </para>
/// <para>
/// <b>Three-way isolation proof.</b> The smoke test asserts the three tenants
/// get (a) distinct endpoints (distinct ephemeral ports), (b) distinct data
/// directories (per <see cref="TenantPaths.NodeDataDirectory"/>), and (c) all
/// three reach <see cref="TenantProcessState.Running"/>. That is the Wave 5.2
/// three-way isolation invariant the plan §5.2.E calls for.
/// </para>
/// </remarks>
public sealed class ThreeTenantSmokeTest : IDisposable
{
    private readonly string _dataRoot;

    public ThreeTenantSmokeTest()
    {
        _dataRoot = Path.Combine(
            Path.GetTempPath(), "sunfish-bridge-5.2.e-tests", Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(_dataRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataRoot))
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup — OS may still hold file handles from the
            // Kestrel fakes if a test raced Dispose.
        }
    }

    [Fact]
    public async Task Three_tenants_spawn_and_reach_running_state()
    {
        // Shared InMemory EF root so the test's seeded DbContext and the
        // scoped contexts the supervisor opens see the same data.
        var dbRoot = new InMemoryDatabaseRoot();
        var keystore = new InMemoryKeystore();
        var starter = new InProcessKestrelProcessStarter();

        var host = BuildBridgeHost(dbRoot, keystore, starter);
        await host.StartAsync();
        try
        {
            // Create three tenants and activate them via the registry —
            // SetTeamPublicKeyAsync is what the founder flow will ultimately
            // drive, and it publishes the Pending → Active event the
            // TenantLifecycleCoordinator listens for.
            var tenantIds = new List<Guid>();
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
                foreach (var slug in new[] { "acme", "beta", "contoso" })
                {
                    var t = await registry.CreateAsync(slug, slug, "Team", CancellationToken.None);
                    await registry.SetTeamPublicKeyAsync(t.TenantId, [1, 2, 3, 4, 5], CancellationToken.None);
                    tenantIds.Add(t.TenantId);
                }
            }

            var supervisor = host.Services.GetRequiredService<ITenantProcessSupervisor>();

            // Poll for each tenant to reach Running. Supervisor's
            // WaitForHealthyAsync is fire-and-forget; 5s is generous given
            // the in-process Kestrel fakes respond within milliseconds.
            foreach (var id in tenantIds)
            {
                await WaitForStateAsync(supervisor, id, TenantProcessState.Running, TimeSpan.FromSeconds(5));
            }

            // Three-way isolation assertions.
            var endpoints = host.Services.GetRequiredService<ITenantEndpointRegistry>();
            var snapshot = endpoints.Snapshot();
            Assert.Equal(3, snapshot.Count);

            var endpointUris = snapshot.Values.ToList();
            Assert.Equal(3, endpointUris.Select(u => u.Port).Distinct().Count());

            // Distinct data directories per TenantPaths convention.
            var dataDirs = tenantIds.Select(id => TenantPaths.NodeDataDirectory(_dataRoot, id)).ToList();
            Assert.Equal(3, dataDirs.Distinct().Count());
            foreach (var dir in dataDirs)
            {
                Assert.True(Directory.Exists(dir), $"expected tenant data directory '{dir}' to exist");
            }

            // Each /health endpoint returns 200.
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            foreach (var (id, endpoint) in snapshot)
            {
                var response = await http.GetAsync(new Uri(endpoint, "/health"));
                Assert.True(
                    response.IsSuccessStatusCode,
                    $"tenant {id} /health returned {(int)response.StatusCode}");
            }

            // Three spawns happened — one per tenant — and each received a
            // distinct seed via LocalNode__RootSeedHex (W5.2 stop-work #1).
            Assert.Equal(3, starter.Spawns.Count);
            var seeds = starter.Spawns
                .Select(s => s.StartInfo.EnvironmentVariables["LocalNode__RootSeedHex"])
                .ToList();
            Assert.Equal(3, seeds.Distinct().Count());
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
            starter.Dispose();
        }
    }

    [Fact]
    public async Task AppHostRestart_PreservesTenantStateAndDisk()
    {
        // Shared database + keystore persist across the two host runs to
        // simulate AppHost restart against the same backing state.
        var dbRoot = new InMemoryDatabaseRoot();
        var keystore = new InMemoryKeystore();

        // --- First run: spawn two tenants ---
        List<Guid> tenantIds;
        {
            var starter = new InProcessKestrelProcessStarter();
            var host = BuildBridgeHost(dbRoot, keystore, starter);
            await host.StartAsync();
            try
            {
                tenantIds = new List<Guid>();
                await using (var scope = host.Services.CreateAsyncScope())
                {
                    var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
                    foreach (var slug in new[] { "alpha", "omega" })
                    {
                        var t = await registry.CreateAsync(slug, slug, "Team", CancellationToken.None);
                        await registry.SetTeamPublicKeyAsync(
                            t.TenantId, [9, 8, 7, 6, 5], CancellationToken.None);
                        tenantIds.Add(t.TenantId);
                    }
                }

                var supervisor = host.Services.GetRequiredService<ITenantProcessSupervisor>();
                foreach (var id in tenantIds)
                {
                    await WaitForStateAsync(
                        supervisor, id, TenantProcessState.Running, TimeSpan.FromSeconds(5));
                }

                // Write a sentinel inside each tenant's data dir so the
                // second run can prove on-disk persistence.
                foreach (var id in tenantIds)
                {
                    var dir = TenantPaths.NodeDataDirectory(_dataRoot, id);
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "persist.txt"), id.ToString("D"));
                }
            }
            finally
            {
                await host.StopAsync();
                host.Dispose();
                starter.Dispose();
            }
        }

        // --- Second run: new host, same DB + keystore + disk ---
        {
            var starter = new InProcessKestrelProcessStarter();
            var host = BuildBridgeHost(dbRoot, keystore, starter);
            await host.StartAsync();
            try
            {
                // Startup-rebuild should have re-spawned both tenants from
                // ListActiveAsync. Poll until they land Running again.
                var supervisor = host.Services.GetRequiredService<ITenantProcessSupervisor>();
                foreach (var id in tenantIds)
                {
                    await WaitForStateAsync(
                        supervisor, id, TenantProcessState.Running, TimeSpan.FromSeconds(10));
                }

                // Sentinel files survived the restart — data directories are
                // Bridge-owned and are NOT cleaned at shutdown.
                foreach (var id in tenantIds)
                {
                    var sentinelPath = Path.Combine(
                        TenantPaths.NodeDataDirectory(_dataRoot, id), "persist.txt");
                    Assert.True(
                        File.Exists(sentinelPath),
                        $"expected on-disk sentinel '{sentinelPath}' to survive restart");
                    Assert.Equal(id.ToString("D"), File.ReadAllText(sentinelPath));
                }

                // Supervisor spawned both tenants on startup-rebuild.
                Assert.Equal(2, starter.Spawns.Count);
            }
            finally
            {
                await host.StopAsync();
                host.Dispose();
                starter.Dispose();
            }
        }
    }

    // ---- helpers --------------------------------------------------------

    private IHost BuildBridgeHost(
        InMemoryDatabaseRoot dbRoot,
        InMemoryKeystore keystore,
        IProcessStarter starter)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();

        // Orchestration options — point at the per-test data root and supply
        // a placeholder exe path (the fake starter ignores it). Shorten the
        // health-poll interval so the restart-rebuild test doesn't wait 10s
        // for the monitor to consolidate state.
        builder.Services.Configure<BridgeOrchestrationOptions>(o =>
        {
            o.TenantDataRoot = _dataRoot;
            o.LocalNodeExecutablePath = "fake-local-node-host.exe";
            o.HealthPollInterval = TimeSpan.FromMilliseconds(200);
            o.HealthFailureStrikeCount = 3;
            o.RelayRefreshInterval = TimeSpan.FromMilliseconds(500);
        });

        // Bridge DbContext on the shared in-memory root.
        builder.Services.AddSingleton<ITenantContext, TestTenant>();
        builder.Services.AddDbContext<SunfishBridgeDbContext>(o =>
            o.UseInMemoryDatabase("wave52-integration", dbRoot));
        builder.Services.AddScoped<ITenantRegistry, TenantRegistry>();

        // Orchestration stack (Wave 5.2.A / 5.2.C / 5.2.D).
        builder.Services.AddBridgeOrchestration();
        builder.Services.AddBridgeOrchestrationHealth();

        // Inject the fake process starter BEFORE the supervisor extension
        // registers the production one (TryAdd-semantics-compatible: the
        // extension uses AddSingleton, not TryAddSingleton, so we instead
        // replace after the extension runs).
        builder.Services.AddSingleton<IKeystore>(keystore);
        builder.Services.AddSingleton<IRootSeedProvider, KeystoreRootSeedProvider>();
        builder.Services.AddBridgeOrchestrationSupervisor();

        // Swap SystemDiagnosticsProcessStarter for our in-process fake.
        var existing = builder.Services
            .FirstOrDefault(s => s.ServiceType == typeof(IProcessStarter));
        if (existing is not null)
        {
            builder.Services.Remove(existing);
        }
        builder.Services.AddSingleton<IProcessStarter>(starter);

        return builder.Build();
    }

    private static async Task WaitForStateAsync(
        ITenantProcessSupervisor supervisor,
        Guid tenantId,
        TenantProcessState target,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var state = await supervisor.GetStateAsync(tenantId, CancellationToken.None);
            if (state == target)
            {
                return;
            }
            await Task.Delay(50);
        }
        var finalState = await supervisor.GetStateAsync(tenantId, CancellationToken.None);
        throw new Xunit.Sdk.XunitException(
            $"Tenant {tenantId} did not reach {target} within {timeout}; final state = {finalState}.");
    }

    private sealed class TestTenant : ITenantContext
    {
        public string TenantId => "integration-tenant";
        public string UserId => "integration-user";
        public IReadOnlyList<string> Roles { get; } = ["Admin"];
        public bool HasPermission(string permission) => true;
    }

    /// <summary>
    /// <see cref="IProcessStarter"/> that starts a lightweight Kestrel web
    /// host per spawn, serving <c>/health</c> → 200. Stands in for the real
    /// <c>local-node-host</c> dll so the supervisor's health-probe wait-loop
    /// observes a live endpoint and transitions tenants to
    /// <see cref="TenantProcessState.Running"/>.
    /// </summary>
    internal sealed class InProcessKestrelProcessStarter : IProcessStarter, IDisposable
    {
        public List<Spawn> Spawns { get; } = new();

        public IProcessHandle Start(ProcessStartInfo startInfo)
        {
            ArgumentNullException.ThrowIfNull(startInfo);
            var urls = startInfo.EnvironmentVariables["ASPNETCORE_URLS"]
                ?? throw new InvalidOperationException(
                    "ASPNETCORE_URLS missing from test spawn ProcessStartInfo.");

            // Ensure the tenant's data directory exists (the supervisor
            // already does this but repeating here matches the real
            // local-node-host boot flow — first-touch creates the dir).
            var dataDir = startInfo.EnvironmentVariables["LocalNode__DataDirectory"];
            if (!string.IsNullOrWhiteSpace(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            var app = WebApplication.CreateBuilder();
            app.Logging.ClearProviders();
            app.WebHost.UseUrls(urls);
            var webapp = app.Build();
            webapp.MapGet("/health", (HttpContext _) => Results.Ok("healthy"));
            webapp.StartAsync().GetAwaiter().GetResult();

            var handle = new InProcessHandle(webapp);
            Spawns.Add(new Spawn(startInfo, handle));
            return handle;
        }

        public void Dispose()
        {
            foreach (var s in Spawns)
            {
                try
                {
                    s.FakeHandle.Dispose();
                }
                catch
                {
                    // best-effort shutdown.
                }
            }
        }

        internal sealed record Spawn(ProcessStartInfo StartInfo, InProcessHandle FakeHandle);
    }

    /// <summary>
    /// <see cref="IProcessHandle"/> backed by a running Kestrel
    /// <see cref="WebApplication"/>. <see cref="Kill"/> shuts the web host
    /// down synchronously so the next tenant's ephemeral-port bind doesn't
    /// collide with a lingering listener.
    /// </summary>
    internal sealed class InProcessHandle : IProcessHandle
    {
        private static int _nextId;
        private readonly WebApplication _app;
        private bool _disposed;

        public InProcessHandle(WebApplication app)
        {
            _app = app;
            Id = System.Threading.Interlocked.Increment(ref _nextId);
        }

        public int Id { get; }
        public bool HasExited { get; private set; }
        public int ExitCode { get; private set; }

        // Supervisor subscribes but the fake never fires a crash-exit — the
        // tests drive termination via supervisor-side Kill/Stop paths only.
#pragma warning disable CS0067
        public event EventHandler? Exited;
#pragma warning restore CS0067

        public void Kill(bool entireProcessTree)
        {
            if (HasExited)
            {
                return;
            }
            HasExited = true;
            ExitCode = 0;
            try
            {
                _app.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            }
            catch
            {
                // best-effort shutdown; tests do not rely on graceful stop.
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            try
            {
                _app.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            }
            catch
            {
                // ignored — Dispose must not throw.
            }
            try
            {
                ((IDisposable)_app).Dispose();
            }
            catch
            {
                // ignored.
            }
        }
    }
}
