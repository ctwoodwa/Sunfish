using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Services;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Wave 5.2.C.1 implementation of <see cref="ITenantProcessSupervisor"/> that
/// spawns per-tenant <c>local-node-host</c> children via
/// <see cref="Process.Start(ProcessStartInfo)"/> — no Aspire entanglement.
/// Wave 5.2.C.2 will layer the
/// <c>AddProject&lt;Projects.Sunfish_LocalNodeHost&gt;</c> AppHost boot path
/// on top once Aspire 13.2 runtime resource-graph mutability (decomposition
/// plan §8 stop-work #3) is validated.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-tenant state.</b> A <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by tenant id holds one <see cref="TenantProcessHandle"/> per tenant.
/// Every mutating operation takes the handle's per-tenant gate before
/// transitioning, so concurrent Start / Pause / Resume / StopAndErase calls
/// for the same tenant serialize. Cross-tenant calls are fully parallel.
/// </para>
/// <para>
/// <b>Per-tenant seed isolation is NOT yet enforced.</b> Children boot with
/// <c>ASPNETCORE_ENVIRONMENT=Development</c> and rely on the Program-time
/// zero-seed stub — so two tenants on the same install currently share the
/// same encryption seed. Decomposition plan §8 stop-work #1 tracks the fix;
/// this supervisor will not drive it.
/// </para>
/// <para>
/// <b>Endpoint assignment.</b> Each spawn binds a <see cref="TcpListener"/> on
/// <c>127.0.0.1:0</c>, reads the OS-assigned ephemeral port, releases the
/// listener, and passes the port to the child via
/// <c>ASPNETCORE_URLS</c> + <c>LocalNode__HealthPort</c>. There is a tiny
/// TOCTOU window between listener.Close and child bind; in practice the port
/// remains free. 5.2.E integration tests cover the happy path.
/// </para>
/// </remarks>
public sealed class TenantProcessSupervisor : ITenantProcessSupervisor, IDisposable
{
    private readonly BridgeOrchestrationOptions _options;
    private readonly IServiceProvider _services;
    private readonly ITenantEndpointRegistry _endpoints;
    private readonly IProcessStarter _processStarter;
    private readonly ILogger<TenantProcessSupervisor> _logger;
    private readonly ConcurrentDictionary<Guid, TenantProcessHandle> _handles = new();
    private readonly HttpClient _healthProbeClient;
    private readonly bool _ownsHealthProbeClient;

    /// <inheritdoc />
    public event EventHandler<TenantProcessEvent>? StateChanged;

    /// <summary>
    /// Construct the supervisor. The <paramref name="services"/> parameter is
    /// captured so each <see cref="StartAsync"/> call can open a fresh scope
    /// and resolve the scoped <see cref="ITenantRegistry"/>.
    /// </summary>
    public TenantProcessSupervisor(
        IOptions<BridgeOrchestrationOptions> options,
        IServiceProvider services,
        ITenantEndpointRegistry endpoints,
        IProcessStarter processStarter,
        HttpClient? healthProbeClient = null,
        ILogger<TenantProcessSupervisor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(processStarter);

        _options = options.Value;
        _services = services;
        _endpoints = endpoints;
        _processStarter = processStarter;
        _logger = logger ?? NullLogger<TenantProcessSupervisor>.Instance;

        if (healthProbeClient is not null)
        {
            _healthProbeClient = healthProbeClient;
            _ownsHealthProbeClient = false;
        }
        else
        {
            _healthProbeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            _ownsHealthProbeClient = true;
        }
    }

