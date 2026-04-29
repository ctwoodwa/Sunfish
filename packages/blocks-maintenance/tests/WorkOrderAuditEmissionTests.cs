using System.Collections.Immutable;
using System.Threading;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

/// <summary>
/// Verifies the 17 work-order <see cref="AuditEventType"/> emissions per
/// ADR 0053 amendment A8. Mirrors the W#31 TaxonomyAuditEmissionTests
/// pattern (capturing audit trail + stub signer + per-event assertions).
/// </summary>
public sealed class WorkOrderAuditEmissionTests
{
    private static readonly EntityId TestPropertyId = new("property", "test", "prop-1");
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly ActorId Operator = new("operator");

    // ── Test infrastructure ────────────────────────────────────────────────

    private sealed class CapturingAuditTrail : IAuditTrail
    {
        public List<AuditRecord> Records { get; } = new();

        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var r in Records) yield return r;
            await Task.CompletedTask;
        }
    }

    private sealed class StubSigner : IOperationSigner
    {
        public PrincipalId IssuerId { get; } = PrincipalId.FromBytes(new byte[32]);

        public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default) =>
            ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, Signature.FromBytes(new byte[64])));
    }

    private static (InMemoryMaintenanceService svc, CapturingAuditTrail trail) NewServiceWithAudit() =>
        (new InMemoryMaintenanceService(new CapturingAuditTrail(), new StubSigner(), TestTenant), new CapturingAuditTrail());

    private static InMemoryMaintenanceService NewServiceCapturing(out CapturingAuditTrail trail)
    {
        trail = new CapturingAuditTrail();
        return new InMemoryMaintenanceService(trail, new StubSigner(), TestTenant);
    }

    private static async Task<(InMemoryMaintenanceService svc, WorkOrder wo, CapturingAuditTrail trail)> NewWorkOrderAsync(WorkOrderStatus advanceTo = WorkOrderStatus.Draft)
    {
        var svc = NewServiceCapturing(out var trail);
        var v = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V", Specialty = VendorSpecialty.Plumbing });
        var r = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        { PropertyId = TestPropertyId, RequestedByDisplayName = "T", Description = "x", Priority = MaintenancePriority.Normal, RequestedDate = new DateOnly(2026, 5, 1) });
        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        { RequestId = r.Id, AssignedVendorId = v.Id, ScheduledDate = new DateOnly(2026, 5, 10), EstimatedCost = 100m });
        var status = WorkOrderStatus.Draft;
        var path = new[]
        {
            WorkOrderStatus.Sent, WorkOrderStatus.Accepted, WorkOrderStatus.Scheduled,
            WorkOrderStatus.InProgress, WorkOrderStatus.Completed,
            WorkOrderStatus.AwaitingSignOff, WorkOrderStatus.Invoiced, WorkOrderStatus.Paid,
            WorkOrderStatus.Closed,
        };
        foreach (var next in path)
        {
            if (status == advanceTo) break;
            wo = await svc.TransitionWorkOrderAsync(wo.Id, next);
            status = next;
        }
        return (svc, wo, trail);
    }

    private static AuditRecord SingleByEventType(CapturingAuditTrail trail, AuditEventType expected) =>
        trail.Records.Single(r => r.EventType == expected);

    // ── 14 status-set emissions (1 Created + 13 transitions) ───────────────

    [Fact]
    public async Task CreateWorkOrder_Emits_WorkOrderCreated()
    {
        var (_, _, trail) = await NewWorkOrderAsync();
        var record = SingleByEventType(trail, AuditEventType.WorkOrderCreated);
        Assert.Equal(TestTenant, record.TenantId);
        Assert.Equal("Draft", record.Payload.Payload.Body["initial_status"]);
    }

    [Fact]
    public async Task DraftToSent_Emits_WorkOrderSent()
    {
        var (_, _, trail) = await NewWorkOrderAsync(WorkOrderStatus.Sent);
        SingleByEventType(trail, AuditEventType.WorkOrderSent);
    }

    [Fact]
    public async Task SentToAccepted_Emits_WorkOrderAccepted()
    {
        var (_, _, trail) = await NewWorkOrderAsync(WorkOrderStatus.Accepted);
        SingleByEventType(trail, AuditEventType.WorkOrderAccepted);
    }

    [Fact]
    public async Task AcceptedToScheduled_Emits_WorkOrderScheduled()
    {
        var (_, _, trail) = await NewWorkOrderAsync(WorkOrderStatus.Scheduled);
        SingleByEventType(trail, AuditEventType.WorkOrderScheduled);
    }

    [Fact]
    public async Task ScheduledToInProgress_Emits_WorkOrderStarted()
    {
        var (_, _, trail) = await NewWorkOrderAsync(WorkOrderStatus.InProgress);
        SingleByEventType(trail, AuditEventType.WorkOrderStarted);
    }

    [Fact]
    public async Task InProgressToOnHold_Emits_WorkOrderHeld()
    {
        var (svc, wo, trail) = await NewWorkOrderAsync(WorkOrderStatus.InProgress);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.OnHold);
        SingleByEventType(trail, AuditEventType.WorkOrderHeld);
    }

    [Fact]
    public async Task OnHoldToInProgress_Emits_WorkOrderResumed()
    {
        var (svc, wo, trail) = await NewWorkOrderAsync(WorkOrderStatus.InProgress);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.OnHold);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.InProgress);
        SingleByEventType(trail, AuditEventType.WorkOrderResumed);
    }

    [Fact]
    public async Task InProgressToCompleted_Emits_WorkOrderCompleted()
    {
        var (_, _, trail) = await NewWorkOrderAsync(WorkOrderStatus.Completed);
        SingleByEventType(trail, AuditEventType.WorkOrderCompleted);
    }

    [Fact]
    public async Task CompletedToAwaitingSignOff_Emits_WorkOrderSignedOff()
    {
        var (_, _, trail) = await NewWorkOrderAsync(WorkOrderStatus.AwaitingSignOff);
        SingleByEventType(trail, AuditEventType.WorkOrderSignedOff);
    }

    [Fact]
    public async Task ToInvoiced_Emits_WorkOrderInvoiced()
    {
        var (_, _, trail) = await NewWorkOrderAsync(WorkOrderStatus.Invoiced);
        SingleByEventType(trail, AuditEventType.WorkOrderInvoiced);
    }

    [Fact]
    public async Task InvoicedToPaid_Emits_WorkOrderPaid()
    {
        var (_, _, trail) = await NewWorkOrderAsync(WorkOrderStatus.Paid);
        SingleByEventType(trail, AuditEventType.WorkOrderPaid);
    }

    [Fact]
    public async Task ToDisputed_Emits_WorkOrderDisputed()
    {
        var (svc, wo, trail) = await NewWorkOrderAsync(WorkOrderStatus.Invoiced);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Disputed);
        SingleByEventType(trail, AuditEventType.WorkOrderDisputed);
    }

    [Fact]
    public async Task ToClosed_Emits_WorkOrderClosed()
    {
        var (_, _, trail) = await NewWorkOrderAsync(WorkOrderStatus.Closed);
        SingleByEventType(trail, AuditEventType.WorkOrderClosed);
    }

    [Fact]
    public async Task SentToCancelled_Emits_WorkOrderCancelled()
    {
        var (svc, wo, trail) = await NewWorkOrderAsync(WorkOrderStatus.Sent);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Cancelled);
        SingleByEventType(trail, AuditEventType.WorkOrderCancelled);
    }

    // ── 4 child-entity emissions ───────────────────────────────────────────

    [Fact]
    public async Task RecordEntryNotice_Emits_WorkOrderEntryNoticeRecorded()
    {
        var (svc, wo, trail) = await NewWorkOrderAsync(WorkOrderStatus.Sent);
        var notice = new WorkOrderEntryNotice
        {
            Id = WorkOrderEntryNoticeId.NewId(),
            WorkOrder = wo.Id,
            PlannedEntryUtc = DateTimeOffset.UtcNow.AddDays(1),
            EntryReason = "Routine",
            NotifiedBy = Operator,
            NotifiedAt = DateTimeOffset.UtcNow,
        };
        await svc.RecordEntryNoticeAsync(notice, Operator);
        var rec = SingleByEventType(trail, AuditEventType.WorkOrderEntryNoticeRecorded);
        Assert.Equal(notice.Id.Value, rec.Payload.Payload.Body["entry_notice_id"]);
        Assert.Equal("Routine", rec.Payload.Payload.Body["entry_reason"]);
    }

    [Fact]
    public async Task ProposeAppointment_Emits_WorkOrderAppointmentScheduled()
    {
        var (svc, wo, trail) = await NewWorkOrderAsync(WorkOrderStatus.Accepted);
        var slotStart = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var appt = new WorkOrderAppointment
        {
            Id = WorkOrderAppointmentId.NewId(),
            WorkOrder = wo.Id,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotStart.AddHours(2),
            Status = AppointmentStatus.Proposed,
            ProposedBy = Operator,
        };
        await svc.ProposeAppointmentAsync(appt, Operator);
        var rec = SingleByEventType(trail, AuditEventType.WorkOrderAppointmentScheduled);
        Assert.Equal(appt.Id.Value, rec.Payload.Payload.Body["appointment_id"]);
    }

    [Fact]
    public async Task ConfirmAppointment_Emits_WorkOrderAppointmentConfirmed()
    {
        var (svc, wo, trail) = await NewWorkOrderAsync(WorkOrderStatus.Accepted);
        var slotStart = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var proposed = await svc.ProposeAppointmentAsync(new WorkOrderAppointment
        {
            Id = WorkOrderAppointmentId.NewId(),
            WorkOrder = wo.Id,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotStart.AddHours(2),
            Status = AppointmentStatus.Proposed,
            ProposedBy = Operator,
        }, Operator);
        await svc.ConfirmAppointmentAsync(proposed.Id, new ActorId("vendor-1"));
        SingleByEventType(trail, AuditEventType.WorkOrderAppointmentConfirmed);
    }

    [Fact]
    public async Task CaptureAttestation_Emits_WorkOrderCompletionAttestationCaptured()
    {
        var (svc, wo, trail) = await NewWorkOrderAsync(WorkOrderStatus.Completed);
        var a = new WorkOrderCompletionAttestation
        {
            Id = WorkOrderCompletionAttestationId.NewId(),
            WorkOrder = wo.Id,
            Signature = new Sunfish.Foundation.Integrations.Signatures.SignatureEventRef(Guid.NewGuid()),
            AttestedAt = DateTimeOffset.UtcNow,
            Attestor = Operator,
        };
        await svc.CaptureCompletionAttestationAsync(a, Operator);
        SingleByEventType(trail, AuditEventType.WorkOrderCompletionAttestationCaptured);
    }
}
