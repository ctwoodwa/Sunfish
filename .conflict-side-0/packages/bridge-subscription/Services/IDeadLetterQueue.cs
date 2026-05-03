using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Dead-letter queue for events that exhausted the
/// <see cref="WebhookRetryPolicy"/> attempts. Per ADR 0031-A1.5, these
/// require operator review (typically: Anchor-side webhook is
/// misconfigured, the URL is unreachable, or the shared secret has
/// drifted from rotation).
/// </summary>
public interface IDeadLetterQueue
{
    /// <summary>Records <paramref name="evt"/> + a terminal-failure reason in the DLQ.</summary>
    ValueTask EnqueueAsync(BridgeSubscriptionEvent evt, string reason, CancellationToken ct = default);

    /// <summary>Returns every dead-lettered event for <paramref name="tenantId"/>.</summary>
    ValueTask<IReadOnlyList<DeadLetterEntry>> GetByTenantAsync(string tenantId, CancellationToken ct = default);
}

/// <summary>One DLQ entry — the original event + the terminal-failure reason.</summary>
public sealed record DeadLetterEntry(BridgeSubscriptionEvent Event, string Reason, System.DateTimeOffset DeadLetteredAt);