    /// <inheritdoc />
    public async ValueTask StartAsync(Guid tenantId, CancellationToken ct)
    {
        var registration = await LoadRegistrationAsync(tenantId, ct).ConfigureAwait(false);
        if (registration is null)
        {
            _logger.LogWarning(
                "TenantProcessSupervisor.StartAsync: tenant {TenantId} not found in registry; skipping spawn.",
                tenantId);
            return;
        }

        if (registration.TrustLevel == TrustLevel.NoHostedPeer)
        {
            _logger.LogInformation(
                "TenantProcessSupervisor.StartAsync: tenant {TenantId} has TrustLevel=NoHostedPeer; skipping spawn per ADR 0031.",
                tenantId);
            return;
        }

        var handle = _handles.GetOrAdd(tenantId, id => new TenantProcessHandle { TenantId = id });

        TenantProcessEvent? transitionEvent = null;
        Uri? endpointToRegister = null;

        lock (handle.Gate)
        {
            // Idempotent on already-running / already-starting tenants.
            if (handle.State is TenantProcessState.Running or TenantProcessState.Starting)
            {
                return;
            }

            var previous = handle.State;

            var dataDir = TenantPaths.NodeDataDirectory(_options.TenantDataRoot, tenantId);
            Directory.CreateDirectory(dataDir);

            var port = PickEphemeralPort();
            var endpoint = new Uri($"http://127.0.0.1:{port}/");

            var startInfo = BuildStartInfo(tenantId, dataDir, port, endpoint);
            IProcessHandle process;
            try
            {
                process = _processStarter.Start(startInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "TenantProcessSupervisor.StartAsync: failed to spawn child for tenant {TenantId}.",
                    tenantId);
                handle.State = TenantProcessState.Failed;
                transitionEvent = new TenantProcessEvent(
                    TenantId: tenantId,
                    Previous: previous,
                    Current: TenantProcessState.Failed,
                    OccurredAt: DateTimeOffset.UtcNow,
                    Reason: $"Process.Start failed: {ex.Message}");
                goto FireEvent;
            }

            handle.Process = process;
            handle.HealthEndpoint = endpoint;
            handle.StartedAt = DateTimeOffset.UtcNow;
            handle.State = TenantProcessState.Starting;
            endpointToRegister = endpoint;

            // Wire the OS exit event so we can project Failed on unexpected
            // termination. Captured tenantId avoids closing over handle.
            var tid = tenantId;
            process.Exited += (_, _) => OnProcessExited(tid);

            transitionEvent = new TenantProcessEvent(
                TenantId: tenantId,
                Previous: previous,
                Current: TenantProcessState.Starting,
                OccurredAt: handle.StartedAt,
                Reason: null);
        }

    FireEvent:
        if (endpointToRegister is not null)
        {
            _endpoints.Register(tenantId, endpointToRegister);
        }
        if (transitionEvent is not null)
        {
            StateChanged?.Invoke(this, transitionEvent);
        }

        if (handle.State == TenantProcessState.Starting)
        {
            // Fire-and-forget health wait; drops the tenant to Running on first
            // 200 response, or leaves it in Starting if the probe times out
            // (health monitor will then take over and may promote to Unhealthy).
            _ = Task.Run(() => WaitForHealthyAsync(tenantId, ct), ct);
        }
    }

