using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Bridge.Subscription.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Reference Anchor-side handler per ADR 0031-A1.6. Implements the
/// 6-step protocol:
/// <list type="number">
///   <item>Verify HMAC signature (per A1.2 + A1.12.1 grace).</item>
///   <item>Reject events outside the ±5-minute replay window (per A1.2).</item>
///   <item>Deduplicate by event id (per-tenant LRU cache; 24h retention).</item>
///   <item>Update local <c>EditionCapabilities</c> cache via
///         <see cref="IEditionCacheUpdater"/> when supplied.</item>
///   <item>Emit the right audit event (per A1.7).</item>
///   <item>Return the matching <see cref="HandlerResponseStatus"/>.</item>
/// </list>
/// Audit emission is opt-in via the W#32 both-or-neither overload —
/// the audit-disabled overload still does steps 1-4 + 6, just without
/// the audit-trail call.
/// </summary>
public sealed class InMemoryBridgeSubscriptionEventHandler : IBridgeSubscriptionEventHandler
{
    private readonly IEventSigner _signer;
    private readonly ISharedSecretStore _secretStore;
    private readonly IIdempotencyCache _idempotencyCache;
    private readonly ReplayWindow _replayWindow;
    private readonly IEditionCacheUpdater? _editionCacheUpdater;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _operationSigner;
    private readonly TenantId _tenantId;
    private readonly TimeProvider _time;

    /// <summary>Audit-disabled overload (test / bootstrap).</summary>
    public InMemoryBridgeSubscriptionEventHandler(
        IEventSigner signer,
        ISharedSecretStore secretStore,
        IIdempotencyCache idempotencyCache,
        ReplayWindow? replayWindow = null,
        IEditionCacheUpdater? editionCacheUpdater = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(idempotencyCache);
        _signer = signer;
        _secretStore = secretStore;
        _idempotencyCache = idempotencyCache;
        _replayWindow = replayWindow ?? new ReplayWindow();
        _editionCacheUpdater = editionCacheUpdater;
        _time = time ?? TimeProvider.System;
        _auditTrail = null;
        _operationSigner = null;
        _tenantId = default;
    }

    /// <summary>Audit-enabled overload — W#32 both-or-neither contract.</summary>
    public InMemoryBridgeSubscriptionEventHandler(
        IEventSigner signer,
        ISharedSecretStore secretStore,
        IIdempotencyCache idempotencyCache,
        IAuditTrail auditTrail,
        IOperationSigner operationSigner,
        TenantId tenantId,
        ReplayWindow? replayWindow = null,
        IEditionCacheUpdater? editionCacheUpdater = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(idempotencyCache);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(operationSigner);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }
        _signer = signer;
        _secretStore = secretStore;
        _idempotencyCache = idempotencyCache;
        _replayWindow = replayWindow ?? new ReplayWindow();
        _editionCacheUpdater = editionCacheUpdater;
        _time = time ?? TimeProvider.System;
        _auditTrail = auditTrail;
        _operationSigner = operationSigner;
        _tenantId = tenantId;
    }

    /// <inheritdoc />
    public async ValueTask<HandlerResponseStatus> HandleAsync(BridgeSubscriptionEvent evt, string sourceIp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentException.ThrowIfNullOrEmpty(sourceIp);

        // Step 1 — verify HMAC against current OR previous-in-grace secret per A1.12.1
        var lookup = await _secretStore.ResolveAsync(evt.TenantId, ct).ConfigureAwait(false);
        var verified = false;
        if (lookup.Current is not null)
        {
            verified = await _signer.VerifyAsync(evt, lookup.Current, ct).ConfigureAwait(false);
        }
        if (!verified && lookup.PreviousInGrace is not null)
        {
            verified = await _signer.VerifyAsync(evt, lookup.PreviousInGrace, ct).ConfigureAwait(false);
        }
        if (!verified)
        {
            await EmitAsync(
                AuditEventType.BridgeSubscriptionEventSignatureFailed,
                BridgeSubscriptionAuditPayloads.SignatureFailed(evt.TenantId, evt.EventId.ToString(), sourceIp),
                ct).ConfigureAwait(false);
            return HandlerResponseStatus.SignatureFailed;
        }

        // Step 2 — replay window per A1.2
        var receivedAt = _time.GetUtcNow();
        if (!_replayWindow.IsFresh(evt.EffectiveAt, receivedAt))
        {
            await EmitAsync(
                AuditEventType.BridgeSubscriptionEventStale,
                BridgeSubscriptionAuditPayloads.Stale(evt.TenantId, evt.EventType, evt.EventId.ToString(), _replayWindow.SkewSeconds(evt.EffectiveAt, receivedAt)),
                ct).ConfigureAwait(false);
            return HandlerResponseStatus.Stale;
        }

        // Step 3 — idempotency per A1.5
        var alreadyClaimed = await _idempotencyCache.TryClaimAsync(evt.TenantId, evt.EventId, ct).ConfigureAwait(false);
        if (alreadyClaimed)
        {
            // 200 OK; do NOT re-process (Bridge stops retrying).
            return HandlerResponseStatus.AlreadyProcessed;
        }

        // Step 4 — local EditionCapabilities cache update per A1.6
        if (_editionCacheUpdater is not null)
        {
            await _editionCacheUpdater.ApplyAsync(evt, ct).ConfigureAwait(false);
        }

        // Step 5 — audit emission per A1.7
        await EmitAsync(
            AuditEventType.BridgeSubscriptionEventReceived,
            BridgeSubscriptionAuditPayloads.Event(evt.TenantId, evt.EventType, evt.EventId.ToString(), evt.DeliveryAttempt),
            ct).ConfigureAwait(false);

        return HandlerResponseStatus.Ok;
    }

    private async Task EmitAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _operationSigner is null) return;
        var occurredAt = _time.GetUtcNow();
        var signed = await _operationSigner.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Optional callback the Anchor wires through to its
/// <c>IEditionResolver</c> + <c>IMissionEnvelopeProvider</c>
/// (per ADR 0062). The handler invokes this on every successful
/// (verified, fresh, deduped) event, before emitting
/// <see cref="AuditEventType.BridgeSubscriptionEventReceived"/>.
/// </summary>
public interface IEditionCacheUpdater
{
    ValueTask ApplyAsync(BridgeSubscriptionEvent evt, CancellationToken ct = default);
}
