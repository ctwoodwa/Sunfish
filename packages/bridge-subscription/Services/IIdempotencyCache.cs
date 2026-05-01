using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Per-tenant idempotency cache for Anchor-side de-duplication of
/// Bridge subscription events per ADR 0031-A1.2. Bridge MAY re-deliver
/// the same event (HTTP failure, network blip, etc.); Anchor de-
/// duplicates on the <see cref="BridgeSubscriptionEvent.EventId"/>
/// field.
/// </summary>
/// <remarks>
/// A1.5 specifies 24-hour retention as the default. Implementations are
/// per-process (in-memory LRU); worst-case duplicate processing across
/// process restarts is acceptable per ADR 0031 (the dedup is a
/// flood-guard, not a correctness invariant).
/// </remarks>
public interface IIdempotencyCache
{
    /// <summary>
    /// Atomically: returns true if <paramref name="eventId"/> has already
    /// been seen for <paramref name="tenantId"/>; otherwise records it +
    /// returns false. Repeated calls with the same key return true until
    /// the entry ages out per the 24-hour retention window.
    /// </summary>
    ValueTask<bool> TryClaimAsync(string tenantId, Guid eventId, CancellationToken ct = default);
}
