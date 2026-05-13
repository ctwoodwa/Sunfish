using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Wayfinder;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Anchor-specific <see cref="IOodWatchService"/> stub.
/// <see cref="GetActiveWatchAsync"/> always returns <c>null</c> (no EOOW on
/// watch) — correct for the Anchor demo where no formal watch-standing is
/// in effect. Write operations throw <see cref="NotSupportedException"/>
/// because Anchor v1 does not support formal watch management via this surface.
/// </summary>
/// <remarks>
/// W#49 shipped <see cref="DefaultOodWatchService"/> + repository seams.
/// Anchor will register a real watch service when the Quarterdeck Blazor UI
/// (W#51 Phase 4) ships its full ship-level service composition.
/// </remarks>
internal sealed class AnchorNoOpOodWatchService : IOodWatchService
{
    /// <inheritdoc />
    public ValueTask<OodWatch?> GetActiveWatchAsync(
        TenantId tenantId,
        OodRole role,
        CancellationToken ct = default)
        => ValueTask.FromResult<OodWatch?>(null);

    /// <inheritdoc />
    public ValueTask<OodWatch> StartWatchAsync(
        TenantId tenantId,
        ActorId onWatchActor,
        OodRole role,
        TimeSpan? maxDuration,
        ActorId requestedBy,
        CancellationToken ct = default)
        => throw new NotSupportedException(
            "Anchor v1 does not support watch management via this surface.");

    /// <inheritdoc />
    public ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
        OodWatchId currentWatchId,
        ActorId incomingActor,
        ActorId requestedBy,
        OodHandoverKind handoverKind,
        string? reason,
        CancellationToken ct = default)
        => throw new NotSupportedException(
            "Anchor v1 does not support watch handover via this surface.");
}
