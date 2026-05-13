using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Quarterdeck;
using Sunfish.Foundation.Tactical;
using Sunfish.Kernel.Audit;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Blocks.Tactical;

/// <summary>
/// <see cref="IQuarterdeckAlertSource"/> implementation that surfaces
/// Tactical Lookout alerts onto the Quarterdeck ticker per ADR 0081 §7.2
/// (W#52 Phase 4). Registered as <c>SourceName = "sunfish.tactical.lookout"</c>;
/// the <c>"sunfish.*"</c> prefix is reserved for first-party sources per
/// ADR 0080 §5.3.
/// </summary>
/// <remarks>
/// Tenant verification is performed at the source boundary before delegating
/// to <see cref="ILookout.GetActiveLookoutAlerts"/>. On tenant mismatch the
/// source returns an empty sequence and emits
/// <see cref="AuditEventType.TacticalAuthorizationDenied"/> with
/// <c>denialReason="tenant-mismatch"</c> (when <see cref="IAuditTrail"/> is
/// registered). Defense-in-depth: each mapped alert is re-checked against the
/// requested tenant before emission. At most 50 alerts are returned per call,
/// sorted <see cref="TacticalAlert.DetectedAt"/> descending.
/// </remarks>
public sealed class LookoutQuarterdeckAlertSource : IQuarterdeckAlertSource
{
    private readonly ILookout _lookout;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly ILogger<LookoutQuarterdeckAlertSource>? _logger;

    private const int MaxAlerts = 50;

    public LookoutQuarterdeckAlertSource(
        ILookout lookout,
        ITenantContext tenantContext,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null,
        ILogger<LookoutQuarterdeckAlertSource>? logger = null)
    {
        _lookout = lookout;
        _tenantContext = tenantContext;
        _auditTrail = auditTrail;
        _signer = signer;
        _logger = logger;
    }

    /// <inheritdoc />
    public string SourceName => "sunfish.tactical.lookout";

    /// <inheritdoc />
    public async IAsyncEnumerable<QuarterdeckAlert> GetAlertsAsync(
        TenantId tenantId,
        ActorId actor,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Tenant verification at source boundary before touching any per-tenant state.
        var ambient = _tenantContext.Tenant;
        if (ambient is null || ambient.Id != tenantId)
        {
            await EmitTenantMismatchAuditAsync(tenantId, ct).ConfigureAwait(false);
            yield break;
        }

        IReadOnlyList<TacticalAlert> raw;
        try
        {
            raw = _lookout.GetActiveLookoutAlerts(tenantId);
        }
        catch (TacticalUnauthorizedException)
        {
            // ILookout performed its own tenant check and rejected — already audited there.
            yield break;
        }

        var count = 0;
        // Sort DetectedAt DESC and cap at MaxAlerts.
        var sorted = SortedByDetectedAtDesc(raw);
        foreach (var alert in sorted)
        {
            if (count >= MaxAlerts) yield break;
            ct.ThrowIfCancellationRequested();

            // Defense-in-depth: re-verify tenant on each alert.
            if (alert.TenantId != tenantId) continue;

            yield return MapToQuarterdeckAlert(alert);
            count++;
        }
    }

    private static QuarterdeckAlert MapToQuarterdeckAlert(TacticalAlert alert)
    {
        var isAcknowledged = alert.Status == AlertStatus.Acknowledged;
        return new QuarterdeckAlert(
            AlertId: alert.AlertId,
            TenantId: alert.TenantId,
            Severity: MapSeverity(alert.Severity),
            Title: alert.Title,
            Summary: string.IsNullOrEmpty(alert.Summary) ? null : alert.Summary,
            IssuedAt: alert.DetectedAt,
            ExpiresAt: null,
            RequiresAcknowledgement: alert.RequiresAcknowledgement,
            IsAcknowledged: isAcknowledged,
            AcknowledgedBy: isAcknowledged ? alert.AcknowledgedBy?.ToString() : null,
            AcknowledgedAt: isAcknowledged ? alert.AcknowledgedAt : null,
            SourceName: SourceNameConstant,
            VisibilityPolicy: AlertVisibilityPolicy.OmitForDeniedActors);
    }

    // Named constant for use inside the class (SourceName is instance property).
    private const string SourceNameConstant = "sunfish.tactical.lookout";

    private static Sunfish.Foundation.Quarterdeck.AlertSeverity MapSeverity(
        Sunfish.Foundation.Tactical.AlertSeverity severity) =>
        severity switch
        {
            Sunfish.Foundation.Tactical.AlertSeverity.Critical      => Sunfish.Foundation.Quarterdeck.AlertSeverity.Emergency,
            Sunfish.Foundation.Tactical.AlertSeverity.High          => Sunfish.Foundation.Quarterdeck.AlertSeverity.High,
            Sunfish.Foundation.Tactical.AlertSeverity.Medium        => Sunfish.Foundation.Quarterdeck.AlertSeverity.Normal,
            Sunfish.Foundation.Tactical.AlertSeverity.Low           => Sunfish.Foundation.Quarterdeck.AlertSeverity.Normal,
            Sunfish.Foundation.Tactical.AlertSeverity.Informational => Sunfish.Foundation.Quarterdeck.AlertSeverity.Informational,
            _ => Sunfish.Foundation.Quarterdeck.AlertSeverity.Normal,
        };

    private static IEnumerable<TacticalAlert> SortedByDetectedAtDesc(IReadOnlyList<TacticalAlert> alerts)
    {
        // Copy + sort — no Span/stackalloc; lists are small (≤50 after cap).
        var list = new List<TacticalAlert>(alerts);
        list.Sort(static (a, b) => b.DetectedAt.CompareTo(a.DetectedAt));
        return list;
    }

    private async System.Threading.Tasks.ValueTask EmitTenantMismatchAuditAsync(
        TenantId tenantId, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null) return;
        try
        {
            var occurredAt = DateTimeOffset.UtcNow;
            var payload = new AuditPayload(new Dictionary<string, object?>
            {
                ["denialReason"] = "tenant-mismatch",
                ["requestedTenantId"] = tenantId.ToString(),
                ["ambientTenantId"] = _tenantContext.Tenant?.Id.ToString() ?? "(none)",
                ["source"] = SourceNameConstant,
            });
            var nonce = Guid.NewGuid();
            var signed = await _signer.SignAsync(payload, occurredAt, nonce, ct).ConfigureAwait(false);
            var record = new AuditRecord(
                AuditId: Guid.NewGuid(),
                TenantId: tenantId,
                EventType: AuditEventType.TacticalAuthorizationDenied,
                OccurredAt: occurredAt,
                Payload: signed,
                AttestingSignatures: Array.Empty<AttestingSignature>());
            await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "LookoutQuarterdeckAlertSource: audit append failed for tenant-mismatch denial on {TenantId}; continuing best-effort.",
                tenantId);
        }
    }
}
