using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.SickBay;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.SickBay;

/// <summary>
/// Reference implementation of <see cref="ISickBayCommandService"/> per
/// ADR 0082 §2 / W#54 Phase 3b.
/// </summary>
/// <remarks>
/// <para>
/// <b>Permission gate (Phase 3b design note):</b>
/// <see cref="ISickBayCommandService.TriggerKeyRotationAsync"/> does not
/// carry actor context (<c>PrincipalId</c>, <c>ShipLocation</c>,
/// <c>DeckDepth</c>). Per the interface contract, callers MUST
/// pre-authorize via <c>IPermissionResolver</c> before invoking this
/// method. The service-level permission gate is deferred to a follow-up
/// contract amendment — flagged for security council review (precedent:
/// <c>ITacticalCommandService</c> takes <c>Principal actor</c>
/// explicitly).
/// </para>
/// <para>
/// <b>Audit-before-operation invariant (ADR 0082 §6):</b>
/// <see cref="AuditEventType.SickBayKeyRotationTriggered"/> is emitted
/// BEFORE <see cref="IKeyRotationScheduler.ScheduleAsync"/> is called.
/// If the audit trail is unavailable, the exception propagates without
/// side-effects.
/// </para>
/// <para>
/// <b>IKeyRotationScheduler wiring:</b> W#32 / ADR 0046-A2 exposes no
/// public scheduling API at Phase 3b authoring time.
/// <see cref="NoopKeyRotationScheduler"/> remains the active
/// implementation; real wiring is deferred to the substrate follow-up.
/// </para>
/// </remarks>
public sealed class SickBayCommandService : ISickBayCommandService
{
    private readonly IAuditTrail _audit;
    private readonly IOperationSigner _signer;
    private readonly IKeyRotationScheduler _scheduler;
    private readonly TimeProvider _time;

    public SickBayCommandService(
        IAuditTrail audit,
        IOperationSigner signer,
        IKeyRotationScheduler scheduler,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(scheduler);
        _audit = audit;
        _signer = signer;
        _scheduler = scheduler;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task TriggerKeyRotationAsync(
        TenantId tenant,
        string fieldPurpose,
        string triggerReason,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fieldPurpose);
        ArgumentNullException.ThrowIfNull(triggerReason);

        // Audit-before-operation: emit BEFORE calling scheduler (ADR 0082 §6).
        await EmitAuditAsync(
            AuditEventType.SickBayKeyRotationTriggered,
            tenant,
            new Dictionary<string, object?>
            {
                ["field_purpose"] = fieldPurpose,
                ["trigger_reason"] = triggerReason,
            },
            ct).ConfigureAwait(false);

        await _scheduler.ScheduleAsync(tenant, fieldPurpose, triggerReason, ct)
            .ConfigureAwait(false);
    }

    private async Task EmitAuditAsync(
        AuditEventType eventType,
        TenantId tenantId,
        IReadOnlyDictionary<string, object?> body,
        CancellationToken ct)
    {
        var occurredAt = _time.GetUtcNow();
        var payload = new AuditPayload(body);
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct)
            .ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
        await _audit.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
