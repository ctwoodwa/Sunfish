using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Recovery;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Anchor's <see cref="IRecoveryCompletionHandler"/> implementation. When the
/// coordinator emits <see cref="RecoveryEventType.RecoveryCompleted"/>, the
/// host's job is to:
///
///   1. Generate a new SQLCipher key material set on the new device.
///   2. Re-encrypt the LocalFirst encrypted store with the new key
///      (PRAGMA rekey via IEncryptedStore — surface deferred).
///   3. Emit a kernel-audit record so the rekey is forensically visible
///      (ADR 0049 sub-pattern #48f — surface deferred).
///   4. Broadcast the new device identity (sync substrate concern — deferred).
///
/// **Phase 2 scope:** wire the IHostedService polling pipeline + the
/// completion-handler seam. Logs the event and exits cleanly. Steps 2 + 3
/// + 4 are stubbed because they require:
///
///   - An `IEncryptedStore.RotateKeyAsync` surface that doesn't exist today
///     (would be an api-change to foundation-localfirst).
///   - A per-session signer accessor (same gap blocking ApproveRecoveryPage;
///     see cob-question 2026-05-16T04-42Z-kernel-security-session-signer-accessor).
///   - A typed `RecoveryRekey` audit event + payload schema.
///
/// Once those land, this handler drops the stub comments and wires the
/// real rekey path. The polling-service + seam shipped here are forward-
/// compatible: the interface contract doesn't change.
/// </summary>
internal sealed class AnchorRecoveryCompletionHandler : IRecoveryCompletionHandler
{
    private readonly ILogger<AnchorRecoveryCompletionHandler> _logger;

    public AnchorRecoveryCompletionHandler(ILogger<AnchorRecoveryCompletionHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task HandleAsync(RecoveryEvent completedEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completedEvent);

        _logger.LogInformation(
            "Recovery completion detected (actor={ActorNodeId}, target={TargetNodeId}, occurredAt={OccurredAt}). "
            + "SQLCipher rekey + audit emission deferred to follow-up — see comments in "
            + "AnchorRecoveryCompletionHandler for the prerequisite surfaces.",
            completedEvent.ActorNodeId,
            completedEvent.TargetNodeId,
            completedEvent.OccurredAt);

        // TODO (post-W#63): IEncryptedStore.RotateKeyAsync(...)
        //                   IAuditTrail.AppendAsync(new AuditRecord(...))
        //                   ISyncDaemon.AnnounceIdentityRotation(...)

        return Task.CompletedTask;
    }
}
