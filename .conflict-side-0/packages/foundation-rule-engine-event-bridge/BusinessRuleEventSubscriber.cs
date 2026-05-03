using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Sunfish.Foundation.BusinessLogic;
using Sunfish.Foundation.BusinessLogic.Rules;
using Sunfish.Kernel.Events;

namespace Sunfish.Foundation.RuleEngine.EventBridge;

/// <summary>
/// A hosted background service that subscribes to <see cref="IEventBus"/> and
/// evaluates a <see cref="BusinessRuleEngine"/> reactively whenever a matching
/// <see cref="KernelEvent"/> arrives.
/// </summary>
/// <remarks>
/// <para>
/// This is the G4 integration surface: the bridge sits in a separate package
/// (<c>Sunfish.Foundation.RuleEngine.EventBridge</c>) so that
/// <c>Sunfish.Foundation</c> keeps no dependency on the kernel event bus.
/// </para>
/// <para>
/// By default the subscriber passes the raw <see cref="KernelEvent"/> to
/// <see cref="BusinessRuleEngine.CheckRules"/>. Callers that register rules
/// operating on a richer domain object can supply an extractor function
/// to map the event to that object before evaluation.
/// </para>
/// <para>
/// Filtering (entity, kind) is delegated entirely to <see cref="IEventBus"/>
/// via the <see cref="EventSubscription"/> — the subscriber never re-filters.
/// </para>
/// <para>
/// The <c>onEvaluated</c> callback is invoked synchronously on the subscriber's
/// background thread. Keep it fast; offload heavy work to a channel or queue if needed.
/// </para>
/// </remarks>
public sealed class BusinessRuleEventSubscriber : IHostedService, IDisposable
{
    private readonly IEventBus _bus;
    private readonly BusinessRuleEngine _engine;
    private readonly EventSubscription _subscription;
    private readonly Action<KernelEvent, IReadOnlyList<BrokenRule>>? _onEvaluated;
    private readonly Func<KernelEvent, object>? _extractor;
    private readonly ILogger<BusinessRuleEventSubscriber> _logger;

    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    /// <summary>
    /// Creates a new <see cref="BusinessRuleEventSubscriber"/>.
    /// </summary>
    /// <param name="bus">The event bus to subscribe to.</param>
    /// <param name="engine">The rule engine whose rules are evaluated on each arriving event.</param>
    /// <param name="subscription">
    /// Filter and subscriber-id parameters forwarded to
    /// <see cref="IEventBus.SubscribeAsync"/>. Use <see cref="EventSubscription.KindFilter"/>
    /// and <see cref="EventSubscription.EntityFilter"/> to restrict which events reach
    /// the engine.
    /// </param>
    /// <param name="onEvaluated">
    /// Optional callback invoked after each evaluation with the source event and the
    /// (possibly empty) list of broken rules. Pass <see langword="null"/> to ignore
    /// evaluation results (useful for side-effect-only rule sets).
    /// </param>
    /// <param name="extractor">
    /// Optional function that maps a <see cref="KernelEvent"/> to a domain object.
    /// When provided, the engine receives the extracted object rather than the raw event.
    /// When <see langword="null"/>, the raw <see cref="KernelEvent"/> is passed to
    /// <see cref="BusinessRuleEngine.CheckRules"/>.
    /// </param>
    /// <param name="logger">Optional logger; a no-op logger is used when null.</param>
    public BusinessRuleEventSubscriber(
        IEventBus bus,
        BusinessRuleEngine engine,
        EventSubscription subscription,
        Action<KernelEvent, IReadOnlyList<BrokenRule>>? onEvaluated,
        Func<KernelEvent, object>? extractor = null,
        ILogger<BusinessRuleEventSubscriber>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(subscription);

        _bus = bus;
        _engine = engine;
        _subscription = subscription;
        _onEvaluated = onEvaluated;
        _extractor = extractor;
        _logger = logger ?? NullLogger<BusinessRuleEventSubscriber>.Instance;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null) return;

        await _cts.CancelAsync().ConfigureAwait(false);

        if (_backgroundTask is not null)
        {
            try
            {
                await _backgroundTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected — cancellation is the normal shutdown path.
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ── Background loop ────────────────────────────────────────────────────

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogDebug(
            "BusinessRuleEventSubscriber '{SubscriberId}' starting.",
            _subscription.SubscriberId);

        try
        {
            await foreach (var signedEvent in _bus.SubscribeAsync(_subscription, ct).ConfigureAwait(false))
            {
                var kernelEvent = signedEvent.Payload;

                try
                {
                    // Determine what object to hand to the rule engine.
                    var subject = _extractor is not null
                        ? _extractor(kernelEvent)
                        : (object)kernelEvent;

                    var broken = _engine.CheckRules(subject);

                    if (_onEvaluated is not null)
                    {
                        _onEvaluated(kernelEvent, broken);
                    }

                    if (broken.Count > 0)
                    {
                        _logger.LogWarning(
                            "BusinessRuleEngine found {BrokenCount} broken rule(s) for event {EventId} (kind '{Kind}', entity '{EntityId}').",
                            broken.Count,
                            kernelEvent.Id,
                            kernelEvent.Kind,
                            kernelEvent.EntityId);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Event {EventId} (kind '{Kind}') passed all rules.",
                            kernelEvent.Id,
                            kernelEvent.Kind);
                    }

                    // Advance checkpoint so a persistent backend can resume here.
                    await _bus.AdvanceCheckpointAsync(
                        _subscription.SubscriberId, kernelEvent.Id, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Rule evaluation errors must not crash the loop; log and continue.
                    _logger.LogError(ex,
                        "Unhandled exception evaluating rules for event {EventId}.",
                        kernelEvent.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug(
                "BusinessRuleEventSubscriber '{SubscriberId}' stopped.",
                _subscription.SubscriberId);
        }
    }
}
