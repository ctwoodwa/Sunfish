using Sunfish.Foundation.Recovery;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Host seam for finalizing a recovery once the coordinator emits
/// <see cref="RecoveryEventType.RecoveryCompleted"/>. Anchor's implementation
/// triggers the SQLCipher rekey + audit-log emission per ADR 0046
/// sub-pattern #48f. Other hosts (test doubles, future relay) can substitute.
///
/// Per XO ruling 2026-05-16 §(c): events are not subscribed via a delegate;
/// they're returned synchronously from <see cref="IRecoveryCoordinator"/> method
/// calls. <see cref="RecoveryGracePollingService"/> polls
/// <see cref="IRecoveryCoordinator.EvaluateGracePeriodAsync"/> and invokes this
/// handler when the return is a non-null <see cref="RecoveryEventType.RecoveryCompleted"/>.
/// </summary>
public interface IRecoveryCompletionHandler
{
    /// <summary>
    /// Handle a <see cref="RecoveryEventType.RecoveryCompleted"/> event.
    /// Implementations are expected to be idempotent — the polling service
    /// may dispatch the same event twice if a tight failure window straddles
    /// a restart.
    /// </summary>
    Task HandleAsync(RecoveryEvent completedEvent, CancellationToken cancellationToken);
}
