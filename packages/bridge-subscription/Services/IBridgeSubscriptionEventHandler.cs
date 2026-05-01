using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Anchor-side handler contract per ADR 0031-A1.6. Bridge POSTs (or
/// pushes via SSE) a signed <see cref="BridgeSubscriptionEvent"/>;
/// the handler verifies the signature, deduplicates by event id,
/// applies the local <c>EditionCapabilities</c> change, emits the
/// audit event, and triggers the ADR 0062 envelope-change.
/// </summary>
public interface IBridgeSubscriptionEventHandler
{
    /// <summary>
    /// Processes a received event. Returns <see cref="HandlerResponseStatus"/>
    /// matching the HTTP status the host should reply with (200 on
    /// success, 401 on signature failure, 200 on idempotent
    /// re-delivery — Bridge MUST treat 200 as "stop retrying").
    /// </summary>
    ValueTask<HandlerResponseStatus> HandleAsync(BridgeSubscriptionEvent evt, string sourceIp, CancellationToken ct = default);
}

/// <summary>The terminal outcome of <see cref="IBridgeSubscriptionEventHandler.HandleAsync"/>.</summary>
public enum HandlerResponseStatus
{
    /// <summary>Verified, deduplicated, processed; the host should reply 200.</summary>
    Ok,

    /// <summary>HMAC signature verification failed; the host should reply 401.</summary>
    SignatureFailed,

    /// <summary><see cref="BridgeSubscriptionEvent.EffectiveAt"/> outside ±5min window; the host should reply 410 Gone.</summary>
    Stale,

    /// <summary>Repeat delivery of an already-processed event id; the host should reply 200 (idempotent).</summary>
    AlreadyProcessed,
}
