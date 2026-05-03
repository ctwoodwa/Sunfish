using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Versioning.Audit;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Reference <see cref="IVersionVectorIncompatibility"/> per ADR 0028-A6.4 +
/// A7.4. Honours the A7.4 dedup windows in-process: 1-hour rolling window
/// for incompatibility rejections, 24-hour for legacy reconnects. Worst-
/// case duplicate emission across process restarts is acceptable per A7.4
/// (the dedup is a flood-guard, not a correctness invariant).
/// </summary>
/// <remarks>
/// <para>
/// Audit dependencies follow the W#32 both-or-neither pattern: when
/// <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/> are both
/// supplied (audit-enabled overload), every non-deduped record emits an
/// <see cref="AuditRecord"/>. The audit-disabled overload is for
/// test/bootstrap; the dedup state still tracks but no record fires.
/// </para>
/// </remarks>
public sealed class InMemoryVersionVectorIncompatibility : IVersionVectorIncompatibility
{
    /// <summary>Default dedup window for <see cref="AuditEventType.VersionVectorIncompatibilityRejected"/> per A7.4.</summary>
    public static readonly TimeSpan DefaultRejectionDedupWindow = TimeSpan.FromHours(1);

    /// <summary>Default dedup window for <see cref="AuditEventType.LegacyDeviceReconnected"/> per A7.4.</summary>
    public static readonly TimeSpan DefaultLegacyReconnectDedupWindow = TimeSpan.FromHours(24);

    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _tenantId;
    private readonly TimeProvider _time;
    private readonly TimeSpan _rejectionDedupWindow;
    private readonly TimeSpan _legacyReconnectDedupWindow;

    private readonly ConcurrentDictionary<RejectionDedupKey, DateTimeOffset> _rejectionLastSeen = new();
    private readonly ConcurrentDictionary<LegacyReconnectDedupKey, DateTimeOffset> _legacyLastSeen = new();

    /// <summary>Audit-disabled overload (test / bootstrap). Dedup state is still tracked.</summary>
    public InMemoryVersionVectorIncompatibility(TimeProvider? time = null)
    {
        _auditTrail = null;
        _signer = null;
        _tenantId = default;
        _time = time ?? TimeProvider.System;
        _rejectionDedupWindow = DefaultRejectionDedupWindow;
        _legacyReconnectDedupWindow = DefaultLegacyReconnectDedupWindow;
    }

    /// <summary>Audit-enabled overload — both audit trail and signer required together.</summary>
    public InMemoryVersionVectorIncompatibility(
        IAuditTrail auditTrail,
        IOperationSigner signer,
        TenantId tenantId,
        TimeProvider? time = null,
        TimeSpan? rejectionDedupWindow = null,
        TimeSpan? legacyReconnectDedupWindow = null)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }
        _auditTrail = auditTrail;
        _signer = signer;
        _tenantId = tenantId;
        _time = time ?? TimeProvider.System;
        _rejectionDedupWindow = rejectionDedupWindow ?? DefaultRejectionDedupWindow;
        _legacyReconnectDedupWindow = legacyReconnectDedupWindow ?? DefaultLegacyReconnectDedupWindow;
    }

    /// <inheritdoc />
    public async ValueTask RecordRejectionAsync(string remoteNodeId, VersionVectorVerdict verdict, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(remoteNodeId);
        ArgumentNullException.ThrowIfNull(verdict);
        if (verdict.Verdict != VerdictKind.Incompatible || verdict.FailedRule is null)
        {
            throw new ArgumentException("RecordRejectionAsync requires an Incompatible verdict with a non-null FailedRule.", nameof(verdict));
        }

        var key = new RejectionDedupKey(remoteNodeId, verdict.FailedRule.Value, verdict.FailedRuleDetail ?? string.Empty);
        var now = _time.GetUtcNow();
        if (_rejectionLastSeen.TryGetValue(key, out var lastSeen) && now - lastSeen < _rejectionDedupWindow)
        {
            return;
        }
        _rejectionLastSeen[key] = now;

        await EmitAuditAsync(
            AuditEventType.VersionVectorIncompatibilityRejected,
            VersionVectorAuditPayloads.IncompatibilityRejected(remoteNodeId, verdict.FailedRule.Value, verdict.FailedRuleDetail ?? string.Empty),
            now,
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RecordLegacyReconnectAsync(string remoteNodeId, string remoteKernel, int kernelMinorLag, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(remoteNodeId);
        ArgumentException.ThrowIfNullOrEmpty(remoteKernel);

        var key = new LegacyReconnectDedupKey(remoteNodeId, kernelMinorLag);
        var now = _time.GetUtcNow();
        if (_legacyLastSeen.TryGetValue(key, out var lastSeen) && now - lastSeen < _legacyReconnectDedupWindow)
        {
            return;
        }
        _legacyLastSeen[key] = now;

        await EmitAuditAsync(
            AuditEventType.LegacyDeviceReconnected,
            VersionVectorAuditPayloads.LegacyReconnected(remoteNodeId, remoteKernel, kernelMinorLag),
            now,
            ct).ConfigureAwait(false);
    }

    private async ValueTask EmitAuditAsync(AuditEventType eventType, AuditPayload payload, DateTimeOffset occurredAt, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null)
        {
            return;
        }

        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    private readonly record struct RejectionDedupKey(string RemoteNodeId, FailedRule Rule, string Detail);
    private readonly record struct LegacyReconnectDedupKey(string RemoteNodeId, int KernelMinorLag);
}
