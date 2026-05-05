using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Persistence boundary for <see cref="OodWatch"/> records — implementations
/// own atomicity (the single-Active-per-(tenant, role) invariant must be
/// enforced under a transaction or equivalent locking strategy). Per
/// ADR 0078 §1.
/// </summary>
public interface IOodWatchRepository
{
    /// <summary>Returns the Active watch for <paramref name="tenantId"/> + <paramref name="role"/>, or null if none.</summary>
    ValueTask<OodWatch?> GetCurrentWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default);

    /// <summary>
    /// Atomically inserts a new Active watch. Implementations MUST throw
    /// <see cref="OodWatchConflictException"/> when an Active watch already
    /// exists for the same (TenantId, OodRole) pair.
    /// </summary>
    ValueTask<OodWatch> StartWatchAsync(
        TenantId tenantId, ActorId onWatchActor, OodRole role,
        TimeSpan? maxDuration, ActorId startedBy, CancellationToken ct = default);

    /// <summary>Transitions the watch with id <paramref name="watchId"/> from Active to <see cref="OodWatchState.Relieved"/>.</summary>
    ValueTask<OodWatch> RelieveWatchAsync(
        OodWatchId watchId, ActorId relievedBy, CancellationToken ct = default);

    /// <summary>
    /// Atomically relieves <paramref name="currentWatchId"/> and starts a new
    /// watch for <paramref name="incomingActor"/> in the same (TenantId, OodRole)
    /// scope as the relieved watch. Implementations MUST enforce transactional
    /// atomicity — if the start-leg fails, the relieve-leg MUST be rolled
    /// back so the (TenantId, OodRole) pair never enters an authority-vacuum
    /// state. Per ADR 0078 §2 + W#49 P2 council Finding 3.
    /// </summary>
    /// <exception cref="OodWatchConflictException">
    /// <paramref name="currentWatchId"/> is not in <see cref="OodWatchState.Active"/>.
    /// </exception>
    ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
        OodWatchId currentWatchId, ActorId incomingActor, ActorId requestedBy,
        CancellationToken ct = default);

    /// <summary>Transitions the watch from Active to <see cref="OodWatchState.Expired"/>.</summary>
    ValueTask<OodWatch> ExpireWatchAsync(
        OodWatchId watchId, CancellationToken ct = default);

    /// <summary>
    /// Streams every watch (any state) for the supplied (tenant, role)
    /// whose <see cref="OodWatch.StartedAt"/> falls within
    /// <paramref name="from"/>..<paramref name="to"/>.
    /// </summary>
    IAsyncEnumerable<OodWatch> GetWatchHistoryAsync(
        TenantId tenantId, OodRole role,
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);

    // R4 (XO post-merge council 2026-05-06): the cross-tenant sweep
    // enumerator now lives on the internal IOodWatchSweepRepository so
    // application code cannot accidentally enumerate Active watches across
    // tenants. Concrete repository implementations typically implement
    // both interfaces.
}
