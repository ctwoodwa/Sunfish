using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Scheduling;

/// <summary>
/// Bounds how many gossip rounds can run concurrently per tick across every
/// team the local user belongs to. Required by ADR 0032's
/// "background-sync-all-teams-foreground-render-one" model — without a cap,
/// a user in 4+ teams experiences a network + CPU stampede on every 30-second
/// gossip interval (paper §6.1).
/// </summary>
/// <remarks>
/// <para>
/// This is a pure scheduling primitive: it knows nothing about gossip daemons,
/// <c>TeamContext</c>, or transports. Callers (Wave 6.3's per-team gossip
/// driver) acquire a slot before starting a round and release it by disposing
/// the returned handle when the round completes — on success, failure, or
/// cancellation.
/// </para>
/// <para>
/// The <c>teamId</c> parameter on
/// <see cref="AcquireGossipSlotAsync(TeamId, CancellationToken)"/> is accepted
/// now to reserve the call shape for future per-team throttling (for example,
/// per-team weighting or priority queues). The initial implementation
/// honours only the global <c>MaxActiveRoundsPerTick</c> cap and ignores the
/// team identity.
/// </para>
/// </remarks>
public interface IResourceGovernor
{
    /// <summary>
    /// Acquire one of the bounded concurrent-gossip-round slots. Awaits
    /// availability if the cap is currently saturated; the returned
    /// <see cref="IDisposable"/> releases the slot on <c>Dispose</c> (which is
    /// idempotent — double-dispose is a no-op).
    /// </summary>
    /// <param name="teamId">
    /// The team whose gossip round is about to start. Reserved for future
    /// per-team throttling; the Wave 6.4 implementation does not use it.
    /// </param>
    /// <param name="ct">
    /// Cancelled while waiting for a slot causes the returned task to fault
    /// with <see cref="OperationCanceledException"/>; no slot is acquired.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that completes with a disposable
    /// slot handle. Dispose the handle to release the slot.
    /// </returns>
    ValueTask<IDisposable> AcquireGossipSlotAsync(TeamId teamId, CancellationToken ct);
}
