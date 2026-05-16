using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Recovery;

namespace Sunfish.Anchor.Services;

/// <summary>
/// IHostedService that polls <see cref="IRecoveryCoordinator.EvaluateGracePeriodAsync"/>
/// on a fixed cadence (default 60s; tune via <see cref="RecoveryHostOptions"/>)
/// and dispatches <see cref="RecoveryEventType.RecoveryCompleted"/> events to
/// <see cref="IRecoveryCompletionHandler"/>.
///
/// Per XO ruling 2026-05-16 §(c): the coordinator has no event-subscription
/// surface (no `OnEventRaised` etc.). Events are returned synchronously from
/// each coordinator method; the host adapts via polling.
///
/// Restart safety: <see cref="StartAsync"/> calls <see cref="IRecoveryCoordinator.EvaluateGracePeriodAsync"/>
/// once before entering the loop so any grace window that elapsed during
/// process downtime is caught.
/// </summary>
internal sealed class RecoveryGracePollingService : IHostedService, IAsyncDisposable
{
    private readonly IRecoveryCoordinator _coordinator;
    private readonly IRecoveryCompletionHandler _completion;
    private readonly ILogger<RecoveryGracePollingService> _logger;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public RecoveryGracePollingService(
        IRecoveryCoordinator coordinator,
        IRecoveryCompletionHandler completion,
        IOptions<RecoveryHostOptions> options,
        ILogger<RecoveryGracePollingService> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _completion  = completion  ?? throw new ArgumentNullException(nameof(completion));
        _logger      = logger      ?? throw new ArgumentNullException(nameof(logger));
        var seconds = Math.Max(1, options?.Value?.GracePollIntervalSeconds ?? 60);
        _interval = TimeSpan.FromSeconds(seconds);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await PollOnceAsync(cancellationToken).ConfigureAwait(false);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        _loop = LoopAsync(_cts.Token);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await PollOnceAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery grace poll failed; will retry on the next tick.");
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var result = await _coordinator.EvaluateGracePeriodAsync(ct).ConfigureAwait(false);
        if (result is null) return;
        if (result.Event.Type != RecoveryEventType.RecoveryCompleted) return;
        // W#67 PR 4 — pass the full RecoveryCompletionResult so the
        // handler has access to the trustee attestations (needed to
        // decrypt the seed envelopes for the SQLCipher rekey).
        await _completion.HandleAsync(result, ct).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null) return;
        _cts.Cancel();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
