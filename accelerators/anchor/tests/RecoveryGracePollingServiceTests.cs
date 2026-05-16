using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.Recovery;
using Xunit;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#63 Phase 2/3 — `RecoveryGracePollingService` contract tests.
///
/// The service polls `IRecoveryCoordinator.EvaluateGracePeriodAsync` on a
/// fixed cadence and dispatches `RecoveryCompleted` events to
/// `IRecoveryCompletionHandler`. Per XO ruling 2026-05-16 §(c): the
/// coordinator has no event-subscription surface, so the host adapts via
/// polling. Tests verify the dispatch + restart-safety paths.
/// </summary>
public sealed class RecoveryGracePollingServiceTests
{
    private static IOptions<RecoveryHostOptions> FastInterval()
        // 1-second floor; faster than the production 60s but well above
        // the test-runner scheduler granularity.
        => Options.Create(new RecoveryHostOptions { GracePollIntervalSeconds = 1 });

    [Fact]
    public async Task StartAsync_DispatchesRecoveryCompleted_WhenStartupPollReturnsIt()
    {
        var coordinator = Substitute.For<IRecoveryCoordinator>();
        var completion  = Substitute.For<IRecoveryCompletionHandler>();
        var evt = NewRecoveryEvent(RecoveryEventType.RecoveryCompleted);
        coordinator.EvaluateGracePeriodAsync(Arg.Any<CancellationToken>())
                   .Returns<RecoveryCompletionResult?>(
                       evt is null ? null : new RecoveryCompletionResult(evt, Array.Empty<TrusteeAttestation>()));

        var svc = new RecoveryGracePollingService(coordinator, completion, FastInterval(),
            NullLogger<RecoveryGracePollingService>.Instance);

        await svc.StartAsync(CancellationToken.None);
        await svc.StopAsync(CancellationToken.None);

        await completion.Received(1).HandleAsync(
            Arg.Is<RecoveryCompletionResult>(r => r != null && r.Event.Type == RecoveryEventType.RecoveryCompleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_DoesNotDispatch_WhenStartupPollReturnsNonCompletedEvent()
    {
        var coordinator = Substitute.For<IRecoveryCoordinator>();
        var completion  = Substitute.For<IRecoveryCompletionHandler>();
        var evt = NewRecoveryEvent(RecoveryEventType.GracePeriodStarted);
        coordinator.EvaluateGracePeriodAsync(Arg.Any<CancellationToken>())
                   .Returns<RecoveryCompletionResult?>(
                       evt is null ? null : new RecoveryCompletionResult(evt, Array.Empty<TrusteeAttestation>()));

        var svc = new RecoveryGracePollingService(coordinator, completion, FastInterval(),
            NullLogger<RecoveryGracePollingService>.Instance);

        await svc.StartAsync(CancellationToken.None);
        await svc.StopAsync(CancellationToken.None);

        await completion.DidNotReceive().HandleAsync(
            Arg.Any<RecoveryCompletionResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_DoesNotDispatch_WhenStartupPollReturnsNull()
    {
        var coordinator = Substitute.For<IRecoveryCoordinator>();
        var completion  = Substitute.For<IRecoveryCompletionHandler>();
        coordinator.EvaluateGracePeriodAsync(Arg.Any<CancellationToken>())
                   .Returns<RecoveryCompletionResult?>((RecoveryCompletionResult?)null);

        var svc = new RecoveryGracePollingService(coordinator, completion, FastInterval(),
            NullLogger<RecoveryGracePollingService>.Instance);

        await svc.StartAsync(CancellationToken.None);
        await svc.StopAsync(CancellationToken.None);

        await completion.DidNotReceive().HandleAsync(
            Arg.Any<RecoveryCompletionResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_AllowsSubsequentStart_WithoutDispatchingStaleEvents()
    {
        var coordinator = Substitute.For<IRecoveryCoordinator>();
        var completion  = Substitute.For<IRecoveryCompletionHandler>();
        coordinator.EvaluateGracePeriodAsync(Arg.Any<CancellationToken>())
                   .Returns<RecoveryCompletionResult?>((RecoveryCompletionResult?)null);

        var svc = new RecoveryGracePollingService(coordinator, completion, FastInterval(),
            NullLogger<RecoveryGracePollingService>.Instance);

        await svc.StartAsync(CancellationToken.None);
        await svc.StopAsync(CancellationToken.None);

        // After Stop, the loop is cancelled; no dispatcher calls.
        await Task.Delay(50);
        await completion.DidNotReceive().HandleAsync(
            Arg.Any<RecoveryCompletionResult>(), Arg.Any<CancellationToken>());

        await svc.DisposeAsync();
    }

    [Fact]
    public async Task LoopTick_DispatchesRecoveryCompleted_AfterFirstInterval()
    {
        // First poll (StartAsync) returns null; second poll (after interval)
        // returns RecoveryCompleted. Verify the handler receives exactly one call.
        var coordinator = Substitute.For<IRecoveryCoordinator>();
        var completion  = Substitute.For<IRecoveryCompletionHandler>();
        var callCount = 0;
        coordinator.EvaluateGracePeriodAsync(Arg.Any<CancellationToken>())
                   .Returns<RecoveryCompletionResult?>(_ =>
                   {
                       callCount++;
                       return callCount == 1
                           ? null
                           : new RecoveryCompletionResult(
                               NewRecoveryEvent(RecoveryEventType.RecoveryCompleted),
                               Array.Empty<TrusteeAttestation>());
                   });

        var svc = new RecoveryGracePollingService(coordinator, completion, FastInterval(),
            NullLogger<RecoveryGracePollingService>.Instance);

        await svc.StartAsync(CancellationToken.None);
        // Wait long enough for at least one loop tick (1.2s > 1s interval).
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        await svc.StopAsync(CancellationToken.None);
        await svc.DisposeAsync();

        await completion.Received().HandleAsync(
            Arg.Is<RecoveryCompletionResult>(r => r != null && r.Event.Type == RecoveryEventType.RecoveryCompleted),
            Arg.Any<CancellationToken>());
    }

    private static RecoveryEvent NewRecoveryEvent(RecoveryEventType type) =>
        new(
            Type: type,
            ActorNodeId: "node-actor",
            TargetNodeId: "node-target",
            OccurredAt: DateTimeOffset.UnixEpoch,
            PreviousEventHash: null,
            Detail: new Dictionary<string, string>());
}
