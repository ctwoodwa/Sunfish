using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Audit;

/// <summary>
/// Per ADR 0064-A1.7 — central audit emitter for the regulatory substrate.
/// Implements W#34 P4 dedup pattern (ConcurrentDictionary keyed on a
/// per-event tuple with per-event time window) so event floods don't drown
/// the audit trail.
/// </summary>
/// <remarks>
/// <para>
/// Consumers create one emitter per host (W#32 both-or-neither — both
/// trail and signer must be present) and pass it into the service
/// constructors via the audit-enabled overloads. Audit-disabled
/// constructors omit the emitter and the services emit no records.
/// </para>
/// <para>
/// <b>Dedup windows (per event, per A1.7):</b>
/// <list type="bullet">
/// <item><description><c>PolicyEnforcementBlocked</c> — 1 hour, keyed on (feature_key, jurisdiction_code, rule_id).</description></item>
/// <item><description><c>JurisdictionProbedWithLowConfidence</c> — 1 hour, keyed on (jurisdiction_code).</description></item>
/// <item><description><c>DataResidencyViolation</c> — 1 hour, keyed on (record_class, jurisdiction_code).</description></item>
/// <item><description><c>SanctionsScreeningHit</c> — 1 hour, keyed on (subject_id, list_source).</description></item>
/// <item><description><c>RegimeAcknowledgmentSurfaced</c> — 24 hours, keyed on (regime).</description></item>
/// </list>
/// Other events are unrate-limited (<c>PolicyEvaluated</c> = always-on telemetry;
/// <c>EuAiActTierClassified</c> / <c>RegulatoryRuleContentReloaded</c> /
/// <c>RegulatoryPolicyCacheInvalidated</c> = rare).
/// </para>
/// </remarks>
public sealed class RegulatoryAuditEmitter
{
    /// <summary>1-hour dedup window for high-frequency rule + violation events.</summary>
    public static readonly TimeSpan DefaultRuleDedupWindow = TimeSpan.FromHours(1);

    /// <summary>24-hour dedup window for regime-acknowledgment surfacing.</summary>
    public static readonly TimeSpan DefaultRegimeDedupWindow = TimeSpan.FromHours(24);

    private readonly IAuditTrail _trail;
    private readonly IOperationSigner _signer;
    private readonly TenantId _tenantId;
    private readonly TimeProvider _time;
    private readonly TimeSpan _ruleDedupWindow;
    private readonly TimeSpan _regimeDedupWindow;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _dedup = new();

    public RegulatoryAuditEmitter(
        IAuditTrail trail,
        IOperationSigner signer,
        TenantId tenantId,
        TimeProvider? time = null,
        TimeSpan? ruleDedupWindow = null,
        TimeSpan? regimeDedupWindow = null)
    {
        ArgumentNullException.ThrowIfNull(trail);
        ArgumentNullException.ThrowIfNull(signer);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }
        _trail = trail;
        _signer = signer;
        _tenantId = tenantId;
        _time = time ?? TimeProvider.System;
        _ruleDedupWindow = ruleDedupWindow ?? DefaultRuleDedupWindow;
        _regimeDedupWindow = regimeDedupWindow ?? DefaultRegimeDedupWindow;
    }

    /// <summary>Emit without dedup. Use for always-on telemetry events.</summary>
    public Task EmitAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
        => EmitInternalAsync(eventType, payload, ct);

    /// <summary>
    /// Emit with per-key dedup over the rule-event window (1 hour by default).
    /// Returns true when the event fires; false when suppressed by dedup.
    /// </summary>
    public async Task<bool> EmitWithRuleDedupAsync(
        string dedupKey,
        AuditEventType eventType,
        AuditPayload payload,
        CancellationToken ct)
    {
        if (!ShouldEmit(dedupKey, _ruleDedupWindow)) return false;
        await EmitInternalAsync(eventType, payload, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Emit with per-key dedup over the regime-event window (24 hours by default).
    /// Returns true when the event fires; false when suppressed by dedup.
    /// </summary>
    public async Task<bool> EmitWithRegimeDedupAsync(
        string dedupKey,
        AuditEventType eventType,
        AuditPayload payload,
        CancellationToken ct)
    {
        if (!ShouldEmit(dedupKey, _regimeDedupWindow)) return false;
        await EmitInternalAsync(eventType, payload, ct).ConfigureAwait(false);
        return true;
    }

    private bool ShouldEmit(string dedupKey, TimeSpan window)
    {
        var now = _time.GetUtcNow();
        var fire = false;
        _dedup.AddOrUpdate(
            dedupKey,
            _ =>
            {
                fire = true;
                return now;
            },
            (_, last) =>
            {
                if (now - last >= window)
                {
                    fire = true;
                    return now;
                }
                return last;
            });
        return fire;
    }

    private async Task EmitInternalAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        var occurredAt = _time.GetUtcNow();
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _trail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
