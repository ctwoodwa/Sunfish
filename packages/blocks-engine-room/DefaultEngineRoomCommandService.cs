using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.EngineRoom;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.EngineRoom;

/// <summary>
/// Reference <see cref="IEngineRoomCommandService"/> per ADR 0079 §2 +
/// W#50 Phase 2b. Implements the §Trust audit-emission ordering invariant:
/// pre-op <c>*Requested</c> audit BEFORE permission resolution, denial
/// audit on rejection, post-op <c>*ed</c> audit after successful
/// persistence. Includes EOOW presence check (non-blocking advisory) per
/// hand-off §Trust line 156.
/// </summary>
/// <remarks>
/// <para>
/// <b>Actor resolution (cohort precedent — Quarterdeck):</b>
/// <see cref="IActorPrincipalResolver"/> converts <see cref="ActorId"/>
/// to <see cref="Principal"/> before the <see cref="IPermissionResolver"/>
/// call. A null resolve result is treated as fail-closed: deny without
/// post-op audit. <see cref="EngineRoomUnauthorizedException"/> is thrown
/// with <see cref="AuditEventType.DamageControlAuthorizationDenied"/>
/// emitted.
/// </para>
/// <para>
/// <b>EOOW check (per hand-off §2 line 156):</b> a missing EOOW watch
/// emits <see cref="AuditEventType.EngineRoomHealthDegraded"/> as a
/// warning-level advisory but does NOT block the command. Damage Control
/// operations remain callable when no EOOW is on watch (e.g., emergency
/// manual override). Future phases may introduce a stricter policy.
/// </para>
/// </remarks>
public sealed class DefaultEngineRoomCommandService : IEngineRoomCommandService
{
    private readonly IDocumentQuarantineStore _store;
    private readonly IActorPrincipalResolver _actorResolver;
    private readonly IPermissionResolver _permissionResolver;
    private readonly IOodWatchService _oodWatch;
    private readonly IAuditTrail _auditTrail;
    private readonly IOperationSigner _signer;
    private readonly TimeProvider _time;
    private readonly ILogger<DefaultEngineRoomCommandService> _logger;

    /// <summary>Construct the default command service.</summary>
    public DefaultEngineRoomCommandService(
        IDocumentQuarantineStore store,
        IActorPrincipalResolver actorResolver,
        IPermissionResolver permissionResolver,
        IOodWatchService oodWatch,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        ILogger<DefaultEngineRoomCommandService>? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(permissionResolver);
        ArgumentNullException.ThrowIfNull(oodWatch);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);

