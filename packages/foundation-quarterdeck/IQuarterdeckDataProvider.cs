using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Contract for the entry-point aggregation surface per ADR 0080 §2.
/// Implementations assemble <see cref="QuarterdeckSnapshot"/> from the
/// active OOD watches, the Mission Envelope, recent Standing Orders,
/// registered alert + KPI sources, and the permission-pre-resolved
/// department link list.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant binding (§5.2):</b> implementations MUST verify that the
/// supplied <c>actor</c>'s tenant matches the supplied <c>tenantId</c>
/// argument before resolving any per-tenant state; cross-tenant calls
/// MUST throw rather than return a permitted-looking-but-wrong snapshot.
/// </para>
/// <para>
/// <b>Permission pre-resolution (§2.3 rule 4):</b> implementations MUST
/// stamp every <see cref="DepartmentLink"/> with the actor's
/// <see cref="DepartmentStatus"/> at snapshot time so the UI never
/// re-resolves permissions; permission cache keys MUST include
/// <c>TenantId</c> per §5.2 anti-spoofing.
/// </para>
/// <para>
/// <b>Subscription contract:</b>
/// <see cref="SubscribeSnapshotAsync"/> emits at the configured
/// <see cref="QuarterdeckOptions.HeartbeatInterval"/> AND on any
/// state change. Permissions are re-resolved on every emit (no cache
/// across emits — required by §2.1 to track watch handovers + alert
/// dismissals + role changes in near-real-time).
/// </para>
/// </remarks>
public interface IQuarterdeckDataProvider
{
    /// <summary>
    /// Assemble + return a single <see cref="QuarterdeckSnapshot"/> for
    /// the supplied actor. Implementations apply the §5.2 tenant
    /// binding check before any state read.
    /// </summary>
    ValueTask<QuarterdeckSnapshot> GetSnapshotAsync(
        TenantId tenantId,
        ActorId actor,
        CancellationToken ct = default);

    /// <summary>
    /// Stream <see cref="QuarterdeckSnapshot"/> values for the
    /// subscriber. Emits at <see cref="QuarterdeckOptions.HeartbeatInterval"/>
    /// AND on state change; permissions re-resolve every emit.
    /// </summary>
    /// <remarks>
    /// Implementations MUST honor <paramref name="ct"/> promptly —
    /// the Quarterdeck is the user's session entry point and stalled
    /// disposal blocks navigation away.
    /// </remarks>
    IAsyncEnumerable<QuarterdeckSnapshot> SubscribeSnapshotAsync(
        TenantId tenantId,
        ActorId actor,
        CancellationToken ct = default);
}
