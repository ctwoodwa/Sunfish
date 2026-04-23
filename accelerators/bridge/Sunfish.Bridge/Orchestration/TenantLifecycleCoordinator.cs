using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Bridge.Data.Entities;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Wave 5.2.C.1 hosted service that bridges
/// <see cref="ITenantRegistryEventBus"/> (producer: <c>TenantRegistry</c>
/// from Wave 5.2.B) and <see cref="ITenantProcessSupervisor"/> (consumer:
/// <c>TenantProcessSupervisor</c>). On every
/// <see cref="TenantLifecycleEvent"/> it drives the matching supervisor
/// operation. Also subscribes to
/// <see cref="TenantHealthMonitor.HealthChanged"/> so health-strike events
/// transition the supervisor's <see cref="TenantProcessState"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decoupling.</b> Keeping the coordinator separate from both the registry
/// and the supervisor means neither has to know about the other. The registry
/// publishes; the supervisor exposes methods; the coordinator wires them
/// together — this matches decomposition plan §7 anti-pattern #7 "Delegation
/// without contracts".
/// </para>
/// <para>
/// <b>DeleteMode.</b> The <see cref="TenantLifecycleEvent.Reason"/> string is
/// used as a discriminator for <see cref="DeleteMode"/> on Cancelled events
/// (TenantRegistry serializes the enum into Reason via <c>mode.ToString()</c>).
/// If parsing fails, default to <see cref="DeleteMode.RetainCiphertext"/> —
/// the safer choice per ADR 0031 ciphertext-at-rest invariant.
/// </para>
/// </remarks>
public sealed class TenantLifecycleCoordinator : IHostedService
{
    private readonly ITenantRegistryEventBus _bus;
    private readonly ITenantProcessSupervisor _supervisor;
    private readonly TenantHealthMonitor? _healthMonitor;
    private readonly ILogger<TenantLifecycleCoordinator> _logger;

    private IDisposable? _busSubscription;

    /// <summary>
    /// Construct the coordinator. <paramref name="healthMonitor"/> is optional
    /// — the coordinator tolerates its absence (relay postures that register
    /// the supervisor without the monitor still boot cleanly).
    /// </summary>
    public TenantLifecycleCoordinator(
        ITenantRegistryEventBus bus,
        ITenantProcessSupervisor supervisor,
        TenantHealthMonitor? healthMonitor = null,
        ILogger<TenantLifecycleCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(supervisor);
        _bus = bus;
        _supervisor = supervisor;
        _healthMonitor = healthMonitor;
        _logger = logger ?? NullLogger<TenantLifecycleCoordinator>.Instance;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _busSubscription = _bus.Subscribe(HandleLifecycleEvent);
        if (_healthMonitor is not null)
        {
            _healthMonitor.HealthChanged += OnHealthChanged;
        }
        return Task.CompletedTask;
        // Wave 5.2.E: a RelayRefreshInterval timer will re-read
        // TenantRegistry.ListActiveAsync and rebuild the relay's
        // AllowedTeamIds allowlist. TODO(5.2.E): Bridge's RelayServer is not
        // resolvable in the SaaS posture's Program.cs — defer to 5.2.E once
        // the AppHost composition exposes it to the coordinator.
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _busSubscription?.Dispose();
        _busSubscription = null;
        if (_healthMonitor is not null)
        {
            _healthMonitor.HealthChanged -= OnHealthChanged;
        }
        return Task.CompletedTask;
    }

    private void HandleLifecycleEvent(TenantLifecycleEvent evt)
    {
        // Fire-and-forget: we cannot await inside the bus's synchronous
        // iteration without blocking other subscribers. The supervisor's
        // per-tenant gate serializes concurrent dispatches for the same
        // tenant.
        _ = Task.Run(async () =>
        {
            try
            {
                await DispatchAsync(evt, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "TenantLifecycleCoordinator: failed to dispatch {Previous}→{Current} for tenant {TenantId}.",
                    evt.Previous, evt.Current, evt.TenantId);
            }
        });
    }

    private async Task DispatchAsync(TenantLifecycleEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            // Pending→Active: first-time activation. Start the tenant.
            case { Previous: TenantStatus.Pending, Current: TenantStatus.Active }:
                await _supervisor.StartAsync(evt.TenantId, ct).ConfigureAwait(false);
                break;

            // Active→Suspended: billing/operator pause.
            case { Previous: TenantStatus.Active, Current: TenantStatus.Suspended }:
                await _supervisor.PauseAsync(evt.TenantId, ct).ConfigureAwait(false);
                break;

            // Suspended→Active: resume.
            case { Previous: TenantStatus.Suspended, Current: TenantStatus.Active }:
                await _supervisor.ResumeAsync(evt.TenantId, ct).ConfigureAwait(false);
                break;

            // *→Cancelled: terminate. Reason carries the DeleteMode per
            // TenantRegistry.CancelAsync.
            case { Current: TenantStatus.Cancelled }:
                var mode = ParseDeleteMode(evt.Reason);
                await _supervisor.StopAndEraseAsync(evt.TenantId, mode, ct).ConfigureAwait(false);
                break;

            // Pending→Pending is the "fresh create" marker (TenantRegistry uses
            // Previous==Current as the signal). Pre-allocate but don't spawn;
            // spawn happens on the first Pending→Active. No-op here — the plan
            // says spawn at Active, not at signup.
            case { Previous: TenantStatus.Pending, Current: TenantStatus.Pending }:
                // No-op by design.
                break;

            default:
                // Unknown transition — log and ignore.
                _logger.LogDebug(
                    "TenantLifecycleCoordinator: ignoring unhandled transition {Previous}→{Current} for tenant {TenantId}.",
                    evt.Previous, evt.Current, evt.TenantId);
                break;
        }
    }

    private static DeleteMode ParseDeleteMode(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return DeleteMode.RetainCiphertext;
        }
        return Enum.TryParse<DeleteMode>(reason, ignoreCase: true, out var parsed)
            ? parsed
            : DeleteMode.RetainCiphertext;
    }

    private void OnHealthChanged(object? sender, TenantHealthEvent evt)
    {
        switch (evt.Current)
        {
            case TenantHealthStatus.Unhealthy when _supervisor is TenantProcessSupervisor concrete:
                concrete.MarkUnhealthy(evt.TenantId, evt.Reason);
                break;

            case TenantHealthStatus.Healthy when _supervisor is TenantProcessSupervisor concrete:
                concrete.MarkHealthy(evt.TenantId);
                break;
        }
    }
}
