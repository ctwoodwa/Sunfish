using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Maintenance.Audit;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// In-memory implementation of <see cref="IMaintenanceService"/> backed by
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> stores.
/// </summary>
/// <remarks>
/// <para>
/// State-mutating operations (<see cref="TransitionRequestAsync"/>, <see cref="TransitionWorkOrderAsync"/>,
/// <see cref="AcceptQuoteAsync"/>) are serialized via per-entity <see cref="SemaphoreSlim"/> instances
/// so concurrent calls on the same entity cannot interleave.
/// </para>
/// <para>
/// <see cref="AcceptQuoteAsync"/> additionally acquires the <em>per-request</em> lock before
/// modifying any quote belonging to that request, ensuring that two concurrent accept calls
/// on different quotes for the same maintenance request converge to exactly one accepted quote.
/// </para>
/// <para>
/// Suitable for demos, integration tests, and kitchen-sink scenarios.
/// Not intended for production use — no persistence, no event bus.
/// </para>
/// </remarks>
public sealed class InMemoryMaintenanceService : IMaintenanceService
{
    // ── Audit emission (W#19 Phase 4 / ADR 0053 A8) ──
    //
    // When _auditTrail and _signer are both supplied, each work-order
    // status transition + child-entity write emits an AuditRecord per the
    // 17 AuditEventType constants in kernel-audit. Both must be supplied
    // together; the parameterless constructor disables emission for tests
    // and host-bootstrap scenarios where audit signing isn't yet wired.

    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _auditTenant;

    /// <summary>Creates the service with audit emission disabled.</summary>
    public InMemoryMaintenanceService()
    {
    }

    /// <summary>Creates the service with audit emission wired through <paramref name="auditTrail"/> + <paramref name="signer"/>; <paramref name="tenantId"/> is the tenant attribution applied to every emitted record.</summary>
    public InMemoryMaintenanceService(IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId)
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

    // ── Stores ────────────────────────────────────────────────────────────────

    private readonly ConcurrentDictionary<VendorId, Vendor> _vendors = new();
    private readonly ConcurrentDictionary<MaintenanceRequestId, MaintenanceRequest> _requests = new();
    private readonly ConcurrentDictionary<WorkOrderId, WorkOrder> _workOrders = new();
    private readonly ConcurrentDictionary<QuoteId, Quote> _quotes = new();
    private readonly ConcurrentDictionary<RfqId, Rfq> _rfqs = new();

    // ── Per-entity locks ──────────────────────────────────────────────────────

    private readonly ConcurrentDictionary<MaintenanceRequestId, SemaphoreSlim> _requestLocks = new();
    private readonly ConcurrentDictionary<WorkOrderId, SemaphoreSlim> _workOrderLocks = new();

    // ── Transition tables ─────────────────────────────────────────────────────

    private static readonly TransitionTable<MaintenanceRequestStatus> RequestTransitions =
        new(
        [
            (MaintenanceRequestStatus.Submitted,    [MaintenanceRequestStatus.UnderReview, MaintenanceRequestStatus.Cancelled]),
            (MaintenanceRequestStatus.UnderReview,  [MaintenanceRequestStatus.Approved, MaintenanceRequestStatus.Rejected, MaintenanceRequestStatus.Cancelled]),
            (MaintenanceRequestStatus.Approved,     [MaintenanceRequestStatus.InProgress, MaintenanceRequestStatus.Cancelled]),
            (MaintenanceRequestStatus.InProgress,   [MaintenanceRequestStatus.Completed, MaintenanceRequestStatus.Cancelled]),
            // Terminal states (Completed, Rejected, Cancelled) have no outgoing edges — Guard will throw.
        ]);