        _store = store;
        _actorResolver = actorResolver;
        _permissionResolver = permissionResolver;
        _oodWatch = oodWatch;
        _auditTrail = auditTrail;
        _signer = signer;
        _logger = logger ?? NullLogger<DefaultEngineRoomCommandService>.Instance;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<QuarantineResult> QuarantineDocumentAsync(
        string documentId,
        TenantId tenantId,
        ActorId requestedBy,
        string reason,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(reason);
        ct.ThrowIfCancellationRequested();

        var occurredAt = _time.GetUtcNow();

        // Step 1 (FIRST): pre-op audit per §Trust ordering invariant.
        // Fail-stop: if audit storage fails the operation aborts; a forensic gap on pre-op is not acceptable.
        await EmitPreOpAsync(
            AuditEventType.DocumentQuarantineRequested,
            tenantId,
            BuildPayload(documentId, requestedBy, reason: reason),
            occurredAt,
            ct).ConfigureAwait(false);

        // Step 2: EOOW advisory check (non-blocking).
        await CheckEoowAsync(tenantId, ct).ConfigureAwait(false);

        // Step 3: resolve actor → principal, then authorize.
        await AuthorizeAsync(
            tenantId, requestedBy, ShipAction.QuarantineDocument,
            resource: new Resource(documentId),
            occurredAt, ct).ConfigureAwait(false);

        // Step 4: persist via store.
        var result = await _store.QuarantineAsync(documentId, tenantId, requestedBy, reason, ct)
            .ConfigureAwait(false);

        // Step 5: post-op audit.
        await EmitAsync(
            AuditEventType.DocumentQuarantined,
            tenantId,
            BuildPayload(documentId, requestedBy, reason: reason),
            occurredAt,
            ct).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<ReleaseResult> ReleaseQuarantineAsync(
        string documentId,
        TenantId tenantId,
        ActorId requestedBy,
        string reason,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(reason);
        ct.ThrowIfCancellationRequested();

        var occurredAt = _time.GetUtcNow();

        // Step 1: pre-op audit (fail-stop — see QuarantineDocumentAsync comment).
        await EmitPreOpAsync(
            AuditEventType.DocumentQuarantineReleaseRequested,
            tenantId,
            BuildPayload(documentId, requestedBy, reason: reason),
            occurredAt,
            ct).ConfigureAwait(false);

        // Step 2: EOOW advisory check (non-blocking).
        await CheckEoowAsync(tenantId, ct).ConfigureAwait(false);

        // Step 3: authorize.
        await AuthorizeAsync(
            tenantId, requestedBy, ShipAction.ReleaseQuarantine,
            resource: new Resource(documentId),
            occurredAt, ct).ConfigureAwait(false);

        // Step 4: persist.
        var result = await _store.ReleaseAsync(documentId, tenantId, requestedBy, ct)
            .ConfigureAwait(false);

        // Step 5: post-op audit.
        await EmitAsync(
            AuditEventType.DocumentQuarantineReleased,
            tenantId,
            BuildPayload(documentId, requestedBy, reason: reason),
            occurredAt,
            ct).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<CompactionResult> CompactDocumentAsync(
        string documentId,
        TenantId tenantId,
        ActorId requestedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ct.ThrowIfCancellationRequested();

        var occurredAt = _time.GetUtcNow();

        // Step 1: pre-op audit (fail-stop — see QuarantineDocumentAsync comment).
        await EmitPreOpAsync(
            AuditEventType.ManualCompactionInitiated,
            tenantId,
            BuildPayload(documentId, requestedBy, reason: null),
            occurredAt,
            ct).ConfigureAwait(false);

        // Step 2: EOOW advisory check (non-blocking).
        await CheckEoowAsync(tenantId, ct).ConfigureAwait(false);

        // Step 3: authorize.
        await AuthorizeAsync(
            tenantId, requestedBy, ShipAction.CompactDocument,
            resource: new Resource(documentId),
            occurredAt, ct).ConfigureAwait(false);

        // Step 4: persist. Store throws InvalidOperationException if not eligible —
        // per interface contract (CompactDocumentAsync throws InvalidOperationException
        // on eligibility failure, NOT EngineRoomUnauthorizedException).
        var result = await _store.CompactAsync(documentId, tenantId, requestedBy, ct)
            .ConfigureAwait(false);

        // Step 5: post-op audit.
        await EmitAsync(
            AuditEventType.ManualCompactionCompleted,
            tenantId,
            BuildPayload(documentId, requestedBy, reason: null),
            occurredAt,
            ct).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// EOOW advisory check: if no EOOW watch is active, emits
    /// <see cref="AuditEventType.EngineRoomHealthDegraded"/> into the audit
    /// trail (§Trust forensic record) and does NOT block the command. Per
    /// hand-off §Trust line 156 and ADR 0079.
    /// </summary>
    private async ValueTask CheckEoowAsync(TenantId tenantId, CancellationToken ct)
    {
        try
        {
            var watch = await _oodWatch
                .GetActiveWatchAsync(tenantId, OodRole.EngineeringOfficerOfTheWatch, ct)
                .ConfigureAwait(false);

            if (watch is null)
            {
                _logger.LogWarning(
                    "No active EOOW watch for tenant {TenantId}; Damage Control command proceeding without EOOW oversight.",
                    tenantId);
                // §Trust: EOOW absence must be on the forensic audit trail, not just logs.
                await EmitAsync(
                    AuditEventType.EngineRoomHealthDegraded,
                    tenantId,
                    new AuditPayload(new Dictionary<string, object?>
                    {
                        ["reason"] = "no_active_eoow_watch",
                    }),
                    _time.GetUtcNow(),
                    ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // EOOW check failure MUST NOT block the command — log and continue.
            _logger.LogWarning(ex,
                "EOOW watch check threw for tenant {TenantId}; Damage Control command proceeding.",
                tenantId);
            // §Trust: the failure itself must be on the forensic trail, not just logs.
            await EmitAsync(
                AuditEventType.EngineRoomHealthDegraded,
                tenantId,
                new AuditPayload(new Dictionary<string, object?>
                {
                    ["reason"] = "eoow_check_threw",
                }),
                _time.GetUtcNow(),
                ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolves the actor to a principal, then checks the permission gate.
    /// Emits <see cref="AuditEventType.DamageControlAuthorizationDenied"/>
    /// and throws <see cref="EngineRoomUnauthorizedException"/> on denial.
    /// </summary>
    private async ValueTask AuthorizeAsync(
        TenantId tenantId,
        ActorId requestedBy,
        ShipAction action,
        Resource? resource,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        var principal = await _actorResolver
            .ResolveAsync(tenantId, requestedBy, ct)
            .ConfigureAwait(false);

        var documentId = resource?.Id ?? string.Empty;

        if (principal is null)
        {
            await EmitDenialAsync(tenantId, requestedBy, action, documentId, occurredAt, ct)
                .ConfigureAwait(false);
            throw new EngineRoomUnauthorizedException(
                $"Actor {requestedBy.Value} could not be resolved to a principal for tenant {tenantId}.");
        }

        var decision = await _permissionResolver.ResolveAsync(
            tenantId,
            principal,
            ShipLocation.EngineRoom,
            DeckDepth.BelowTheWaterline,
            action,
            resource,
            ct).ConfigureAwait(false);

        if (decision is not PermissionDecision.Granted)
        {
            await EmitDenialAsync(tenantId, requestedBy, action, documentId, occurredAt, ct)
                .ConfigureAwait(false);
            throw new EngineRoomUnauthorizedException(
                $"Actor {requestedBy.Value} is not authorized to perform {action} for tenant {tenantId}.");
        }
    }

    private async ValueTask EmitDenialAsync(
        TenantId tenantId,
        ActorId requestedBy,
        ShipAction action,
        string documentId,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        await EmitAsync(
            AuditEventType.DamageControlAuthorizationDenied,
            tenantId,
            new AuditPayload(new Dictionary<string, object?>
            {
                ["document_id"] = documentId,
                ["actor"] = requestedBy.Value,
                ["action"] = action.Name,
            }),
            occurredAt,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Fail-stop audit emit for pre-op events. Unlike <see cref="EmitAsync"/>,
    /// any storage failure propagates to the caller — aborting the operation
    /// before any side-effects occur is the correct §Trust behaviour when the
    /// forensic record of intent cannot be written.
    /// </summary>
    private async ValueTask EmitPreOpAsync(
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
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    private static AuditPayload BuildPayload(string documentId, ActorId requestedBy, string? reason)
    {
        var body = new Dictionary<string, object?>
        {
            ["document_id"] = documentId,
            ["actor"] = requestedBy.Value,
        };
        if (reason is not null)
        {
            body["reason"] = reason;
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
            _logger.LogError(ex,
                "Engine Room audit append failed for event {EventType} on tenant {TenantId}; continuing best-effort.",
                eventType, tenantId);
        }
    }
}
