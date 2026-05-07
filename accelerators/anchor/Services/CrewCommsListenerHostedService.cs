using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Anchor.Services;

/// <summary>
/// W#59 Phase 3 — drives <see cref="IChannelProvider.ListenAsync"/>
/// continuously and forwards each inbound <see cref="IChannelInvitation"/>
/// to <see cref="ICrewCommsInvitationBusWriter"/>. Closes MVP-demo gap (C):
/// W#45 P5 shipped the substrate but no Anchor consumer drained the listener,
/// so a peer's incoming invitation never surfaced to the UI.
/// <para>
/// Lifecycle mirrors <see cref="AnchorSyncHostedService"/>: an
/// <see cref="IHostedService"/> registered AFTER the bootstrap +
/// active-team materialization so the listener has a tenant to bind to.
/// On <see cref="StartAsync"/> a background loop spins up; on
/// <see cref="StopAsync"/> the linked CTS cancels and the loop drains.
/// </para>
/// <para>
/// Backoff on listener exception: 1s → 2s → 4s → 8s → 16s, capped at 16s.
/// Per ADR 0076 a thrown <see cref="OperationCanceledException"/> is the
/// graceful shutdown path; any other exception is logged and the loop
/// retries with exponential delay so a transient network blip doesn't
/// poison the daemon.
/// </para>
/// </summary>
public sealed class CrewCommsListenerHostedService : IHostedService, IAsyncDisposable
{
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(16);

    private readonly IChannelProvider _provider;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly ICrewCommsInvitationBusWriter _bus;
    private readonly ILogger<CrewCommsListenerHostedService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    /// <summary>Construct the listener.</summary>
    public CrewCommsListenerHostedService(
        IChannelProvider provider,
        IActiveTeamAccessor activeTeam,
        ICrewCommsInvitationBusWriter bus,
        ILogger<CrewCommsListenerHostedService> logger)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _activeTeam = activeTeam ?? throw new ArgumentNullException(nameof(activeTeam));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>True when the listener loop is running.</summary>
    public bool IsRunning => _loopTask is { IsCompleted: false };

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loopTask is not null)
        {
            return Task.CompletedTask;
        }
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null || _loopTask is null)
        {
            return;
        }
        try
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Already disposed — race with DisposeAsync; safe to ignore.
        }
        try
        {
            await _loopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected shutdown path.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CrewCommsListener] Listener loop ended abnormally during shutdown.");
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var backoff = InitialBackoff;
        while (!ct.IsCancellationRequested)
        {
            var tenant = ResolveActiveTenantOrNull();
            if (tenant is null)
            {
                _logger.LogDebug("[CrewCommsListener] No active team yet — waiting before retry.");
                try
                {
                    await Task.Delay(InitialBackoff, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                continue;
            }

            try
            {
                _logger.LogInformation(
                    "[CrewCommsListener] Listening on tenant={TenantId}",
                    tenant.Value.Value);

                await foreach (var invitation in _provider.ListenAsync(tenant.Value, ct).ConfigureAwait(false))
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    try
                    {
                        _bus.PublishInvitation(invitation);
                    }
                    catch (Exception publishEx)
                    {
                        _logger.LogWarning(
                            publishEx,
                            "[CrewCommsListener] Bus publish threw — continuing loop.");
                    }
                }

                // ListenAsync returned normally — provider chose to stop.
                // Reset backoff and try again next cycle.
                backoff = InitialBackoff;
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown — exit.
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[CrewCommsListener] ListenAsync threw — retrying in {Backoff}s.",
                    backoff.TotalSeconds);
                try
                {
                    await Task.Delay(backoff, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                backoff = backoff < MaxBackoff
                    ? TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, MaxBackoff.TotalSeconds))
                    : MaxBackoff;
            }
        }
    }

    private TenantId? ResolveActiveTenantOrNull()
    {
        var active = _activeTeam.Active;
        if (active is null)
        {
            return null;
        }
        return new TenantId(active.TeamId.Value.ToString("D"));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            try
            {
                await _cts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Already disposed.
            }
        }
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch
            {
                // Disposal is best-effort; swallow.
            }
        }
        _cts?.Dispose();
    }
}
