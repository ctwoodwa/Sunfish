using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Reference implementation of <see cref="IStandingOrderIssuer"/>. Per ADR 0065
/// §3 / §4: runs the validator chain in ascending <see cref="IStandingOrderValidator.Priority"/>,
/// short-circuits the issuance verdict on <see cref="StandingOrderValidationSeverity.Block"/>
/// (state flips to <see cref="StandingOrderState.Rejected"/>), persists every
/// resulting order via <see cref="IStandingOrderRepository.AppendAsync"/>, and
/// emits exactly one <see cref="AuditRecord"/> per issuance / rescission via
/// the supplied <see cref="IAuditTrail"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Audit emission pattern</b> mirrors the cohort precedent (W#34 / W#35 /
/// W#40 / W#41): the issuer holds an <see cref="IOperationSigner"/> and a
/// <see cref="TimeProvider"/>; the <see cref="AuditRecord"/> is constructed
/// directly (per ADR 0065 §A0.3 — the issuer is the construction site, not a
/// pseudo overload).
/// </para>
/// <para>
/// <b>Rescission semantics</b> per ADR 0065 §4: <see cref="RescindAsync"/>
/// emits a new <see cref="AuditEventType.StandingOrderRescinded"/> record
/// without redacting the original <see cref="AuditEventType.StandingOrderIssued"/>
/// record (audit immutability per ADR 0049). The rescinded order's
/// <see cref="StandingOrder.State"/> flips to <see cref="StandingOrderState.Rescinded"/>
/// and is re-appended to the repository.
/// </para>
/// </remarks>
public sealed class DefaultStandingOrderIssuer : IStandingOrderIssuer
{
    private readonly IStandingOrderRepository _repository;
    private readonly IReadOnlyList<IStandingOrderValidator> _validators;
    private readonly IOperationSigner _signer;
    private readonly TimeProvider _time;
    private readonly InMemoryStandingOrderEventStream _eventStream;

