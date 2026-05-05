using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Authority surface for OOD watch operations — the public seam consumers
/// drive (Quarterdeck WatchBanner, Engine Room EOOW-check, kernel-audit
/// emissions). Implementations compose <see cref="IOodWatchRepository"/>
/// with audit emission + signing per ADR 0078 §Trust impact.
/// </summary>
public interface IOodWatchService
{
    /// <summary>
    /// Starts a new watch. <see cref="OodWatch.Id"/> is server-generated;
    /// callers MUST NOT supply it.
    /// </summary>
    /// <exception cref="OodWatchConflictException">
    /// An Active watch already exists for the supplied
    /// <paramref name="tenantId"/> + <paramref name="role"/>.
    /// </exception>
    ValueTask<OodWatch> StartWatchAsync(
        TenantId tenantId, ActorId onWatchActor, OodRole role,
        TimeSpan? maxDuration, ActorId requestedBy, CancellationToken ct = default);

    /// <summary>
    /// Formal handover: relieves the current watch and starts a new one
    /// atomically. Returns the (relieved, started) pair.
    /// </summary>
    /// <param name="currentWatchId">The Active watch being relieved.</param>
    /// <param name="incomingActor">The actor who will hold the new watch.</param>
    /// <param name="requestedBy">The actor invoking the handover (typically the relieving authority or the outgoing watch-keeper).</param>
    /// <param name="handoverKind">
    /// Distinguishes <see cref="OodHandoverKind.Voluntary"/> shift-changes
    /// (severity "Normal") from <see cref="OodHandoverKind.CommandRelieved"/>
    /// authority-ordered reliefs (severity "High"). Per W#49 P2 amendment R3.
    /// </param>
    /// <param name="reason">Free-form context written into the audit payload; may be null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="OodWatchConflictException">
    /// <paramref name="currentWatchId"/> is not in
    /// <see cref="OodWatchState.Active"/> at the time of the call.
    /// </exception>
    ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
        OodWatchId currentWatchId, ActorId incomingActor,
        ActorId requestedBy, OodHandoverKind handoverKind, string? reason,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the Active watch for <paramref name="tenantId"/> +
    /// <paramref name="role"/>, or <c>null</c> if none. Returns null for
    /// Relieved or Expired watches.
    /// </summary>
    ValueTask<OodWatch?> GetActiveWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default);
}
