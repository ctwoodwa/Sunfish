using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Orchestration;
using Sunfish.Bridge.Services;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Orchestration;

/// <summary>
/// Unit tests for Wave 5.2.C.1 <see cref="TenantProcessSupervisor"/>. Uses a
/// <see cref="FakeProcessStarter"/> so tests never spawn a real OS process —
/// the <see cref="SystemDiagnosticsProcessStarter"/> path is integration
/// territory covered by Wave 5.2.E.
/// </summary>
public sealed class TenantProcessSupervisorTests : IDisposable
{
    private static readonly Guid Tenant = new("11111111-2222-3333-4444-555555555555");

    private readonly string _dataRoot;

    public TenantProcessSupervisorTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "sunfish-supervisor-tests", Guid.NewGuid().ToString("D"));
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
            // best-effort cleanup.
        }
    }

    [Fact]
    public async Task StartAsync_spawns_process_with_expected_env_vars()
    {
        var (supervisor, starter) = BuildSupervisor();

        await supervisor.StartAsync(Tenant, CancellationToken.None);

        var spawn = Assert.Single(starter.Spawns);
        Assert.NotNull(spawn.StartInfo);
        Assert.Equal(Tenant.ToString("D"),
            spawn.StartInfo.EnvironmentVariables["LocalNode__TeamId"]);
        Assert.Equal("false",
            spawn.StartInfo.EnvironmentVariables["LocalNode__MultiTeam__Enabled"]);
        Assert.Equal("Development",
            spawn.StartInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"]);
        // NodeId is the tenant's 16 bytes rendered as lower-case hex (32 chars).
        var nodeId = spawn.StartInfo.EnvironmentVariables["LocalNode__NodeId"];
        Assert.NotNull(nodeId);
        Assert.Equal(32, nodeId!.Length);
        // Health port env var is present and numeric.
        var healthPort = spawn.StartInfo.EnvironmentVariables["LocalNode__HealthPort"];
        Assert.NotNull(healthPort);
        Assert.True(int.TryParse(healthPort, out var port) && port > 0);
        // ASPNETCORE_URLS mirrors the ephemeral port.
        var urls = spawn.StartInfo.EnvironmentVariables["ASPNETCORE_URLS"];
        Assert.Equal($"http://127.0.0.1:{port}", urls);
    }

    [Fact]
    public async Task StartAsync_creates_data_directory_if_missing()
    {
        var (supervisor, _) = BuildSupervisor();
        var expected = TenantPaths.NodeDataDirectory(_dataRoot, Tenant);
        Assert.False(Directory.Exists(expected));

        await supervisor.StartAsync(Tenant, CancellationToken.None);

        Assert.True(Directory.Exists(expected));
    }

    [Fact]
    public async Task StartAsync_with_NoHostedPeer_trust_level_does_not_spawn()
    {
        var (supervisor, starter) = BuildSupervisor(
            trustLevel: TrustLevel.NoHostedPeer);

        await supervisor.StartAsync(Tenant, CancellationToken.None);

        Assert.Empty(starter.Spawns);
        // State should remain Unknown — no handle was created.
        var state = await supervisor.GetStateAsync(Tenant, CancellationToken.None);
        Assert.Equal(TenantProcessState.Unknown, state);
    }

    [Fact]
    public async Task PauseAsync_kills_process_but_retains_handle_and_data()
    {
        var (supervisor, starter) = BuildSupervisor();
        await supervisor.StartAsync(Tenant, CancellationToken.None);
        var dataDir = TenantPaths.NodeDataDirectory(_dataRoot, Tenant);
        File.WriteAllText(Path.Combine(dataDir, "marker.txt"), "payload");

        var spawn = starter.Spawns[0];
        Assert.False(spawn.FakeHandle.KillCalled);

        await supervisor.PauseAsync(Tenant, CancellationToken.None);

        Assert.True(spawn.FakeHandle.KillCalled);
        Assert.Equal(TenantProcessState.Paused,
            await supervisor.GetStateAsync(Tenant, CancellationToken.None));
        // Tenant data directory retained.
        Assert.True(File.Exists(Path.Combine(dataDir, "marker.txt")));
    }

    [Fact]
    public async Task ResumeAsync_restarts_killed_process()
    {
        var (supervisor, starter) = BuildSupervisor();
        await supervisor.StartAsync(Tenant, CancellationToken.None);
        await supervisor.PauseAsync(Tenant, CancellationToken.None);

        await supervisor.ResumeAsync(Tenant, CancellationToken.None);

        Assert.Equal(2, starter.Spawns.Count);
        // After restart, state transitions back to Starting.
        var state = await supervisor.GetStateAsync(Tenant, CancellationToken.None);
        Assert.True(state is TenantProcessState.Starting or TenantProcessState.Running);
    }

    [Fact]
    public async Task StopAndEraseAsync_RetainCiphertext_moves_to_graveyard()
    {
        var (supervisor, _) = BuildSupervisor();
        await supervisor.StartAsync(Tenant, CancellationToken.None);
        var tenantRoot = TenantPaths.TenantRoot(_dataRoot, Tenant);
        File.WriteAllText(Path.Combine(tenantRoot, "relic.txt"), "keep-me");

        await supervisor.StopAndEraseAsync(Tenant, DeleteMode.RetainCiphertext, CancellationToken.None);

        Assert.False(Directory.Exists(tenantRoot));
        var graveyardParent = Path.Combine(_dataRoot, "graveyard", Tenant.ToString("D"));
        Assert.True(Directory.Exists(graveyardParent));
        // The timestamped subdir should exist and contain the relic.
        var timestamped = Directory.GetDirectories(graveyardParent);
        var single = Assert.Single(timestamped);
        Assert.True(File.Exists(Path.Combine(single, "relic.txt")));
    }

    [Fact]
    public async Task StopAndEraseAsync_SecureWipe_deletes_tree()
    {
        var (supervisor, _) = BuildSupervisor();
        await supervisor.StartAsync(Tenant, CancellationToken.None);
        var tenantRoot = TenantPaths.TenantRoot(_dataRoot, Tenant);
        File.WriteAllText(Path.Combine(tenantRoot, "bye.txt"), "delete-me");

        await supervisor.StopAndEraseAsync(Tenant, DeleteMode.SecureWipe, CancellationToken.None);

        Assert.False(Directory.Exists(tenantRoot));
        // Graveyard not populated for SecureWipe.
        var graveyardRoot = Path.Combine(_dataRoot, "graveyard");
        if (Directory.Exists(graveyardRoot))
        {
            var tenantGraveyard = Path.Combine(graveyardRoot, Tenant.ToString("D"));
            Assert.False(Directory.Exists(tenantGraveyard));
        }
    }

    [Fact]
    public async Task GetStateAsync_unknown_tenant_returns_Unknown()
    {
        var (supervisor, _) = BuildSupervisor();
        var state = await supervisor.GetStateAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(TenantProcessState.Unknown, state);
    }

    [Fact]
    public async Task Idempotent_pause()
    {
        var (supervisor, starter) = BuildSupervisor();
        await supervisor.StartAsync(Tenant, CancellationToken.None);
        await supervisor.PauseAsync(Tenant, CancellationToken.None);

        // Second pause must not spawn, must not re-kill, must not throw.
        await supervisor.PauseAsync(Tenant, CancellationToken.None);

        // Still exactly one spawn.
        Assert.Single(starter.Spawns);
        Assert.Equal(TenantProcessState.Paused,
            await supervisor.GetStateAsync(Tenant, CancellationToken.None));
    }

    [Fact]
    public async Task Idempotent_resume()
    {
        var (supervisor, starter) = BuildSupervisor();
        await supervisor.StartAsync(Tenant, CancellationToken.None);

        // ResumeAsync on an already-Starting/Running tenant is a no-op.
        await supervisor.ResumeAsync(Tenant, CancellationToken.None);

        Assert.Single(starter.Spawns);
    }

    [Fact]
    public async Task StateChanged_fires_on_transition()
    {
        var (supervisor, _) = BuildSupervisor();
        var events = new List<TenantProcessEvent>();
        supervisor.StateChanged += (_, e) => events.Add(e);

        await supervisor.StartAsync(Tenant, CancellationToken.None);
        await supervisor.PauseAsync(Tenant, CancellationToken.None);

        // First event: Unknown → Starting; Second: Starting → Paused.
        // (Additional Starting→Running may fire asynchronously from the health
        // waiter, but with a fake starter there's no HTTP server and the
        // probe will fail — it leaves state in Starting until Pause rewrites.)
        Assert.Contains(events,
            e => e.Current == TenantProcessState.Starting);
        Assert.Contains(events,
            e => e.Current == TenantProcessState.Paused);
    }

    // ---- test doubles ---------------------------------------------------

    private (TenantProcessSupervisor supervisor, FakeProcessStarter starter) BuildSupervisor(
        TrustLevel trustLevel = TrustLevel.RelayOnly)
    {
        var starter = new FakeProcessStarter();
        var options = Options.Create(new BridgeOrchestrationOptions
        {
            TenantDataRoot = _dataRoot,
            LocalNodeExecutablePath = "fake-node-host.exe",
        });

        var registry = new FakeTenantRegistry();
        registry.Seed(new TenantRegistration
        {
            TenantId = Tenant,
            Slug = "t",
            DisplayName = "T",
            TrustLevel = trustLevel,
            Status = TenantStatus.Active,
        });

        var services = new ServiceCollection();
        services.AddScoped<ITenantRegistry>(_ => registry);
        var provider = services.BuildServiceProvider();

        var endpoints = new InMemoryTenantEndpointRegistry();
        var supervisor = new TenantProcessSupervisor(
            options,
            provider,
            endpoints,
            starter);
        return (supervisor, starter);
    }

    internal sealed class FakeProcessStarter : IProcessStarter
    {
        public List<Spawn> Spawns { get; } = new();

        public IProcessHandle Start(ProcessStartInfo startInfo)
        {
            var handle = new FakeProcessHandle();
            Spawns.Add(new Spawn(startInfo, handle));
            return handle;
        }

        internal sealed record Spawn(ProcessStartInfo StartInfo, FakeProcessHandle FakeHandle);
    }

    internal sealed class FakeProcessHandle : IProcessHandle
    {
        private static int _next;
        public int Id { get; } = System.Threading.Interlocked.Increment(ref _next);
        public bool HasExited { get; private set; }
        public int ExitCode { get; private set; }
        public bool KillCalled { get; private set; }

        public event EventHandler? Exited;

        public void Kill(bool entireProcessTree)
        {
            KillCalled = true;
            if (!HasExited)
            {
                HasExited = true;
                ExitCode = -1;
                // Do NOT raise Exited here — the real Process.Exited fires
                // asynchronously; tests observing transitions should not rely
                // on the synchronous event.
            }
        }

        public void RaiseExited(int exitCode)
        {
            HasExited = true;
            ExitCode = exitCode;
            Exited?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            // no-op.
        }
    }

    internal sealed class FakeTenantRegistry : ITenantRegistry
    {
        private readonly Dictionary<Guid, TenantRegistration> _rows = new();

        public void Seed(TenantRegistration reg) => _rows[reg.TenantId] = reg;

        public Task<TenantRegistration?> GetByIdAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(_rows.TryGetValue(tenantId, out var r) ? r : null);

        public Task<TenantRegistration?> GetBySlugAsync(string slug, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<TenantRegistration> CreateAsync(string slug, string displayName, string plan, CancellationToken ct)
            => throw new NotImplementedException();

        public Task SetTeamPublicKeyAsync(Guid tenantId, byte[] publicKey, CancellationToken ct)
            => throw new NotImplementedException();

        public Task UpdateTrustLevelAsync(Guid tenantId, TrustLevel level, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<TenantRegistration>> ListActiveAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TenantRegistration>>(
                _rows.Values.Where(r => r.Status == TenantStatus.Active).ToList());

        public ValueTask SuspendAsync(Guid id, string reason, CancellationToken ct)
            => throw new NotImplementedException();

        public ValueTask ResumeAsync(Guid id, CancellationToken ct)
            => throw new NotImplementedException();

        public ValueTask CancelAsync(Guid id, DeleteMode mode, CancellationToken ct)
            => throw new NotImplementedException();
    }
}
