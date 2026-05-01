using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Reference <see cref="IFeatureForceEnableSurface"/> per ADR
/// 0062-A1.2 + A1.9. In-memory store of force-enable records
/// keyed on <c>(featureKey, dimension)</c>; rejects force-enables
/// targeting <see cref="ForceEnablePolicy.NotOverridable"/>
/// dimensions per A1.9.
/// </summary>
/// <remarks>
/// <para>
/// Operator-role authorization is the host's responsibility (typically
/// wired via an authorization middleware that verifies the caller is
/// in the operator role before invoking <see cref="RequestAsync"/>).
/// This surface only enforces the per-dimension policy — it doesn't
/// know who's calling.
/// </para>
/// <para>
/// Audit emission per A1.9: every successful <see cref="RequestAsync"/>
/// emits <see cref="AuditEventType.FeatureForceEnabled"/>; every rejected
/// request emits <see cref="AuditEventType.FeatureForceEnableRejected"/>;
/// every <see cref="RevokeAsync"/> emits
/// <see cref="AuditEventType.FeatureForceRevoked"/>.
/// </para>
/// </remarks>
public sealed class DefaultFeatureForceEnableSurface : IFeatureForceEnableSurface
{
    private readonly ConcurrentDictionary<(string FeatureKey, DimensionChangeKind Dimension), ForceEnableRecord> _records = new();
    private readonly TimeProvider _time;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _tenantId;

    /// <summary>Audit-disabled overload (test / bootstrap).</summary>
    public DefaultFeatureForceEnableSurface(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Audit-enabled overload — W#32 both-or-neither contract.</summary>
    public DefaultFeatureForceEnableSurface(
        IAuditTrail auditTrail,
        IOperationSigner signer,
        TenantId tenantId,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }
        _time = time ?? TimeProvider.System;
        _auditTrail = auditTrail;
        _signer = signer;
        _tenantId = tenantId;
    }

    /// <inheritdoc />
    public async ValueTask<ForceEnableRecord> RequestAsync(FeatureForceEnableRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var policy = ForceEnablePolicyResolver.ResolveFor(request.Dimension);
        if (policy == ForceEnablePolicy.NotOverridable)
        {
            await EmitAsync(
                AuditEventType.FeatureForceEnableRejected,
                BuildRejectedPayload(request),
                ct).ConfigureAwait(false);
            throw new ForceEnableNotPermittedException(request.Dimension);
        }

        var record = new ForceEnableRecord
        {
            FeatureKey = request.FeatureKey,
            Dimension = request.Dimension,
            OperatorPrincipalId = request.OperatorPrincipalId,
            RequestedAt = _time.GetUtcNow(),
            ExpiresAt = request.ExpiresAt,
            Reason = request.Reason,
        };
        _records[(request.FeatureKey, request.Dimension)] = record;

        await EmitAsync(
            AuditEventType.FeatureForceEnabled,
            BuildEnabledPayload(record),
            ct).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc />
    public async ValueTask RevokeAsync(string featureKey, DimensionChangeKind dimension, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureKey);
        ct.ThrowIfCancellationRequested();
        if (_records.TryRemove((featureKey, dimension), out var revoked))
        {
            await EmitAsync(
                AuditEventType.FeatureForceRevoked,
                BuildRevokedPayload(featureKey, dimension, revoked.OperatorPrincipalId),
                ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ValueTask<ForceEnableRecord?> ResolveAsync(string featureKey, DimensionChangeKind dimension, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureKey);
        ct.ThrowIfCancellationRequested();

        if (!_records.TryGetValue((featureKey, dimension), out var record))
        {
            return ValueTask.FromResult<ForceEnableRecord?>(null);
        }

        // Honor the optional ExpiresAt — expired records are removed
        // lazily on access.
        if (record.ExpiresAt is { } exp && _time.GetUtcNow() >= exp)
        {
            _records.TryRemove((featureKey, dimension), out _);
            return ValueTask.FromResult<ForceEnableRecord?>(null);
        }
        return ValueTask.FromResult<ForceEnableRecord?>(record);
    }

    private static AuditPayload BuildEnabledPayload(ForceEnableRecord record) =>
        new(new System.Collections.Generic.Dictionary<string, object?>
        {
            ["dimension"] = record.Dimension.ToString(),
            ["expires_at"] = record.ExpiresAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["feature_key"] = record.FeatureKey,
            ["operator_principal_id"] = record.OperatorPrincipalId,
            ["reason"] = record.Reason,
        });

    private static AuditPayload BuildRejectedPayload(FeatureForceEnableRequest request) =>
        new(new System.Collections.Generic.Dictionary<string, object?>
        {
            ["dimension"] = request.Dimension.ToString(),
            ["feature_key"] = request.FeatureKey,
            ["operator_principal_id"] = request.OperatorPrincipalId,
            ["policy"] = ForceEnablePolicy.NotOverridable.ToString(),
            ["reason"] = request.Reason,
        });

    private static AuditPayload BuildRevokedPayload(string featureKey, DimensionChangeKind dimension, string operatorPrincipalId) =>
        new(new System.Collections.Generic.Dictionary<string, object?>
        {
            ["dimension"] = dimension.ToString(),
            ["feature_key"] = featureKey,
            ["operator_principal_id"] = operatorPrincipalId,
        });

    private async Task EmitAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null) return;
        var occurredAt = _time.GetUtcNow();
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
}
