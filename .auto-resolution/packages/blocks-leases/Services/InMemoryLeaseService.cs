using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Leases.Audit;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Blocks.Leases.Services;

/// <summary>
/// In-memory implementation of <see cref="ILeaseService"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for demos, integration tests, and kitchen-sink scenarios.
/// Not intended for production use — no persistence, no event bus.
/// </summary>
/// <remarks>
/// W#27 Phase 5 wires LeaseDrafted + 5 phase-transition events.
/// W#27 Phases 2+3 (this PR) wire LeaseDocumentVersionAppended +
/// LeasePartySignatureRecorded + LeaseLandlordAttestationSet — all 9
/// AuditEventType constants from kernel-audit are now emitted.
/// </remarks>
public sealed class InMemoryLeaseService : ILeaseService
{
    private readonly ConcurrentDictionary<LeaseId, Lease> _store = new();

    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _auditTenant;
    private readonly ILeaseDocumentVersionLog? _documentVersionLog;

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
        : this(auditTrail, signer, tenantId, documentVersionLog: null) { }

    /// <summary>Creates the service with audit emission + an optional document-version log (W#27 Phase 2). When the log is supplied, <see cref="AppendDocumentVersionAsync"/> persists revisions to it; otherwise the call throws.</summary>
    public InMemoryLeaseService(IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId, ILeaseDocumentVersionLog? documentVersionLog)
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
        _documentVersionLog = documentVersionLog;
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

        // W#27 Phase 3 invariant: AwaitingSignature → Executed only when
        // every tenant has a LeasePartySignature on the latest document
        // version + the landlord has set their attestation.
        if (previous == LeasePhase.AwaitingSignature && newPhase == LeasePhase.Executed)
        {
            EnforceExecutedTransitionGuard(lease);
        }

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

    /// <inheritdoc />
    public async ValueTask<Lease> AppendDocumentVersionAsync(LeaseId id, LeaseDocumentVersion revision, ActorId actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ct.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(id, out var lease))
        {
            throw new InvalidOperationException($"Lease '{id}' not found.");
        }
        if (_documentVersionLog is null)
        {
            throw new InvalidOperationException("Document-version log is required; pass an ILeaseDocumentVersionLog via the 4-arg constructor.");
        }

        // The log assigns the stable id + version number on append.
        var stored = await _documentVersionLog.AppendAsync(revision with { Lease = id }, ct).ConfigureAwait(false);

        var updatedVersions = lease.DocumentVersions.Append(stored.Id).ToArray();
        var updatedLease = lease with { DocumentVersions = updatedVersions };
        _store[id] = updatedLease;

        await EmitAsync(
            AuditEventType.LeaseDocumentVersionAppended,
            new AuditPayload(new Dictionary<string, object?>
            {
                ["lease_id"] = id.Value,
                ["version_id"] = stored.Id.Value,
                ["version_number"] = stored.VersionNumber,
                ["document_hash"] = stored.DocumentHash.ToString(),
                ["authored_by"] = actor.Value,
            }),
            ct).ConfigureAwait(false);

        return updatedLease;
    }

    /// <inheritdoc />
    public async ValueTask<Lease> RecordPartySignatureAsync(LeaseId id, PartyId party, SignatureEventId signatureEvent, ActorId actor, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(id, out var lease))
        {
            throw new InvalidOperationException($"Lease '{id}' not found.");
        }
        if (!lease.Tenants.Contains(party))
        {
            throw new InvalidOperationException($"Party '{party.Value}' is not a tenant on lease '{id}'.");
        }
        if (lease.DocumentVersions.Count == 0)
        {
            throw new InvalidOperationException($"Lease '{id}' has no document version yet; append one before collecting signatures.");
        }

        var latestVersionId = lease.DocumentVersions[^1];
        var signature = new LeasePartySignature
        {
            Id = new LeasePartySignatureId(Guid.NewGuid()),
            Lease = id,
            Party = party,
            SignatureEvent = signatureEvent,
            DocumentVersion = latestVersionId,
            SignedAt = DateTimeOffset.UtcNow,
        };
        var updatedLease = lease with { PartySignatures = lease.PartySignatures.Append(signature).ToArray() };
        _store[id] = updatedLease;

        await EmitAsync(
            AuditEventType.LeasePartySignatureRecorded,
            new AuditPayload(new Dictionary<string, object?>
            {
                ["lease_id"] = id.Value,
                ["signature_id"] = signature.Id.Value,
                ["party"] = party.Value,
                ["signature_event_id"] = signatureEvent.Value,
                ["document_version_id"] = latestVersionId.Value,
                ["actor"] = actor.Value,
            }),
            ct).ConfigureAwait(false);

        return updatedLease;
    }

    /// <inheritdoc />
    public async ValueTask<Lease> SetLandlordAttestationAsync(LeaseId id, SignatureEventId signatureEvent, ActorId actor, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(id, out var lease))
        {
            throw new InvalidOperationException($"Lease '{id}' not found.");
        }

        var updatedLease = lease with { LandlordAttestation = signatureEvent };
        _store[id] = updatedLease;

        await EmitAsync(
            AuditEventType.LeaseLandlordAttestationSet,
            new AuditPayload(new Dictionary<string, object?>
            {
                ["lease_id"] = id.Value,
                ["signature_event_id"] = signatureEvent.Value,
                ["actor"] = actor.Value,
            }),
            ct).ConfigureAwait(false);

        return updatedLease;
    }

    /// <summary>
    /// AwaitingSignature → Executed guard per W#27 hand-off Phase 3.
    /// When the lease has been authored using the document-version flow
    /// (one or more revisions appended), the guard enforces: every
    /// tenant has a LeasePartySignature on the latest revision AND the
    /// landlord has set their attestation. When no revision has been
    /// appended (legacy / simplified flow), the guard is skipped to
    /// preserve backward compatibility with pre-Phase-2 callers.
    /// </summary>
    private static void EnforceExecutedTransitionGuard(Lease lease)
    {
        if (lease.DocumentVersions.Count == 0)
        {
            // Legacy path — no version-tracked authoring; skip the guard.
            return;
        }
        if (lease.LandlordAttestation is null)
        {
            throw new InvalidOperationException(
                $"Lease '{lease.Id}' cannot transition AwaitingSignature → Executed: landlord attestation has not been set. Call SetLandlordAttestationAsync first.");
        }
        var latestVersionId = lease.DocumentVersions[^1];
        foreach (var tenant in lease.Tenants)
        {
            var signedLatest = lease.PartySignatures.Any(s => s.Party == tenant && s.DocumentVersion == latestVersionId);
            if (!signedLatest)
            {
                throw new InvalidOperationException(
                    $"Lease '{lease.Id}' cannot transition AwaitingSignature → Executed: tenant '{tenant.Value}' has not signed the latest document version '{latestVersionId.Value}'.");
            }
        }
    }
}