    private static readonly TransitionTable<WorkOrderStatus> WorkOrderTransitions =
        new(
        [
            (WorkOrderStatus.Draft,           [WorkOrderStatus.Sent]),
            (WorkOrderStatus.Sent,            [WorkOrderStatus.Accepted, WorkOrderStatus.Cancelled]),
            (WorkOrderStatus.Accepted,        [WorkOrderStatus.Scheduled]),
            (WorkOrderStatus.Scheduled,       [WorkOrderStatus.InProgress]),
            (WorkOrderStatus.InProgress,      [WorkOrderStatus.Completed, WorkOrderStatus.OnHold]),
            (WorkOrderStatus.OnHold,          [WorkOrderStatus.InProgress]),
            // Post-completion segment (ADR 0053 A4):
            (WorkOrderStatus.Completed,       [WorkOrderStatus.AwaitingSignOff, WorkOrderStatus.Invoiced]),
            (WorkOrderStatus.AwaitingSignOff, [WorkOrderStatus.Invoiced, WorkOrderStatus.OnHold]),
            (WorkOrderStatus.Invoiced,        [WorkOrderStatus.Paid, WorkOrderStatus.Disputed, WorkOrderStatus.OnHold]),
            (WorkOrderStatus.Paid,            [WorkOrderStatus.Closed, WorkOrderStatus.Disputed]),
            (WorkOrderStatus.Disputed,        [WorkOrderStatus.Invoiced, WorkOrderStatus.Paid, WorkOrderStatus.Closed]),
            // Terminal states (Closed, Cancelled) have no outgoing edges.
        ]);

    private static readonly TransitionTable<QuoteStatus> QuoteTransitions =
        new(
        [
            (QuoteStatus.Draft,      [QuoteStatus.Submitted]),
            (QuoteStatus.Submitted,  [QuoteStatus.Accepted, QuoteStatus.Declined, QuoteStatus.Expired]),
        ]);

    private static readonly TransitionTable<RfqStatus> RfqTransitions =
        new(
        [
            (RfqStatus.Draft,  [RfqStatus.Sent]),
            (RfqStatus.Sent,   [RfqStatus.Closed, RfqStatus.Cancelled]),
        ]);