    /// <inheritdoc />
    public ValueTask PauseAsync(Guid tenantId, CancellationToken ct)
    {
        if (!_handles.TryGetValue(tenantId, out var handle))
        {
            return ValueTask.CompletedTask;
        }

        TenantProcessEvent? transitionEvent = null;

        lock (handle.Gate)
        {
            if (handle.State == TenantProcessState.Paused)
            {
                return ValueTask.CompletedTask;
            }

            var previous = handle.State;
            var process = handle.Process;
            handle.Process = null;
            handle.State = TenantProcessState.Paused;

            if (process is not null)
            {
                process.Kill(entireProcessTree: true);
                process.Dispose();
            }

            transitionEvent = new TenantProcessEvent(
                TenantId: tenantId,
                Previous: previous,
                Current: TenantProcessState.Paused,
                OccurredAt: DateTimeOffset.UtcNow,
                Reason: "PauseAsync invoked.");
        }

        _endpoints.Unregister(tenantId);
        StateChanged?.Invoke(this, transitionEvent);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ResumeAsync(Guid tenantId, CancellationToken ct)
    {
        // ResumeAsync semantics are identical to StartAsync — re-run the
        // spawn pipeline. Idempotent against Running (handled inside StartAsync).
        return StartAsync(tenantId, ct);
    }

    /// <inheritdoc />
    public ValueTask StopAndEraseAsync(Guid tenantId, DeleteMode mode, CancellationToken ct)
    {
        TenantProcessEvent? transitionEvent = null;
        TenantProcessState previous = TenantProcessState.Unknown;

        if (_handles.TryGetValue(tenantId, out var handle))
        {
            lock (handle.Gate)
            {
                previous = handle.State;
                var process = handle.Process;
                handle.Process = null;
                handle.State = TenantProcessState.Cancelled;

                if (process is not null)
                {
                    process.Kill(entireProcessTree: true);
                    process.Dispose();
                }
            }
            _handles.TryRemove(tenantId, out _);
        }

        _endpoints.Unregister(tenantId);

        // Disk disposition (best-effort — ciphertext already protects contents).
        try
        {
            var tenantRoot = TenantPaths.TenantRoot(_options.TenantDataRoot, tenantId);
            if (Directory.Exists(tenantRoot))
            {
                switch (mode)
                {
                    case DeleteMode.RetainCiphertext:
                        var graveyard = TenantPaths.GraveyardRoot(
                            _options.TenantDataRoot, tenantId, DateTimeOffset.UtcNow);
                        var graveyardParent = Path.GetDirectoryName(graveyard);
                        if (!string.IsNullOrEmpty(graveyardParent))
                        {
                            Directory.CreateDirectory(graveyardParent);
                        }
                        Directory.Move(tenantRoot, graveyard);
                        break;

                    case DeleteMode.SecureWipe:
                        Directory.Delete(tenantRoot, recursive: true);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "TenantProcessSupervisor.StopAndEraseAsync: disk disposition failed for tenant {TenantId} (mode={Mode}).",
                tenantId, mode);
        }

        transitionEvent = new TenantProcessEvent(
            TenantId: tenantId,
            Previous: previous,
            Current: TenantProcessState.Cancelled,
            OccurredAt: DateTimeOffset.UtcNow,
            Reason: $"StopAndEraseAsync({mode}).");

        StateChanged?.Invoke(this, transitionEvent);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<TenantProcessState> GetStateAsync(Guid tenantId, CancellationToken ct)
    {
        if (_handles.TryGetValue(tenantId, out var handle))
        {
            return ValueTask.FromResult(handle.State);
        }
        return ValueTask.FromResult(TenantProcessState.Unknown);
    }

    /// <summary>
    /// Mark a tenant <see cref="TenantProcessState.Unhealthy"/> — called by
    /// <see cref="TenantLifecycleCoordinator"/> in response to
    /// <see cref="TenantHealthMonitor.HealthChanged"/> events. Idempotent on
    /// tenants not currently <see cref="TenantProcessState.Running"/>.
    /// </summary>
    public void MarkUnhealthy(Guid tenantId, string? reason)
    {
        if (!_handles.TryGetValue(tenantId, out var handle))
        {
            return;
        }

        TenantProcessEvent? transitionEvent = null;
        lock (handle.Gate)
        {
            if (handle.State == TenantProcessState.Unhealthy)
            {
                return;
            }
            var previous = handle.State;
            handle.State = TenantProcessState.Unhealthy;
            transitionEvent = new TenantProcessEvent(
                TenantId: tenantId,
                Previous: previous,
                Current: TenantProcessState.Unhealthy,
                OccurredAt: DateTimeOffset.UtcNow,
                Reason: reason);
        }
        StateChanged?.Invoke(this, transitionEvent);
    }

    /// <summary>
    /// Mark a previously-<see cref="TenantProcessState.Unhealthy"/> tenant
    /// back to <see cref="TenantProcessState.Running"/> after a successful
    /// health probe.
    /// </summary>
    public void MarkHealthy(Guid tenantId)
    {
        if (!_handles.TryGetValue(tenantId, out var handle))
        {
            return;
        }

        TenantProcessEvent? transitionEvent = null;
        lock (handle.Gate)
        {
            if (handle.State != TenantProcessState.Unhealthy)
            {
                return;
            }
            handle.State = TenantProcessState.Running;
            transitionEvent = new TenantProcessEvent(
                TenantId: tenantId,
                Previous: TenantProcessState.Unhealthy,
                Current: TenantProcessState.Running,
                OccurredAt: DateTimeOffset.UtcNow,
                Reason: "Health probe recovered.");
        }
        StateChanged?.Invoke(this, transitionEvent);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var handle in _handles.Values)
        {
            lock (handle.Gate)
            {
                handle.Process?.Kill(entireProcessTree: true);
                handle.Process?.Dispose();
                handle.Process = null;
            }
        }
        _handles.Clear();
        if (_ownsHealthProbeClient)
        {
            _healthProbeClient.Dispose();
        }
    }

    // ---- internals ------------------------------------------------------

    private ProcessStartInfo BuildStartInfo(Guid tenantId, string dataDir, int port, Uri endpoint)
    {
        // Deterministic 16-byte (32 hex chars) NodeId derived from tenantId's
        // first 16 bytes. Stable across restarts so the child presents the
        // same node identity to the relay.
        var tenantBytes = tenantId.ToByteArray();
        var nodeIdHex = Convert.ToHexString(tenantBytes).ToLowerInvariant();

        // FileName + arguments:
        //   - LocalNodeExecutablePath non-null → spawn it directly.
        //   - null → "dotnet {dll-path}" — requires the caller to have set
        //     LocalNodeExecutablePath to the dll path. We do NOT attempt to
        //     reflect Assembly.GetEntryAssembly() of Sunfish.LocalNodeHost
        //     (it is not a reference of Sunfish.Bridge by design — local-node-host
        //     is not a library). W5.2.E integration test rigs set the path
        //     explicitly; production AppHost (W5.2.C.2) will resolve via Aspire.
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = dataDir,
        };

        var exePath = _options.LocalNodeExecutablePath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            // Fall back to "dotnet" with no args — callers lacking the exe
            // path will land in a clearly-failed child. This matches the
            // decomposition plan §4 guidance: require LocalNodeExecutablePath
            // in 5.2.C.1 (the reflective path is 5.2.C.2 Aspire territory).
            startInfo.FileName = "dotnet";
        }
        else if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = "dotnet";
            startInfo.ArgumentList.Add(exePath);
        }
        else
        {
            startInfo.FileName = exePath;
        }