    /// <summary>
    /// Construct an issuer bound to a repository, an enumerable of validators
    /// (registered via <see cref="WayfinderServiceExtensions.AddStandingOrderValidator{T}"/>),
    /// an operation signer, a time provider, and the in-process
    /// <see cref="InMemoryStandingOrderEventStream"/> for
    /// <see cref="StandingOrderAppliedEvent"/> fanout.
    /// </summary>
    /// <remarks>
    /// Validators are sorted by ascending <see cref="IStandingOrderValidator.Priority"/>
    /// at construction; ties resolve to registration order (per
    /// <see cref="WayfinderServiceExtensions.AddStandingOrderValidator{T}"/>'s
    /// <c>TryAddEnumerable</c> semantics). The event stream is the
    /// concrete <see cref="InMemoryStandingOrderEventStream"/> rather
    /// than the public <see cref="IStandingOrderEventStream"/>
    /// abstraction so the issuer can call the internal
    /// <c>Publish</c> method per ADR 0065-A1 §A1.5 (the publish surface
    /// is intentionally not on the public interface).
    /// </remarks>
    public DefaultStandingOrderIssuer(
        IStandingOrderRepository repository,
        IEnumerable<IStandingOrderValidator> validators,
        IOperationSigner signer,
        TimeProvider time,
        IStandingOrderEventStream eventStream)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validators);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(eventStream);

        if (eventStream is not InMemoryStandingOrderEventStream concreteStream)
        {
            throw new ArgumentException(
                $"DefaultStandingOrderIssuer requires the {nameof(InMemoryStandingOrderEventStream)} "
                + $"implementation of {nameof(IStandingOrderEventStream)}; the issuer publishes to "
                + "that concrete instance directly to keep the only-the-issuer-publishes invariant "
                + "(ADR 0065-A1 §A1.5).",
                nameof(eventStream));
        }

        _repository = repository;
        _validators = validators.OrderBy(v => (int)v.Priority).ToArray();
        _signer = signer;
        _time = time;
        _eventStream = concreteStream;
    }

    /// <inheritdoc />
    public async Task<StandingOrder> IssueAsync(
        StandingOrderDraft draft,
        ActorId issuedBy,
        IAuditTrail auditTrail,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(auditTrail);

        var occurredAt = _time.GetUtcNow();
        var orderId = new StandingOrderId(Guid.NewGuid());
        var auditId = new AuditRecordId(Guid.NewGuid());

        // Construct a probe order with default state for validator inspection;
        // the realized state is decided by the validator-chain verdict below.
        var probe = new StandingOrder(
            orderId,
            draft.TenantId,
            issuedBy,
            occurredAt,
            draft.Scope,
            draft.Triples,
            draft.Rationale,
            draft.ApprovalChain,
            auditId,
            StandingOrderState.Issued);

        var context = new StandingOrderContext(draft.TenantId, issuedBy);
        var verdict = await RunValidatorChainAsync(probe, context, ct).ConfigureAwait(false);

        // Per ADR 0065 §3: any Block-severity issue rejects; State flips to
        // Rejected; rejection still emits an audit event.
        var realizedState = verdict.Accepted ? StandingOrderState.Validated : StandingOrderState.Rejected;
        var realized = probe with { State = realizedState };

        await _repository.AppendAsync(realized, ct).ConfigureAwait(false);

        var eventType = verdict.Accepted
            ? AuditEventType.StandingOrderIssued
            : AuditEventType.StandingOrderRejected;
        await EmitAuditAsync(auditTrail, eventType, auditId.Value, realized, verdict, occurredAt, ct).ConfigureAwait(false);

        if (verdict.Accepted)
        {
            // ADR 0065-A1 §A1.5 — publish the in-process applied event
            // AFTER the order is persisted + audited. The publish is the
            // last step in the success path; subscribers reading the
            // event can rely on the repository + audit trail already
            // reflecting the applied state. Rejected / Conflicted /
            // Rescinded paths do NOT publish — they fire their own
            // AuditEventType constants observed via IAuditEventStream.
            _eventStream.Publish(new StandingOrderAppliedEvent(
                realized.Id,
                realized.TenantId,
                realized.IssuedBy,
                occurredAt,
                realized.Scope,
                realized.Triples,
                auditId,
                realized.Rationale));
        }

        return realized;
    }

    /// <inheritdoc />
    public async Task<StandingOrder> RescindAsync(
        StandingOrderId id,
        ActorId rescindedBy,
        string rationale,
        IAuditTrail auditTrail,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(rationale);
        ArgumentNullException.ThrowIfNull(auditTrail);

        // Rescission must locate the original order; we walk every known
        // tenant via the repository's enumeration. In practice the caller
        // supplies the tenant via context; this scan is a safety net for
        // the substrate API and is fine at Phase-2 cardinality.
        StandingOrder? original = null;
        await foreach (var candidate in EnumerateAllTenantsAsync(id, ct).ConfigureAwait(false))
        {
            original = candidate;
            break;
        }
        if (original is null)
        {
            throw new InvalidOperationException(
                $"Standing Order with id {id.Value} was not found; cannot rescind.");
        }

        var occurredAt = _time.GetUtcNow();
        var auditId = new AuditRecordId(Guid.NewGuid());
        var rescinded = original with { State = StandingOrderState.Rescinded };

        // Re-append updates the per-tenant log; the original audit record is
        // preserved (ADR 0049 / ADR 0065 §4 audit immutability).
        await _repository.AppendAsync(rescinded, ct).ConfigureAwait(false);

        await EmitRescindAuditAsync(auditTrail, auditId.Value, rescinded, rescindedBy, rationale, occurredAt, ct).ConfigureAwait(false);

        return rescinded;
    }

    private async ValueTask<StandingOrderValidationResult> RunValidatorChainAsync(
        StandingOrder order, StandingOrderContext context, CancellationToken ct)
    {
        var issues = new List<StandingOrderValidationIssue>();
        var blocked = false;

        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(order, context, ct).ConfigureAwait(false);
            issues.AddRange(result.Issues);
            // Honour both signals: any Block-severity issue rejects per
            // ADR 0065 §3, AND a validator that explicitly returns
            // Accepted=false (e.g., for the sub-admin Error→Block reduction
            // documented on StandingOrderValidationResult.Accepted) rejects
            // even if no individual issue carries Block severity.
            if (!result.Accepted ||
                result.Issues.Any(i => i.Severity == StandingOrderValidationSeverity.Block))
            {
                blocked = true;
            }
        }
        return new StandingOrderValidationResult(!blocked, issues);
    }

    private async IAsyncEnumerable<StandingOrder> EnumerateAllTenantsAsync(
        StandingOrderId id,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Per ADR 0065 §1, RescindAsync does not take a TenantId. The
        // Phase 2 substrate scans every materialized tenant via
        // <c>CrdtStandingOrderRepository.KnownTenants</c>; Phase 3a's Atlas
        // projector replaces this with a tenant-aware index.
        if (_repository is not CrdtStandingOrderRepository crdt)
        {
            // Loud failure rather than silent "not found": a host that swaps
            // in a non-CRDT IStandingOrderRepository implementation must also
            // provide a tenant-aware lookup index for rescissions to work.
            // Phase 3a Atlas projector closes this gap.
            throw new InvalidOperationException(
                "DefaultStandingOrderIssuer.RescindAsync requires CrdtStandingOrderRepository " +
                "for the substrate-Phase-2 cross-tenant scan; bind a tenant-aware index " +
                "(planned in Phase 3a Atlas projector) when registering an alternate " +
                "IStandingOrderRepository implementation.");
        }

        foreach (var tenantId in crdt.KnownTenants)
        {
            var found = await _repository.GetAsync(tenantId, id, ct).ConfigureAwait(false);
            if (found is not null)
            {
                yield return found;
                yield break;
            }
        }
    }

    private async Task EmitAuditAsync(
        IAuditTrail auditTrail,
        AuditEventType eventType,
        Guid auditRecordId,
        StandingOrder order,
        StandingOrderValidationResult verdict,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["audit_record_id"] = auditRecordId.ToString("N"),
            ["issued_by"] = order.IssuedBy.Value,
            ["rationale"] = order.Rationale,
            ["scope"] = order.Scope.ToString(),
            ["standing_order_id"] = order.Id.Value.ToString("N"),
            ["state"] = order.State.ToString(),
            ["tenant_id"] = order.TenantId.Value,
            ["triple_count"] = order.Triples.Count,
            ["validation_issue_count"] = verdict.Issues.Count,
        });
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: auditRecordId,
            TenantId: order.TenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    private async Task EmitRescindAuditAsync(
        IAuditTrail auditTrail,
        Guid auditRecordId,
        StandingOrder rescinded,
        ActorId rescindedBy,
        string rationale,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["audit_record_id"] = auditRecordId.ToString("N"),
            ["rationale"] = rationale,
            ["rescinded_by"] = rescindedBy.Value,
            ["scope"] = rescinded.Scope.ToString(),
            ["standing_order_id"] = rescinded.Id.Value.ToString("N"),
            ["tenant_id"] = rescinded.TenantId.Value,
        });
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: auditRecordId,
            TenantId: rescinded.TenantId,
            EventType: AuditEventType.StandingOrderRescinded,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