    // ── Vendors ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public ValueTask<Vendor> CreateVendorAsync(CreateVendorRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var vendor = new Vendor(
            Id: VendorId.NewId(),
            DisplayName: request.DisplayName,
            ContactName: request.ContactName,
            ContactEmail: request.ContactEmail,
            ContactPhone: request.ContactPhone,
            Specialty: request.Specialty,
            Status: request.Status);

        _vendors[vendor.Id] = vendor;
        return ValueTask.FromResult(vendor);
    }

    /// <inheritdoc />
    public ValueTask<Vendor?> GetVendorAsync(VendorId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _vendors.TryGetValue(id, out var vendor);
        return ValueTask.FromResult(vendor);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Vendor> ListVendorsAsync(
        ListVendorsQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var vendor in _vendors.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.Specialty.HasValue && vendor.Specialty != query.Specialty.Value)
                continue;

            if (query.Status.HasValue && vendor.Status != query.Status.Value)
                continue;

            yield return vendor;
            await Task.Yield();
        }
    }

    // ── Maintenance requests ──────────────────────────────────────────────────

    /// <inheritdoc />
    public ValueTask<MaintenanceRequest> SubmitRequestAsync(SubmitMaintenanceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var req = new MaintenanceRequest(
            Id: MaintenanceRequestId.NewId(),
            PropertyId: request.PropertyId,
            RequestedByDisplayName: request.RequestedByDisplayName,
            Description: request.Description,
            Priority: request.Priority,
            Status: MaintenanceRequestStatus.Submitted,
            RequestedDate: request.RequestedDate,
            DeficiencyReference: request.DeficiencyReference,
            CreatedAtUtc: Instant.Now);

        _requests[req.Id] = req;
        return ValueTask.FromResult(req);
    }

    /// <inheritdoc />
    public async ValueTask<MaintenanceRequest> TransitionRequestAsync(
        MaintenanceRequestId id,
        MaintenanceRequestStatus newStatus,
        CancellationToken ct = default)
    {
        var sem = _requestLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_requests.TryGetValue(id, out var req))
                throw new InvalidOperationException($"MaintenanceRequest '{id}' not found.");

            RequestTransitions.Guard(req.Status, newStatus, $"MaintenanceRequest '{id}'");

            var updated = req with { Status = newStatus };
            _requests[id] = updated;
            return updated;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask<MaintenanceRequest?> GetRequestAsync(MaintenanceRequestId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _requests.TryGetValue(id, out var req);
        return ValueTask.FromResult(req);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MaintenanceRequest> ListRequestsAsync(
        ListRequestsQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var req in _requests.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.PropertyId.HasValue && req.PropertyId != query.PropertyId.Value)
                continue;

            if (query.Status.HasValue && req.Status != query.Status.Value)
                continue;

            if (query.Priority.HasValue && req.Priority != query.Priority.Value)
                continue;

            yield return req;
            await Task.Yield();
        }
    }

    // ── RFQ / Quote ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public ValueTask<Rfq> SendRfqAsync(SendRfqRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        if (request.InvitedVendors is null || request.InvitedVendors.Count == 0)
            throw new ArgumentException("At least one vendor must be invited.", nameof(request));

        var rfq = new Rfq(
            Id: RfqId.NewId(),
            RequestId: request.RequestId,
            InvitedVendors: request.InvitedVendors,
            ResponseDueDate: request.ResponseDueDate,
            Scope: request.Scope,
            Status: RfqStatus.Sent,
            SentAtUtc: Instant.Now);

        _rfqs[rfq.Id] = rfq;
        return ValueTask.FromResult(rfq);
    }

    /// <inheritdoc />
    public ValueTask<Quote> SubmitQuoteAsync(SubmitQuoteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var quote = new Quote(
            Id: QuoteId.NewId(),
            VendorId: request.VendorId,
            RequestId: request.RequestId,
            Amount: request.Amount,
            ValidUntil: request.ValidUntil,
            Scope: request.Scope,
            Status: QuoteStatus.Submitted,
            SubmittedAtUtc: Instant.Now);

        _quotes[quote.Id] = quote;
        return ValueTask.FromResult(quote);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Atomicity: acquires the per-request lock before touching any quote for that request.
    /// This guarantees that concurrent <c>AcceptQuoteAsync</c> calls on different quotes for
    /// the same maintenance request converge to exactly one accepted quote — the first one
    /// through the lock wins; the second sees its target already Declined and throws.
    /// </remarks>
    public async ValueTask<Quote> AcceptQuoteAsync(QuoteId id, CancellationToken ct = default)
    {
        // First resolve which request this quote belongs to, outside the lock.
        if (!_quotes.TryGetValue(id, out var target))
            throw new InvalidOperationException($"Quote '{id}' not found.");

        // Acquire the per-REQUEST lock so no other AcceptQuoteAsync for the same request
        // can run concurrently.
        var requestLock = _requestLocks.GetOrAdd(target.RequestId, _ => new SemaphoreSlim(1, 1));
        await requestLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-read the target quote inside the lock — its status may have changed.
            if (!_quotes.TryGetValue(id, out target))
                throw new InvalidOperationException($"Quote '{id}' not found.");

            QuoteTransitions.Guard(target.Status, QuoteStatus.Accepted, $"Quote '{id}'");

            // Accept the target quote.
            var accepted = target with { Status = QuoteStatus.Accepted };
            _quotes[id] = accepted;

            // Decline all other Submitted/Draft quotes for the same request.
            foreach (var (otherId, other) in _quotes)
            {
                if (otherId == id) continue;
                if (other.RequestId != target.RequestId) continue;
                if (other.Status is QuoteStatus.Submitted or QuoteStatus.Draft)
                    _quotes[otherId] = other with { Status = QuoteStatus.Declined };
            }

            // Spawn a WorkOrder from this accepted quote.
            var workOrder = new WorkOrder(
                Id: WorkOrderId.NewId(),
                RequestId: target.RequestId,
                AssignedVendorId: target.VendorId,
                Status: WorkOrderStatus.Draft,
                ScheduledDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), // placeholder; caller can update
                CompletedDate: null,
                EstimatedCost: target.Amount,
                ActualCost: null,
                Notes: $"Created from accepted quote {id}.",
                CreatedAtUtc: Instant.Now);

            _workOrders[workOrder.Id] = workOrder;

            return accepted;
        }
        finally
        {
            requestLock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Quote> ListQuotesAsync(
        MaintenanceRequestId requestId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var quote in _quotes.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (quote.RequestId != requestId)
                continue;

            yield return quote;
            await Task.Yield();
        }
    }

    // ── Work orders ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async ValueTask<WorkOrder> CreateWorkOrderAsync(CreateWorkOrderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var workOrder = new WorkOrder(
            Id: WorkOrderId.NewId(),
            RequestId: request.RequestId,
            AssignedVendorId: request.AssignedVendorId,
            Status: WorkOrderStatus.Draft,
            ScheduledDate: request.ScheduledDate,
            CompletedDate: null,
            EstimatedCost: request.EstimatedCost,
            ActualCost: null,
            Notes: request.Notes,
            CreatedAtUtc: Instant.Now);

        _workOrders[workOrder.Id] = workOrder;
        await EmitAsync(
            AuditEventType.WorkOrderCreated,
            WorkOrderAuditPayloadFactory.Created(workOrder.Id, WorkOrderStatus.Draft, ActorId.System),
            ct).ConfigureAwait(false);
        return workOrder;
    }

    /// <inheritdoc />
    public async ValueTask<WorkOrder> TransitionWorkOrderAsync(
        WorkOrderId id,
        WorkOrderStatus newStatus,
        CancellationToken ct = default)
    {
        var sem = _workOrderLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_workOrders.TryGetValue(id, out var workOrder))
                throw new InvalidOperationException($"WorkOrder '{id}' not found.");

            WorkOrderTransitions.Guard(workOrder.Status, newStatus, $"WorkOrder '{id}'");

            var completedDate = newStatus == WorkOrderStatus.Completed
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : workOrder.CompletedDate;

            var updated = workOrder with
            {
                Status = newStatus,
                CompletedDate = completedDate,
            };

            _workOrders[id] = updated;
            await EmitAsync(
                WorkOrderAuditPayloadFactory.EventForTransition(workOrder.Status, newStatus),
                WorkOrderAuditPayloadFactory.StatusTransition(id, workOrder.Status, newStatus, ActorId.System),
                ct).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask<WorkOrder?> GetWorkOrderAsync(WorkOrderId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _workOrders.TryGetValue(id, out var workOrder);
        return ValueTask.FromResult(workOrder);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WorkOrder> ListWorkOrdersAsync(
        ListWorkOrdersQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var wo in _workOrders.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.RequestId.HasValue && wo.RequestId != query.RequestId.Value)
                continue;

            if (query.VendorId.HasValue && wo.AssignedVendorId != query.VendorId.Value)
                continue;

            if (query.Status.HasValue && wo.Status != query.Status.Value)
                continue;

            yield return wo;
            await Task.Yield();
        }
    }

    // ─────────────────── Child entities (W#19 Phase 3 / ADR 0053) ──────────────

    private readonly ConcurrentDictionary<WorkOrderEntryNoticeId, WorkOrderEntryNotice> _entryNotices = new();
    private readonly ConcurrentDictionary<WorkOrderCompletionAttestationId, WorkOrderCompletionAttestation> _attestations = new();
    private readonly ConcurrentDictionary<WorkOrderAppointmentId, WorkOrderAppointment> _appointments = new();

    // Phase 2 will replace with the Flease primitive per ADR 0028.
    private readonly object _appointmentSlotLock = new();

    /// <inheritdoc />
    public async ValueTask<WorkOrderEntryNotice> RecordEntryNoticeAsync(WorkOrderEntryNotice notice, ActorId actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notice);
        if (!_workOrders.ContainsKey(notice.WorkOrder))
        {
            throw new InvalidOperationException($"Work order '{notice.WorkOrder}' not found.");
        }
        if (!_entryNotices.TryAdd(notice.Id, notice))
        {
            throw new InvalidOperationException($"Entry notice '{notice.Id}' already exists.");
        }
        await EmitAsync(
            AuditEventType.WorkOrderEntryNoticeRecorded,
            WorkOrderAuditPayloadFactory.EntryNoticeRecorded(notice, actor),
            ct).ConfigureAwait(false);
        return notice;
    }

    /// <inheritdoc />
    public async ValueTask<WorkOrderCompletionAttestation> CaptureCompletionAttestationAsync(WorkOrderCompletionAttestation attestation, ActorId actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(attestation);
        if (!_workOrders.ContainsKey(attestation.WorkOrder))
        {
            throw new InvalidOperationException($"Work order '{attestation.WorkOrder}' not found.");
        }
        if (!_attestations.TryAdd(attestation.Id, attestation))
        {
            throw new InvalidOperationException($"Completion attestation '{attestation.Id}' already exists.");
        }
        await EmitAsync(
            AuditEventType.WorkOrderCompletionAttestationCaptured,
            WorkOrderAuditPayloadFactory.CompletionAttestationCaptured(attestation, actor),
            ct).ConfigureAwait(false);
        return attestation;
    }

    /// <inheritdoc />
    public async ValueTask<WorkOrderAppointment> ProposeAppointmentAsync(WorkOrderAppointment proposed, ActorId actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        if (!_workOrders.ContainsKey(proposed.WorkOrder))
        {
            throw new InvalidOperationException($"Work order '{proposed.WorkOrder}' not found.");
        }
        if (proposed.Status != AppointmentStatus.Proposed)
        {
            throw new InvalidOperationException($"New appointment must be in {nameof(AppointmentStatus.Proposed)} status; got {proposed.Status}.");
        }

        // Phase 2 will replace with the Flease primitive per ADR 0028.
        // For Phase 1 in-memory, an in-process lock prevents double-booking
        // overlapping slots on the same work order.
        lock (_appointmentSlotLock)
        {
            foreach (var existing in _appointments.Values.Where(a => a.WorkOrder == proposed.WorkOrder && a.Status != AppointmentStatus.Cancelled))
            {
                if (proposed.SlotStartUtc < existing.SlotEndUtc && existing.SlotStartUtc < proposed.SlotEndUtc)
                {
                    throw new InvalidOperationException($"Proposed slot for work order '{proposed.WorkOrder}' overlaps with existing appointment '{existing.Id}'.");
                }
            }
            if (!_appointments.TryAdd(proposed.Id, proposed))
            {
                throw new InvalidOperationException($"Appointment '{proposed.Id}' already exists.");
            }
        }
        await EmitAsync(
            AuditEventType.WorkOrderAppointmentScheduled,
            WorkOrderAuditPayloadFactory.AppointmentScheduled(proposed, actor),
            ct).ConfigureAwait(false);
        return proposed;
    }

    /// <inheritdoc />
    public async ValueTask<WorkOrderAppointment> ConfirmAppointmentAsync(WorkOrderAppointmentId id, ActorId actor, CancellationToken ct = default)
    {
        if (!_appointments.TryGetValue(id, out var current))
        {
            throw new InvalidOperationException($"Appointment '{id}' not found.");
        }
        if (current.Status != AppointmentStatus.Proposed)
        {
            throw new InvalidOperationException($"Appointment '{id}' is in {current.Status} status; only {nameof(AppointmentStatus.Proposed)} can be confirmed.");
        }
        var confirmed = current with
        {
            Status = AppointmentStatus.Confirmed,
            ConfirmedBy = actor,
            ConfirmedAt = DateTimeOffset.UtcNow,
        };
        _appointments[id] = confirmed;
        await EmitAsync(
            AuditEventType.WorkOrderAppointmentConfirmed,
            WorkOrderAuditPayloadFactory.AppointmentConfirmed(confirmed, actor),
            ct).ConfigureAwait(false);
        return confirmed;
    }
}
