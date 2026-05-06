using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Reference <see cref="IQuarterdeckCommandService"/> per ADR 0080
/// §5 + W#51 Phase 2b. Implements the two-phase audit invariant
/// (pre-op intent always; post-op success only on confirmed
/// acknowledgement) on top of the existing
/// <see cref="IPermissionResolver"/> +
/// <see cref="IActorPrincipalResolver"/> seam.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-phase audit (§5):</b>
/// <see cref="AcknowledgeAlertAsync"/> emits
/// <see cref="AuditEventType.AlertAcknowledgementRequested"/> as the
/// FIRST observable side-effect of every call — before tenant
/// binding, before alert-id resolution, before permission resolution,
/// before any First-Aid surface. The intent record makes denials of
/// every kind (tenant-spoofing, unknown-alert probing, permission
/// deny) auditable. The only acceptable reason to skip pre-op
/// emission is audit-infrastructure failure that surfaces as a
/// thrown exception (not a silent catch-and-continue).
/// </para>
/// <para>
/// <b>Authority check (§5):</b> after pre-op audit, the service
/// resolves <see cref="IPermissionResolver"/> for
/// <c>ShipAction.AcknowledgeAlert</c>. Denied → return false (no
/// post-op audit). Granted → emit
/// <see cref="AuditEventType.AlertAcknowledged"/> + return true.
/// Absence of <see cref="AuditEventType.AlertAcknowledged"/> after
/// <see cref="AuditEventType.AlertAcknowledgementRequested"/> in
/// the audit trail IS the failure signal — there is no separate
/// <c>AlertAcknowledgementFailed</c> constant.
/// </para>
/// <para>
/// <b>v1 substrate scope:</b> the service is an audit-emitter +
/// authority gate. The contract surfaces no "alert source's
/// acknowledge path" because <see cref="IQuarterdeckAlertSource"/>
/// is a read-only enumeration in v1; alert-state transitions are
/// projected from the audit trail. Phase 3 may introduce a
/// per-source <c>IAcknowledgeable</c> seam if alert sources need
/// to track ack-state internally; v1's contract is the audit-trail
/// diff.
/// </para>
/// </remarks>
public sealed class DefaultQuarterdeckCommandService : IQuarterdeckCommandService
{
    private readonly IActorPrincipalResolver _actorResolver;
    private readonly IPermissionResolver _permissionResolver;
    private readonly IAuditTrail _auditTrail;
    private readonly IOperationSigner _signer;
    private readonly TimeProvider _time;
    private readonly ILogger<DefaultQuarterdeckCommandService> _logger;

    /// <summary>Construct the service.</summary>
    public DefaultQuarterdeckCommandService(
        IActorPrincipalResolver actorResolver,
        IPermissionResolver permissionResolver,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        ILogger<DefaultQuarterdeckCommandService> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(permissionResolver);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(logger);

        _actorResolver = actorResolver;
        _permissionResolver = permissionResolver;
        _auditTrail = auditTrail;
        _signer = signer;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<bool> AcknowledgeAlertAsync(
        string alertId,
        TenantId tenantId,
        ActorId actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alertId);
        ct.ThrowIfCancellationRequested();

        var occurredAt = _time.GetUtcNow();

        // Step 1 (FIRST observable side-effect, §5 invariant): emit
        // pre-op intent. Audit-infrastructure failure throws —
        // EmitAsync's best-effort catch handles only non-cancellation,
        // non-signature exceptions; signature/cancellation faults
        // propagate to the caller.
        await EmitAsync(
            AuditEventType.AlertAcknowledgementRequested,
            tenantId,
            BuildPayload(alertId, actor, granted: null),
            occurredAt,
            ct).ConfigureAwait(false);

        // Step 2: resolve actor → principal.
        // NOTE: per IActorPrincipalResolver xmldoc the v1 in-memory
        // resolver IGNORES tenantId (reserved for future per-tenant
        // override paths). Cross-tenant probes therefore fall through
        // to the IPermissionResolver gate, which denies on no-matching-
        // role at the target tenant. The pre-op AlertAcknowledgementRequested
        // record above ensures cross-tenant probes are auditable
        // regardless. TODO(W#51-P3): once IActorPrincipalResolver
        // surfaces the principal's home tenant, add an explicit
        // tenantId-binding check here for defence-in-depth.
        var principal = await _actorResolver
            .ResolveAsync(tenantId, actor, ct)
            .ConfigureAwait(false);
        if (principal is null)
        {
            // Fail-closed: actor cannot be resolved → deny. No post-op
            // audit (audit-trail diff IS the failure record).
            return false;
        }

        // Step 3: authority check via IPermissionResolver.
        var decision = await _permissionResolver.ResolveAsync(
            tenantId,
            principal,
            ShipLocation.Quarterdeck,
            DeckDepth.MainDeck,
            ShipAction.AcknowledgeAlert,
            resource: null,
            ct).ConfigureAwait(false);

        if (decision is not PermissionDecision.Granted)
        {
            // Denied (or unknown decision shape) — return false; no
            // post-op event.
            return false;
        }

        // Step 4: post-op success audit. Granted → emit AlertAcknowledged.
        await EmitAsync(
            AuditEventType.AlertAcknowledged,
            tenantId,
            BuildPayload(alertId, actor, granted: true),
            occurredAt,
            ct).ConfigureAwait(false);

        return true;
    }

    private static AuditPayload BuildPayload(string alertId, ActorId actor, bool? granted)
    {
        var body = new Dictionary<string, object?>
        {
            ["alert_id"] = alertId,
            ["actor"] = actor.Value,
        };
        if (granted is { } g)
        {
            body["granted"] = g;
        }
        return new AuditPayload(body);
    }

    private async ValueTask EmitAsync(
        AuditEventType eventType,
        TenantId tenantId,
        AuditPayload payload,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        var nonce = Guid.NewGuid();
        var signed = await _signer.SignAsync(payload, occurredAt, nonce, ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
        try
        {
            await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not AuditSignatureException && ex is not OperationCanceledException)
        {
            // Best-effort: audit-backend hiccups must not deny the
            // ack outcome, but they MUST surface through the host's
            // logging pipeline so SREs can investigate. Cohort
            // precedent: DefaultPermissionResolver.EmitAsync.
            _logger.LogError(ex,
                "Quarterdeck command audit write failed for {EventType}; continuing best-effort",
                eventType);
        }
    }
}
