using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Relay;
using Sunfish.Bridge.Services;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Wave 5.2.E hosted service that periodically re-reads
/// <see cref="ITenantRegistry.ListActiveAsync"/> and pushes the resulting
/// <c>tenant-id</c> set into the relay's allowlist. The plan's 5.2.C.1 carve-out
/// deferred this to 5.2.E; the supervisor owns per-tenant process lifecycle
/// and the coordinator owns the lifecycle-event → supervisor translation, but
/// neither knows about the relay. This service closes the loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>Relay posture coupling.</b> The SaaS posture (ADR 0026 Posture A /
/// ADR 0031) that this refresher is registered under typically does NOT
/// resolve an <see cref="IRelayServer"/> — the relay lives in the Relay
/// posture. The refresher handles the missing-relay case gracefully and
/// returns without further polling if no relay is wired in. Co-hosting both
/// postures in one process is supported and the refresher will then drive
/// the relay's allowlist.
/// </para>
/// <para>
/// <b>Why a hosted service instead of folding into the coordinator.</b>
/// <see cref="TenantLifecycleCoordinator"/> is event-driven (reacts to
/// <see cref="ITenantRegistryEventBus"/> publications). The refresher is
/// timer-driven (re-reads the DB every
/// <see cref="BridgeOrchestrationOptions.RelayRefreshInterval"/>) and is the
/// backstop against missed events or out-of-band DB mutations. Mixing the
/// two models in one class obscures which path drove a given update.
/// </para>
/// <para>
/// <b>Allowlist semantics.</b> The relay's <c>AllowedTeamIds</c> is an
/// empty-means-accept-any allowlist. We always push a non-empty set (or
/// <c>["__none__"]</c> as a fail-closed sentinel when no tenants are active)
/// so an empty registry never becomes "open relay" by accident. Tenant ids
/// are rendered in canonical <c>D</c> form to match the relay's
/// <c>CapabilityResult.Granted</c> team-id derivation (paper §6.1 tier-3).
/// </para>
/// </remarks>
public sealed class BridgeRelayAllowlistRefresher : BackgroundService
{
    private const string FailClosedSentinel = "__none__";

    private readonly IServiceProvider _services;
    private readonly BridgeOrchestrationOptions _options;
    private readonly ILogger<BridgeRelayAllowlistRefresher> _logger;

    public BridgeRelayAllowlistRefresher(
        IServiceProvider services,
        IOptions<BridgeOrchestrationOptions> options,
        ILogger<BridgeRelayAllowlistRefresher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        _services = services;
        _options = options.Value;
        _logger = logger ?? NullLogger<BridgeRelayAllowlistRefresher>.Instance;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay before the first tick so SaaS-posture DB readers don't race
        // migrations at boot. The first tick runs after RelayRefreshInterval.
        var interval = _options.RelayRefreshInterval;
        if (interval <= TimeSpan.Zero)
        {
            // Disabled — the refresher exits quietly so the generic host does
            // not churn a tight loop.
            _logger.LogDebug(
                "BridgeRelayAllowlistRefresher: RelayRefreshInterval={Interval} <= 0; refresher disabled.",
                interval);
            return;
        }

        using var timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "BridgeRelayAllowlistRefresher: refresh iteration failed; will retry in {Interval}.",
                    interval);
            }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();

        var registry = scope.ServiceProvider.GetService<ITenantRegistry>();
        if (registry is null)
        {
            // No registry registered — can't compute allowlist. Log once at
            // debug and return; the next tick will retry.
            _logger.LogDebug(
                "BridgeRelayAllowlistRefresher: ITenantRegistry not registered; refresh is a no-op.");
            return;
        }

        var active = await registry.ListActiveAsync(ct).ConfigureAwait(false);

        // Render allowed team ids. Fall back to a fail-closed sentinel on an
        // empty active set so the relay never silently becomes open.
        var allowed = active.Count == 0
            ? new[] { FailClosedSentinel }
            : active.Select(t => t.TenantId.ToString("D")).ToArray();

        // Push into BridgeOptions.Relay.AllowedTeamIds if the SaaS posture is
        // co-hosted with the relay posture. In the typical SaaS-only case this
        // is a no-op — the relay isn't resolvable from the scope.
        var bridgeOptions = scope.ServiceProvider.GetService<IOptions<BridgeOptions>>();
        if (bridgeOptions?.Value?.Relay is { } relayOptions)
        {
            relayOptions.AllowedTeamIds = allowed;
            _logger.LogDebug(
                "BridgeRelayAllowlistRefresher: pushed {Count} team id(s) into RelayOptions.AllowedTeamIds.",
                allowed.Length);
        }
    }
}