        startInfo.EnvironmentVariables["LocalNode__NodeId"] = nodeIdHex;
        startInfo.EnvironmentVariables["LocalNode__TeamId"] = tenantId.ToString("D");
        startInfo.EnvironmentVariables["LocalNode__DataDirectory"] = dataDir;
        startInfo.EnvironmentVariables["LocalNode__MultiTeam__Enabled"] = "false";
        startInfo.EnvironmentVariables["LocalNode__HealthPort"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        // W5.2 stop-work #1: children need Development to skip the keystore-
        // backed seed until W6.7 lands. Tracked as follow-up.
        startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = endpoint.ToString().TrimEnd('/');

        return startInfo;
    }

    private static int PickEphemeralPort()
    {
        // Bind to 127.0.0.1:0, read the OS-assigned port, release. There is a
        // tiny TOCTOU window between listener.Stop() and the child's bind; in
        // practice this is acceptable because the supervisor calls this once
        // per spawn and the OS rarely hands out the same port to a different
        // process in that window.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            return endpoint.Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task<TenantRegistration?> LoadRegistrationAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var registry = scope.ServiceProvider.GetService<ITenantRegistry>();
            if (registry is null)
            {
                // No registry registered (test scenarios). Synthesize a
                // default registration so the supervisor still spawns — tests
                // that want NoHostedPeer short-circuit inject their own fake
                // ITenantRegistry.
                return new TenantRegistration
                {
                    TenantId = tenantId,
                    Slug = tenantId.ToString("D"),
                    DisplayName = tenantId.ToString("D"),
                    TrustLevel = TrustLevel.RelayOnly,
                    Status = TenantStatus.Active,
                };
            }
            return await registry.GetByIdAsync(tenantId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "TenantProcessSupervisor: failed to load tenant {TenantId} from registry.",
                tenantId);
            return null;
        }
    }

    private async Task WaitForHealthyAsync(Guid tenantId, CancellationToken ct)
    {
        if (!_handles.TryGetValue(tenantId, out var handle))
        {
            return;
        }

        var healthUri = new Uri(handle.HealthEndpoint, "/health");
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        var pollInterval = TimeSpan.FromMilliseconds(250);

        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                probeCts.CancelAfter(TimeSpan.FromSeconds(1));
                using var response = await _healthProbeClient
                    .GetAsync(healthUri, probeCts.Token)
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    TenantProcessEvent? transition = null;
                    lock (handle.Gate)
                    {
                        if (handle.State == TenantProcessState.Starting)
                        {
                            handle.State = TenantProcessState.Running;
                            transition = new TenantProcessEvent(
                                TenantId: tenantId,
                                Previous: TenantProcessState.Starting,
                                Current: TenantProcessState.Running,
                                OccurredAt: DateTimeOffset.UtcNow,
                                Reason: "First health probe succeeded.");
                        }
                    }
                    if (transition is not null)
                    {
                        StateChanged?.Invoke(this, transition);
                    }
                    return;
                }
            }
            catch
            {
                // Swallow transient errors — retry until deadline.
            }

            try
            {
                await Task.Delay(pollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
        // Timeout: leave the tenant in Starting. The health monitor (5.2.D)
        // will either promote to Running on its first successful poll or to
        // Unhealthy after three strikes.
    }

    private void OnProcessExited(Guid tenantId)
    {
        if (!_handles.TryGetValue(tenantId, out var handle))
        {
            return;
        }

        TenantProcessEvent? transitionEvent = null;
        lock (handle.Gate)
        {
            // Ignore exits that are the intended consequence of Pause /
            // StopAndErase (those transitions null out handle.Process before
            // killing). Only project Failed when the process exits on its own.
            if (handle.Process is null)
            {
                return;
            }
            if (handle.State is TenantProcessState.Paused or TenantProcessState.Cancelled)
            {
                return;
            }

            var previous = handle.State;
            var exitCode = -1;
            try
            {
                exitCode = handle.Process.ExitCode;
            }
            catch
            {
                // Ignored — exit code not available.
            }
            handle.State = TenantProcessState.Failed;

            transitionEvent = new TenantProcessEvent(
                TenantId: tenantId,
                Previous: previous,
                Current: TenantProcessState.Failed,
                OccurredAt: DateTimeOffset.UtcNow,
                Reason: $"Child exited unexpectedly with code {exitCode}.");
        }

        _endpoints.Unregister(tenantId);
        if (transitionEvent is not null)
        {
            StateChanged?.Invoke(this, transitionEvent);
        }
    }
}
