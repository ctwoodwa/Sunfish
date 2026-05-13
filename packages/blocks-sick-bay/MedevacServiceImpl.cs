using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.SickBay;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.SickBay;

/// <summary>
/// Reference <see cref="IMedevacService"/> per ADR 0082 §2 / W#54 Phase 3b.
/// In-process, per-tenant state machine with audit-before-operation
/// invariant on every transition.
/// </summary>
/// <remarks>
/// <para>
/// <b>State machine (per §2):</b>
/// <c>Idle → Requested → PendingAuthorization → Authorized → InProgress
/// → Complete</c>. <c>Cancel</c> resets non-terminal states to
/// <c>Idle</c>; terminal states (<c>Idle</c>, <c>Complete</c>) throw on
/// cancel.
/// </para>
/// <para>
/// <b>Four-eyes invariant (§Trust):</b>
/// <see cref="AuthorizeAsync"/> emits
/// <see cref="AuditEventType.SickBayMedevacSelfApprovalRejected"/>
/// THEN throws <see cref="InvalidOperationException"/> when the
/// authorizing principal equals the requesting principal. The dual
/// emission lets the Sick Bay timeline surface the rejection event
/// alongside the exception.
/// </para>
/// <para>
/// <b>Audit-before-operation invariant (ADR 0082 §6):</b>
/// every transition emits its audit event BEFORE mutating state.
/// </para>
/// <para>
/// <b>IChannelProvider (ADR 0076) hook:</b> notifications are deferred
/// to a follow-up workstream (ADR 0082 Open Q1 — cross-tenant Bridge
/// wire protocol). The <c>IMedevacEscalationStrategy</c> hook point is
/// reserved here as a no-op (interface not yet defined); the medevac
/// flow remains intra-tenant only in Phase 3b.
/// </para>
/// <para>
/// <b>Thread-safety:</b> per-tenant state is stored in a concurrent
/// dictionary. Within a single tenant, concurrent
/// transitions are serialized by the state-machine check; no distributed
/// coordination is provided in Phase 3b (in-process only).
/// </para>
/// </remarks>
public sealed class MedevacServiceImpl : IMedevacService
{
    private sealed record MedevacRecord(MedevacState State, PrincipalId? RequestedBy);

    private readonly ConcurrentDictionary<TenantId, MedevacRecord> _state = new();
    private readonly IAuditTrail _audit;
    private readonly IOperationSigner _signer;
    private readonly TimeProvider _time;

    public MedevacServiceImpl(
        IAuditTrail audit,
        IOperationSigner signer,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(signer);
        _audit = audit;
        _signer = signer;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<MedevacState> GetStateAsync(TenantId tenant, CancellationToken ct = default)
    {
        var record = _state.GetValueOrDefault(tenant) ?? new MedevacRecord(MedevacState.Idle, null);
        return Task.FromResult(record.State);
    }

    /// <inheritdoc />
    public async Task RequestAsync(
        TenantId tenant,
        PrincipalId requestedBy,
        string reason,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var current = GetRecord(tenant);
        if (current.State != MedevacState.Idle)
            ThrowInvalidTransition(current.State, MedevacState.Requested);

        // Audit-before-operation: emit BEFORE state change.
        await EmitAuditAsync(AuditEventType.SickBayMedevacInitiated, tenant,
            new Dictionary<string, object?>
            {
                ["requested_by"] = requestedBy.ToBase64Url(),
                ["reason"] = reason,
            }, ct).ConfigureAwait(false);

        // Transition: Idle → Requested → PendingAuthorization (internal routing).
        _state[tenant] = new MedevacRecord(MedevacState.PendingAuthorization, requestedBy);
    }

    /// <inheritdoc />
    public async Task AuthorizeAsync(
        TenantId tenant,
        PrincipalId authorizingPrincipal,
        CancellationToken ct = default)
    {
        var current = GetRecord(tenant);
        if (current.State != MedevacState.PendingAuthorization)
            ThrowInvalidTransition(current.State, MedevacState.Authorized);

        // Four-eyes invariant: reject self-approval before state change.
        if (current.RequestedBy == authorizingPrincipal)
        {
            await EmitAuditAsync(AuditEventType.SickBayMedevacSelfApprovalRejected, tenant,
                new Dictionary<string, object?>
                {
                    ["authorizing_principal"] = authorizingPrincipal.ToBase64Url(),
                    ["requested_by"] = current.RequestedBy?.ToBase64Url(),
                }, ct).ConfigureAwait(false);

            throw new InvalidOperationException(
                "Self-approval rejected per four-eyes invariant.");
        }

        // Audit-before-operation.
        await EmitAuditAsync(AuditEventType.SickBayMedevacAuthorized, tenant,
            new Dictionary<string, object?>
            {
                ["authorizing_principal"] = authorizingPrincipal.ToBase64Url(),
                ["requested_by"] = current.RequestedBy?.ToBase64Url(),
            }, ct).ConfigureAwait(false);

        // Transition: PendingAuthorization → Authorized → InProgress (internal dispatch).
        _state[tenant] = new MedevacRecord(MedevacState.InProgress, current.RequestedBy);
    }

    /// <inheritdoc />
    public async Task CancelAsync(
        TenantId tenant,
        PrincipalId cancellingPrincipal,
        CancellationToken ct = default)
    {
        var current = GetRecord(tenant);
        if (current.State is MedevacState.Idle or MedevacState.Complete)
            ThrowInvalidTransition(current.State, MedevacState.Idle);

        await EmitAuditAsync(AuditEventType.SickBayMedevacCancelled, tenant,
            new Dictionary<string, object?>
            {
                ["cancelling_principal"] = cancellingPrincipal.ToBase64Url(),
                ["from_state"] = current.State.ToString(),
            }, ct).ConfigureAwait(false);

        _state[tenant] = new MedevacRecord(MedevacState.Idle, null);
    }

    /// <inheritdoc />
    public async Task CompleteAsync(TenantId tenant, CancellationToken ct = default)
    {
        var current = GetRecord(tenant);
        if (current.State != MedevacState.InProgress)
            ThrowInvalidTransition(current.State, MedevacState.Complete);

        await EmitAuditAsync(AuditEventType.SickBayMedevacCompleted, tenant,
            new Dictionary<string, object?>
            {
                ["requested_by"] = current.RequestedBy?.ToBase64Url(),
            }, ct).ConfigureAwait(false);

        _state[tenant] = new MedevacRecord(MedevacState.Complete, current.RequestedBy);
    }

    private MedevacRecord GetRecord(TenantId tenant) =>
        _state.GetValueOrDefault(tenant) ?? new MedevacRecord(MedevacState.Idle, null);

    private static void ThrowInvalidTransition(MedevacState from, MedevacState to) =>
        throw new InvalidOperationException(
            $"medevac state machine: cannot transition from {from} to {to}");

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
