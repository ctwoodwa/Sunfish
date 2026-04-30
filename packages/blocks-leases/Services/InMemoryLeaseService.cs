using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Leases.Audit;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.Leases.Services;

/// <summary>
/// In-memory implementation of <see cref="ILeaseService"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for demos, integration tests, and kitchen-sink scenarios.
/// Not intended for production use — no persistence, no event bus.
/// </summary>
/// <remarks>
/// W#27 Phase 5: when constructed with <see cref="IAuditTrail"/> +
/// <see cref="IOperationSigner"/> + <see cref="TenantId"/>, every lifecycle
/// event emits an <see cref="AuditRecord"/> per ADR 0049 / ADR 0028.
/// Phase 5 wires emission for the 5 events with shipped operations
/// (LeaseDrafted on Create, LeaseExecuted/LeaseActivated/LeaseRenewed/
/// LeaseTerminated on phase transitions) plus LeaseCancelled. The 3 events
/// for not-yet-shipped operations
/// (<see cref="AuditEventType.LeaseDocumentVersionAppended"/>,
/// <see cref="AuditEventType.LeasePartySignatureRecorded"/>,
/// <see cref="AuditEventType.LeaseLandlordAttestationSet"/>) are declared
/// in kernel-audit for forward compatibility and will be wired when
/// W#27 Phases 2 + 3 ship.
/// </remarks>
public sealed class InMemoryLeaseService : ILeaseService
{
    private readonly ConcurrentDictionary<LeaseId, Lease> _store = new();

    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _auditTenant;

    // W#27 Phase 1: state-machine guards via the public TransitionTable<TState>
    // primitive from blocks-maintenance (ADR 0053 amendment A5).
    private static readonly TransitionTable<LeasePhase> PhaseTransitions =
        new(
        [
            (LeasePhase.Draft,             [LeasePhase.AwaitingSignature, LeasePhase.Cancelled]),
            (LeasePhase.AwaitingSignature, [LeasePhase.Executed, LeasePhase.Cancelled, LeasePhase.Draft]),
            (LeasePhase.Executed,          [LeasePhase.Active]),
            (LeasePhase.Active,            [LeasePhase.Renewed, LeasePhase.Terminated]),
            (LeasePhase.Renewed,           [LeasePhase.Active]),
            // Terminal: Terminated, Cancelled have no outgoing edges.
        ]);

    /// <summary>Creates the service with audit emission disabled.</summary>
    public InMemoryLeaseService()
    {
    }

    /// <summary>Creates the service with audit emission wired through <paramref name="auditTrail"/> + <paramref name="signer"/>; <paramref name="tenantId"/> is the tenant attribution applied to every emitted record.</summary>
    public InMemoryLeaseService(IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        if (tenantId == default)
        {
            throw new ArgumentException("TenantId is required for audit emission.", nameof(tenantId));
        }
        _auditTrail = auditTrail;
        _signer = signer;
        _auditTenant = tenantId;
    }

    private async Task EmitAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null)
        {
            return;
        }

        var occurredAt = DateTimeOffset.UtcNow;
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _auditTenant,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Lease> CreateAsync(CreateLeaseRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var lease = new Lease
        {
            Id = LeaseId.NewId(),
            UnitId = request.UnitId,
            Tenants = request.Tenants,
            Landlord = request.Landlord,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MonthlyRent = request.MonthlyRent,
            Phase = LeasePhase.Draft
        };

        _store[lease.Id] = lease;

        await EmitAsync(
            AuditEventType.LeaseDrafted,
            LeaseAuditPayloadFactory.Drafted(lease, ActorId.System),
            ct).ConfigureAwait(false);

        return lease;
    }

    /// <inheritdoc />
    public ValueTask<Lease?> GetAsync(LeaseId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _store.TryGetValue(id, out var lease);
        return ValueTask.FromResult(lease);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Lease> ListAsync(
        ListLeasesQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var lease in _store.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.Phase.HasValue && lease.Phase != query.Phase.Value)
                continue;

            if (query.TenantId.HasValue && !lease.Tenants.Contains(query.TenantId.Value))
                continue;

            yield return lease;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async ValueTask<Lease> TransitionPhaseAsync(LeaseId id, LeasePhase newPhase, ActorId actor, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(id, out var lease))
        {
            throw new InvalidOperationException($"Lease '{id}' not found.");
        }

        var previous = lease.Phase;
        PhaseTransitions.Guard(previous, newPhase, $"Lease '{id}'");

        var updated = lease with { Phase = newPhase };
        _store[id] = updated;

        var eventType = LeaseAuditPayloadFactory.EventForTransition(previous, newPhase);
        if (eventType.HasValue)
        {
            await EmitAsync(
                eventType.Value,
                LeaseAuditPayloadFactory.PhaseTransition(id, previous, newPhase, actor),
                ct).ConfigureAwait(false);
        }

        return updated;
    }
}
