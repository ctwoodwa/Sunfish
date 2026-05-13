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
///   <item><description>Resolve current actor via <see cref="IAuditContextProvider"/> + verify TenantId scope</description></item>
///   <item><description><see cref="IPermissionResolver.ResolveAsync"/> gate</description></item>
///   <item><description>Audit pre-op: emit BEFORE state mutation</description></item>
///   <item><description>Execute state change (Phase 2 stub: audit trail is the durable record)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>PublishAsync rejection path:</b> emit <see cref="AuditEventType.ShipsOfficePublishRejected"/>
/// + return <see cref="PublishOutcome.Rejected"/> WITHOUT throwing (per SI-1 + §5).
/// </para>
/// <para>
/// <b>ArchiveAsync rejection path:</b> throw <see cref="UnauthorizedAccessException"/>
/// with NO audit event (informational-only path per §5).
/// </para>
/// <para>
/// <b>RequireSecondActorPublish:</b> when <see cref="ShipsOfficeOptions.RequireSecondActorPublish"/>
/// is true, a publish attempt by the same actor who last modified the document is treated
/// as a rejection (four-eyes pattern). Phase 5 revisit per Open Q4 deferral.
/// </para>
/// </remarks>
internal sealed class ShipsOfficeCommandService : IShipsOfficeCommandService
{
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
            await TryEmitAsync(AuditEventType.ShipsOfficePublishRejected, actor, id, tenant, ct).ConfigureAwait(false);
            return PublishOutcome.Rejected;
        }

        // Step 2: permission gate (XO+ required at MainDeck per §4 + ADR 0083 §5).
        var decision = await _permissionResolver.ResolveAsync(
            tenant, principal,
            ShipLocation.ShipsOffice, DeckDepth.MainDeck,
            ShipAction.PublishShipsOfficeDocument, resource: null, ct).ConfigureAwait(false);

        if (decision is PermissionDecision.Denied)
        {
            await TryEmitAsync(AuditEventType.ShipsOfficePublishRejected, actor, id, tenant, ct).ConfigureAwait(false);
            return PublishOutcome.Rejected;
        }

        // RequireSecondActorPublish: four-eyes guard (Phase 5 revisit per Open Q4).
        if (_options.Value.RequireSecondActorPublish)
        {
            var snapshot = await _dataProvider.GetSnapshotAsync(tenant, ct).ConfigureAwait(false);
            var doc = snapshot.Documents.FirstOrDefault(d => d.Id == id);
            if (doc is not null && doc.LastModifiedBy == actor)
            {
                await TryEmitAsync(AuditEventType.ShipsOfficePublishRejected, actor, id, tenant, ct).ConfigureAwait(false);
                return PublishOutcome.Rejected;
            }
        }

        // Step 3: audit pre-op — emitted BEFORE state mutation per ADR 0083 §5 B-2.
        await TryEmitAsync(AuditEventType.ShipsOfficeDocumentPublished, actor, id, tenant, ct).ConfigureAwait(false);

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
            throw new UnauthorizedAccessException(
                $"Archive denied: actor '{actor.Value}' could not be resolved to a principal in tenant '{tenant.Value}'.");

        // Step 2: permission gate (XO+ required at MainDeck per §4 + ADR 0083 §5).
        // ArchiveAsync denial THROWS — no audit event per §5 informational-only path.
        var decision = await _permissionResolver.ResolveAsync(
            tenant, principal,
            ShipLocation.ShipsOffice, DeckDepth.MainDeck,
            ShipAction.ArchiveShipsOfficeDocument, resource: null, ct).ConfigureAwait(false);

        if (decision is PermissionDecision.Denied denied)
            throw new UnauthorizedAccessException(
                $"Archive denied: {denied.Reason}. Remediation: {denied.Remediation}.");

        // Step 3: audit pre-op — emitted BEFORE state mutation per ADR 0083 §5 B-2.
        await TryEmitAsync(AuditEventType.ShipsOfficeDocumentArchived, actor, id, tenant, ct).ConfigureAwait(false);

        // Step 4: execute state change.
        // Phase 2 stub: no document store yet — audit trail is the durable record.
        // Revisit in Phase 3 when the Blazor block wires the real document lifecycle.
    }

    private async Task TryEmitAsync(
        AuditEventType eventType,
        ActorId actor,
        ShipsOfficeDocumentId docId,
        TenantId tenant,
        CancellationToken ct)
    {
        try
        {
            var now = _time.GetUtcNow();
            var payload = new AuditPayload(new Dictionary<string, object?>
            {
                ["actor"] = actor.Value,
                ["document_id"] = docId.Value,
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort audit emission per cohort precedent (W#50 P2 + W#52 P2).
            // Audit-backend hiccups MUST NOT block the user-facing permission flow.
            _ = ex;
        }
    }
}
