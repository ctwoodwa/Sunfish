using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.Kernel.Audit;
using KernelAuditRecord = Sunfish.Kernel.Audit.AuditRecord;

namespace Sunfish.Blocks.ShipsOffice;

/// <summary>
/// Reference <see cref="IShipsOfficeCommandService"/> per ADR 0083 §2 + §5 + W#55 Phase 2c.
/// Implements the write-side command surface (Publish + Archive) with the §5 B-2
/// audit-emission ordering invariant: permission FIRST → audit pre-op → execute.
/// </summary>
/// <remarks>
/// <para>
/// <b>§5 ordering (B-2 council finding — load-bearing):</b>
/// <list type="number">
///   <item><description>Resolve current actor via <see cref="IAuditContextProvider"/> + TenantId scope</description></item>
///   <item><description><see cref="IPermissionResolver.ResolveAsync"/> gate</description></item>
///   <item><description>Audit pre-op: <see cref="RequireEmitAsync"/> emitted BEFORE state mutation; propagates on failure (fail-closed)</description></item>
///   <item><description>Execute state change (Phase 2 stub: audit trail is the durable record)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>PublishAsync rejection path:</b> emit <see cref="AuditEventType.ShipsOfficePublishRejected"/>
/// (best-effort) + return <see cref="PublishOutcome.Rejected"/> WITHOUT throwing (per SI-1 + §5).
/// Payload includes a <c>rejection_reason</c> discriminant so post-incident triage can
/// distinguish permission denial, same-actor four-eyes, and unresolvable principal.
/// </para>
/// <para>
/// <b>ArchiveAsync rejection path:</b> throw <see cref="UnauthorizedAccessException"/> with a
/// generic message (no tenant/role detail in the exception to avoid information leakage).
/// NO audit event on denial per §5 informational-only path.
/// </para>
/// </remarks>
internal sealed class ShipsOfficeCommandService : IShipsOfficeCommandService
{
    private const string RejectionReasonPermissionDenied = "permission_denied";
    private const string RejectionReasonPrincipalUnresolved = "principal_unresolved";
    private const string RejectionReasonSameActor = "same_actor";

    private readonly IPermissionResolver _permissionResolver;
    private readonly IActorPrincipalResolver _actorResolver;
    private readonly IAuditContextProvider _actorContext;
    private readonly IAuditTrail _auditTrail;
    private readonly IOperationSigner _signer;
    private readonly IShipsOfficeDataProvider _dataProvider;
    private readonly IOptions<ShipsOfficeOptions> _options;
    private readonly TimeProvider _time;

