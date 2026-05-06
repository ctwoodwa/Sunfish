using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// In-process event stream surfacing
/// <see cref="StandingOrderAppliedEvent"/> for consumers that need
/// to react to applied <see cref="StandingOrder"/>s — Helm widgets,
/// permission-resolver caches, feature-management invalidation
/// listeners. Per ADR 0065-A1 §A1.2; structurally parallel to
/// <c>Sunfish.Kernel.Audit.IAuditEventStream</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>In-process only.</b> Cross-process fanout (Bridge → remote
/// Anchor) is the responsibility of ADR 0031 §A1's
/// subscription-event-emitter; this stream stays inside the
/// hosting process.
/// </para>
/// <para>
/// <b>Tenant-scope filtering is the consumer's responsibility.</b>
/// <see cref="Subscribe"/> is intentionally all-tenant — callers
/// MUST filter on
/// <see cref="StandingOrderAppliedEvent.TenantId"/> when their
/// concern is tenant-scoped (e.g., a Helm widget rendering for one
/// tenant's session). The all-tenant default keeps the stream's
/// fanout primitive trivial and makes per-tenant filters a
/// consumer-side decision.
/// </para>
/// <para>
/// <b><see cref="ReplayAll"/> is restart-volatile.</b> The
/// in-memory replay buffer is reset on every process restart. For
/// durable replay, rebuild from
/// <see cref="IStandingOrderRepository.EnumerateAsync"/> filtered to
/// <see cref="StandingOrderState.Validated"/> /
/// <see cref="StandingOrderState.Applied"/>; the event stream is
/// in-process fanout only.
/// </para>
/// <para>
/// <b>Subscribe-then-replay idiom (§A1.6):</b> consumers building a
/// catch-up cache typically (1) call
/// <see cref="Subscribe"/> first, (2) call <see cref="ReplayAll"/>
/// to seed history, (3) deduplicate against a
/// <c>HashSet&lt;StandingOrderId&gt;</c> populated as
/// events arrive — handling the race where a new publish lands
/// between subscribe and replay.
/// </para>
/// </remarks>
public interface IStandingOrderEventStream
{
    /// <summary>
    /// Snapshot of every applied event observed since process start,
    /// in publish order. Restart-volatile; not durable.
    /// </summary>
    IReadOnlyList<StandingOrderAppliedEvent> ReplayAll();

    /// <summary>
    /// Subscribe a callback invoked synchronously for each newly
    /// published event. Returns an <see cref="IDisposable"/> that
    /// unsubscribes on dispose. Subscribers are invoked from the
    /// publishing thread under no lock; long-running handlers
    /// should hand off to a background queue rather than block the
    /// publisher.
    /// </summary>
    IDisposable Subscribe(Action<StandingOrderAppliedEvent> handler);
}
