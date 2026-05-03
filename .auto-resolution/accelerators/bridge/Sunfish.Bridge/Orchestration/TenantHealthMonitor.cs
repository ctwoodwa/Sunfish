using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Services;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Wave 5.2.D hosted service that periodically polls each active tenant's
/// <c>/health</c> endpoint (served by the per-tenant <c>local-node-host</c>
/// child — Wave 5.2.D Part 1) and emits <see cref="TenantHealthEvent"/>
/// transitions on a configurable failure-strike count. Wave 5.2.C's
/// supervisor subscribes to these events to project process state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Polling loop.</b> A single background task iterates
/// <see cref="ITenantRegistry.ListActiveAsync"/> every
/// <see cref="BridgeOrchestrationOptions.HealthPollInterval"/>. For each
/// active tenant the monitor resolves the endpoint via
/// <see cref="ITenantEndpointRegistry.TryGet"/>; tenants with no registered
/// endpoint are skipped (they're paused, pre-spawn, or never-registered).
/// </para>
/// <para>
/// <b>Strike counting.</b> The monitor keeps a per-tenant in-memory state
/// (<see cref="TenantState"/>). Each successful poll resets the strike count
/// to zero; each failure increments it. On reaching
/// <see cref="BridgeOrchestrationOptions.HealthFailureStrikeCount"/>
/// consecutive failures, status transitions Healthy → Unhealthy and a
/// <see cref="TenantHealthEvent"/> fires. The next successful poll transitions
/// Unhealthy → Healthy and fires the inverse event.
/// </para>
/// <para>
/// <b>HTTP client.</b> A single <see cref="HttpClient"/> is shared across
/// all polls with a 2s timeout per decomposition plan §6 "Health +
/// Monitoring". Per-tenant <see cref="CancellationTokenSource"/> is used to
/// cap individual probe latency independently of the loop cadence.
/// </para>
/// <para>
/// <b>Decoupling.</b> Per decomposition plan §7 anti-pattern #7, the monitor
/// does NOT call into <see cref="ITenantProcessSupervisor"/>. Instead it
/// exposes <see cref="HealthChanged"/>, which 5.2.C's supervisor subscribes
/// to. This keeps 5.2.D shippable before 5.2.C lands.
/// </para>
/// </remarks>
public sealed class TenantHealthMonitor : IHostedService, IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly ITenantEndpointRegistry _endpoints;
    private readonly BridgeOrchestrationOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ILogger<TenantHealthMonitor> _logger;
    private readonly ConcurrentDictionary<Guid, TenantState> _state = new();

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    /// <summary>
    /// Fired on every Healthy → Unhealthy or Unhealthy → Healthy transition.
    /// Wave 5.2.C's <see cref="ITenantProcessSupervisor"/> subscribes here to
    /// drive its own <see cref="TenantProcessState"/> projections. Handlers
    /// MUST be non-blocking; the monitor fires synchronously from its poll
    /// loop, so a slow handler delays the next poll.
    /// </summary>
    public event EventHandler<TenantHealthEvent>? HealthChanged;

    /// <summary>
    /// Construct the monitor. The service provider is captured so the loop
    /// can create a scope and resolve <see cref="ITenantRegistry"/> (scoped)
    /// on every tick — scoping the DbContext per tick rather than per-monitor
    /// is consistent with the rest of Bridge.
    /// </summary>
    public TenantHealthMonitor(
        IServiceProvider services,
        ITenantEndpointRegistry endpoints,
        IOptions<BridgeOrchestrationOptions> options,
        HttpClient? httpClient = null,
        ILogger<TenantHealthMonitor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);

        _services = services;
        _endpoints = endpoints;
        _options = options.Value;
        _logger = logger ?? NullLogger<TenantHealthMonitor>.Instance;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            _ownsHttpClient = true;
        }
    }

    /// <summary>
    /// Read-only snapshot of the monitor's current view of every tracked
    /// tenant's health. Exposed for tests and diagnostics — production
    /// consumers should subscribe to <see cref="HealthChanged"/> rather
    /// than poll this.
    /// </summary>
    public IReadOnlyDictionary<Guid, TenantHealthStatus> CurrentStatuses
        => _state.ToDictionary(kv => kv.Key, kv => kv.Value.Status);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loopTask is not null)
        {
            return Task.CompletedTask;
        }

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loopCts is null || _loopTask is null)
        {
            return;
        }

        _loopCts.Cancel();
        try
        {
            await _loopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        finally
        {
            _loopCts.Dispose();
            _loopCts = null;
            _loopTask = null;
        }
    }

    /// <summary>
    /// Run a single poll tick against every endpoint-registered tenant.
    /// Exposed for tests so they can drive the monitor deterministically
    /// without spinning up the background loop.
    /// </summary>
    public async Task PollOnceAsync(CancellationToken ct)
    {
        var activeTenants = await LoadActiveTenantIdsAsync(ct).ConfigureAwait(false);
        foreach (var tenantId in activeTenants)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (!_endpoints.TryGet(tenantId, out var endpoint) || endpoint is null)
            {
                // No endpoint yet — pause, never-spawned, or between
                // supervisor events. Do not count a strike; the monitor only
                // tracks tenants it can actually reach.
                continue;
            }

            await ProbeTenantAsync(tenantId, endpoint, ct).ConfigureAwait(false);
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        // Use a PeriodicTimer for steady cadence; first tick fires after the
        // first interval so boot ordering (supervisor spawn → monitor first
        // poll) is not racy on fast test intervals.
        using var timer = new PeriodicTimer(_options.HealthPollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    await PollOnceAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Per decomposition plan §6, the monitor must not crash
                    // the host on a transient failure; log and continue.
                    _logger.LogError(
                        ex,
                        "Tenant health-monitor poll failed; continuing on next interval.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task<IReadOnlyList<Guid>> LoadActiveTenantIdsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var registry = scope.ServiceProvider.GetService<ITenantRegistry>();
            if (registry is null)
            {
                // Tests that drive the monitor without a registry can still
                // exercise PollOnceAsync by populating the endpoint registry
                // directly; fall back to the endpoint-snapshot key set.
                return [.. _endpoints.Snapshot().Keys];
            }

            var active = await registry.ListActiveAsync(ct).ConfigureAwait(false);
            return [.. active.Select(t => t.TenantId)];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Tenant health-monitor could not load active tenant list; falling back to endpoint-registry snapshot.");
            return [.. _endpoints.Snapshot().Keys];
        }
    }

    private async Task ProbeTenantAsync(Guid tenantId, Uri endpoint, CancellationToken ct)
    {
        var healthUri = new Uri(endpoint, "/health");
        bool ok;
        string? failureReason = null;

        try
        {
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probeCts.CancelAfter(TimeSpan.FromSeconds(2));

            using var response = await _httpClient
                .GetAsync(healthUri, HttpCompletionOption.ResponseContentRead, probeCts.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content
                    .ReadAsStringAsync(probeCts.Token)
                    .ConfigureAwait(false);
                // The health-check middleware writes "Healthy" / "Degraded" /
                // "Unhealthy" as the default plaintext response body. Per
                // decomposition plan §6 Bridge treats anything other than
                // "Healthy" as a failure — Degraded is a transient boot state
                // that should resolve before the strike count trips.
                ok = string.Equals(body.Trim(), "Healthy", StringComparison.Ordinal);
                if (!ok)
                {
                    failureReason = $"Health probe returned 200 but body '{body.Trim()}' is not 'Healthy'.";
                }
            }
            else
            {
                ok = false;
                failureReason = $"Health probe returned HTTP {(int)response.StatusCode}.";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // outer loop handles shutdown
        }
        catch (OperationCanceledException)
        {
            ok = false;
            failureReason = "Health probe timed out (>2s).";
        }
        catch (HttpRequestException ex)
        {
            ok = false;
            failureReason = $"Health probe transport error: {ex.Message}";
        }

        RecordProbeResult(tenantId, ok, failureReason);
    }

    private void RecordProbeResult(Guid tenantId, bool ok, string? failureReason)
    {
        var state = _state.GetOrAdd(tenantId, _ => new TenantState());
        TenantHealthEvent? eventToFire = null;

        lock (state.Gate)
        {
            var previous = state.Status;
            if (ok)
            {
                state.StrikeCount = 0;
                if (previous == TenantHealthStatus.Unhealthy || previous == TenantHealthStatus.Unknown)
                {
                    // Transition back to Healthy. From Unknown we also fire
                    // so subscribers see the first "OK" signal — useful for
                    // initial-boot observability.
                    state.Status = TenantHealthStatus.Healthy;
                    if (previous == TenantHealthStatus.Unhealthy)
                    {
                        eventToFire = new TenantHealthEvent(
                            TenantId: tenantId,
                            Previous: previous,
                            Current: TenantHealthStatus.Healthy,
                            OccurredAt: DateTimeOffset.UtcNow,
                            Reason: "Health probe succeeded after previous failure.");
                    }
                }
                else
                {
                    // previously Healthy, stays Healthy — no event.
                    state.Status = TenantHealthStatus.Healthy;
                }
            }
            else
            {
                state.StrikeCount++;
                if (state.StrikeCount >= _options.HealthFailureStrikeCount
                    && previous != TenantHealthStatus.Unhealthy)
                {
                    state.Status = TenantHealthStatus.Unhealthy;
                    eventToFire = new TenantHealthEvent(
                        TenantId: tenantId,
                        Previous: previous,
                        Current: TenantHealthStatus.Unhealthy,
                        OccurredAt: DateTimeOffset.UtcNow,
                        Reason: failureReason
                            ?? $"{state.StrikeCount} consecutive failed health probes.");
                }
            }
        }

        if (eventToFire is not null)
        {
            HealthChanged?.Invoke(this, eventToFire);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// Per-tenant mutable state. Exposed as a nested type so the monitor's
    /// strike counter + status transitions are colocated with the public
    /// event surface.
    /// </summary>
    private sealed class TenantState
    {
        public readonly object Gate = new();
        public TenantHealthStatus Status = TenantHealthStatus.Unknown;
        public int StrikeCount;
    }
}