    public ShipsOfficeCommandService(
        IPermissionResolver permissionResolver,
        IActorPrincipalResolver actorResolver,
        IAuditContextProvider actorContext,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        IShipsOfficeDataProvider dataProvider,
        IOptions<ShipsOfficeOptions> options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(permissionResolver);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(actorContext);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(dataProvider);
        ArgumentNullException.ThrowIfNull(options);
        _permissionResolver = permissionResolver;
        _actorResolver = actorResolver;
        _actorContext = actorContext;
        _auditTrail = auditTrail;
        _signer = signer;
        _dataProvider = dataProvider;
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<PublishOutcome> PublishAsync(
        TenantId tenant,
        ShipsOfficeDocumentId id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Step 1: resolve current actor.
        var actor = _actorContext.GetActor();
        var principal = await _actorResolver.ResolveAsync(tenant, actor, ct).ConfigureAwait(false);
        if (principal is null)
        {
            await TryEmitRejectionAsync(
                actor, principalId: null, id, tenant,
                ShipAction.PublishShipsOfficeDocument,
                RejectionReasonPrincipalUnresolved, ct).ConfigureAwait(false);
            return PublishOutcome.Rejected;
        }

        // Step 2: permission gate (XO+ required at MainDeck per ADR 0083 §4 + §5).
        var decision = await _permissionResolver.ResolveAsync(
            tenant, principal,
            ShipLocation.ShipsOffice, DeckDepth.MainDeck,
            ShipAction.PublishShipsOfficeDocument, resource: null, ct).ConfigureAwait(false);

        if (decision is PermissionDecision.Denied)
        {
            await TryEmitRejectionAsync(
                actor, principal.Id, id, tenant,
                ShipAction.PublishShipsOfficeDocument,
                RejectionReasonPermissionDenied, ct).ConfigureAwait(false);
            return PublishOutcome.Rejected;
        }

        // RequireSecondActorPublish: four-eyes guard (Phase 5 revisit per Open Q4).
        if (_options.Value.RequireSecondActorPublish)
        {
            var snapshot = await _dataProvider.GetSnapshotAsync(tenant, ct).ConfigureAwait(false);
            var doc = snapshot.Documents.FirstOrDefault(d => d.Id == id);
            if (doc is not null && doc.LastModifiedBy == actor)
            {
                await TryEmitRejectionAsync(
                    actor, principal.Id, id, tenant,
                    ShipAction.PublishShipsOfficeDocument,
                    RejectionReasonSameActor, ct).ConfigureAwait(false);
                return PublishOutcome.Rejected;
            }
        }

        // Step 3: audit pre-op — REQUIRED (fail-closed per security council B5 finding).
        // If the audit trail is unavailable, do NOT proceed to state mutation.
        await RequireEmitAsync(
            AuditEventType.ShipsOfficeDocumentPublished,
            actor, principal.Id, id, tenant,
            ShipAction.PublishShipsOfficeDocument, ct).ConfigureAwait(false);

        // Step 4: execute state change.
        // Phase 2 stub: no document store yet — audit trail is the durable record.
        // Revisit in Phase 3 when the Blazor block wires the real document lifecycle.

        return PublishOutcome.Published;
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(
        TenantId tenant,
        ShipsOfficeDocumentId id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Step 1: resolve current actor.
        var actor = _actorContext.GetActor();
        var principal = await _actorResolver.ResolveAsync(tenant, actor, ct).ConfigureAwait(false);
        if (principal is null)
            throw new UnauthorizedAccessException("Archive denied.");

        // Step 2: permission gate — denial THROWS, no audit event per §5 informational-only path.
        // Generic exception message: denial reason deliberately excluded to avoid leaking
        // role/policy information to callers (security council B3 finding).
        var decision = await _permissionResolver.ResolveAsync(
            tenant, principal,
            ShipLocation.ShipsOffice, DeckDepth.MainDeck,
            ShipAction.ArchiveShipsOfficeDocument, resource: null, ct).ConfigureAwait(false);

        if (decision is PermissionDecision.Denied)
            throw new UnauthorizedAccessException("Archive denied.");

        // Step 3: audit pre-op — REQUIRED (fail-closed).
        await RequireEmitAsync(
            AuditEventType.ShipsOfficeDocumentArchived,
            actor, principal.Id, id, tenant,
            ShipAction.ArchiveShipsOfficeDocument, ct).ConfigureAwait(false);

        // Step 4: execute state change.
        // Phase 2 stub: no document store yet — audit trail is the durable record.
    }

    /// <summary>
    /// Emits a pre-operation audit record. MUST NOT swallow exceptions —
    /// if the audit trail is unavailable, the operation must abort (fail-closed
    /// per ADR 0083 §5 B-2 and security council finding B5).
    /// </summary>
    private async Task RequireEmitAsync(
        AuditEventType eventType,
        ActorId actor,
        PrincipalId principalId,
        ShipsOfficeDocumentId docId,
        TenantId tenant,
        ShipAction action,
        CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["actor"] = actor.Value,
            ["principal_id"] = principalId.ToBase64Url(),
            ["document_id"] = docId.Value,
            ["tenant_id"] = tenant.Value,
            ["ship_location"] = nameof(ShipLocation.ShipsOffice),
            ["ship_action"] = action.Name,
        });
        var signed = await _signer.SignAsync(payload, now, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new KernelAuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenant,
            EventType: eventType,
            OccurredAt: now,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Emits a rejection audit record. Best-effort — swallows non-cancellation exceptions
    /// so a degraded audit backend does not promote a denial to an unhandled exception.
    /// </summary>
    private async Task TryEmitRejectionAsync(
        ActorId actor,
        PrincipalId? principalId,
        ShipsOfficeDocumentId docId,
        TenantId tenant,
        ShipAction action,
        string rejectionReason,
        CancellationToken ct)
    {
        try
        {
            var now = _time.GetUtcNow();
            var payload = new AuditPayload(new Dictionary<string, object?>
            {
                ["actor"] = actor.Value,
                ["principal_id"] = principalId?.ToBase64Url(),
                ["document_id"] = docId.Value,
                ["tenant_id"] = tenant.Value,
                ["ship_location"] = nameof(ShipLocation.ShipsOffice),
                ["ship_action"] = action.Name,
                ["rejection_reason"] = rejectionReason,
            });
            var signed = await _signer.SignAsync(payload, now, Guid.NewGuid(), ct).ConfigureAwait(false);
            var record = new KernelAuditRecord(
                AuditId: Guid.NewGuid(),
                TenantId: tenant,
                EventType: AuditEventType.ShipsOfficePublishRejected,
                OccurredAt: now,
                Payload: signed,
                AttestingSignatures: Array.Empty<AttestingSignature>());
            await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: rejection events are already denying the operation;
            // a degraded audit backend must not mask the denial with an exception.
            _ = ex;
        }
    }
}
