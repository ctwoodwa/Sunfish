using System.Collections.Immutable;
using System.Threading;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Integrations.Messaging;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Integrations.Signatures;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

/// <summary>
/// Cross-package wiring integration tests per W#19 Phase 6 / ADR 0053.
/// </summary>
public sealed class CrossPackageWiringTests
{
    private static readonly EntityId TestPropertyId = new("property", "test", "prop-1");
    private static readonly TenantId TestTenant = new("tenant-a");

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

    private sealed class StubThreadStore : IThreadStore
    {
        public List<(TenantId Tenant, IReadOnlyList<Participant> Participants, MessageVisibility Visibility)> Created { get; } = new();
        public Task<ThreadId> CreateAsync(TenantId tenant, IReadOnlyList<Participant> participants, MessageVisibility defaultVisibility, CancellationToken ct)
        {
            Created.Add((tenant, participants, defaultVisibility));
            return Task.FromResult(new ThreadId(Guid.NewGuid()));
        }
        public Task<ThreadSnapshot?> GetAsync(TenantId tenant, ThreadId threadId, CancellationToken ct) => Task.FromResult<ThreadSnapshot?>(null);
        public Task<ThreadId> SplitAsync(TenantId tenant, ThreadId sourceThreadId, IReadOnlyList<Participant> newParticipants, IReadOnlyList<MessageId> copyForwardMessageIds, MessageVisibility newDefaultVisibility, CancellationToken ct) =>
            Task.FromResult(new ThreadId(Guid.NewGuid()));
        public Task AppendMessageAsync(TenantId tenant, ThreadId threadId, MessageId messageId, CancellationToken ct) => Task.CompletedTask;
    }

    private static (InMemoryMaintenanceService svc, StubThreadStore threads, InMemoryPaymentGateway pay, InMemorySignatureCapture sigs) NewWiredService()
    {
        var trail = new CapturingAuditTrail();
        var threads = new StubThreadStore();
        var pay = new InMemoryPaymentGateway();
        var sigs = new InMemorySignatureCapture();
        var svc = new InMemoryMaintenanceService(trail, new StubSigner(), TestTenant, threads, pay, sigs);
        return (svc, threads, pay, sigs);
    }

    private static async Task<(InMemoryMaintenanceService svc, StubThreadStore threads, InMemoryPaymentGateway pay, InMemorySignatureCapture sigs, WorkOrder wo)> NewWorkOrderAsync(WorkOrderStatus advanceTo = WorkOrderStatus.Draft)
    {
        var (svc, threads, pay, sigs) = NewWiredService();
        var v = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "Acme Plumbing", ContactEmail = "ops@acme.example", Specialty = VendorSpecialty.Plumbing });
        var r = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        { PropertyId = TestPropertyId, RequestedByDisplayName = "T", Description = "x", Priority = MaintenancePriority.Normal, RequestedDate = new DateOnly(2026, 5, 1) });
        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        { Tenant = TestTenant, RequestId = r.Id, AssignedVendorId = v.Id, ScheduledDate = new DateOnly(2026, 5, 10), EstimatedCost = Money.Usd(100m) });
        var status = WorkOrderStatus.Draft;
        var path = new[] { WorkOrderStatus.Sent, WorkOrderStatus.Accepted, WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress, WorkOrderStatus.Completed, WorkOrderStatus.AwaitingSignOff, WorkOrderStatus.Invoiced, WorkOrderStatus.Paid, WorkOrderStatus.Closed };
        foreach (var next in path)
        {
            if (status == advanceTo) break;
            wo = await svc.TransitionWorkOrderAsync(wo.Id, next);
            status = next;
        }
        return (svc, threads, pay, sigs, wo);
    }

    [Fact]
    public async Task CreateWorkOrder_OpensCoordinationThread_WhenIThreadStoreWired()
    {
        var (svc, threads, _, _, wo) = await NewWorkOrderAsync();

        Assert.Single(threads.Created);
        Assert.NotNull(wo.PrimaryThread);

        var (_, participants, visibility) = threads.Created[0];
        Assert.Equal(2, participants.Count);
        Assert.Equal("Operator", participants[0].DisplayName);
        Assert.Equal("Acme Plumbing", participants[1].DisplayName);
        Assert.Equal("ops@acme.example", participants[1].EmailAddress);
        Assert.Equal(MessageVisibility.Public, visibility);
    }

    [Fact]
    public async Task CreateWorkOrder_SkipsThread_WhenCreateThreadFalse()
    {
        var (svc, threads, _, _) = NewWiredService();
        var v = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V", Specialty = VendorSpecialty.Plumbing });
        var r = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        { PropertyId = TestPropertyId, RequestedByDisplayName = "T", Description = "x", Priority = MaintenancePriority.Normal, RequestedDate = new DateOnly(2026, 5, 1) });

        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        { Tenant = TestTenant, RequestId = r.Id, AssignedVendorId = v.Id, ScheduledDate = new DateOnly(2026, 5, 10), EstimatedCost = Money.Usd(100m), CreateThread = false });

        Assert.Empty(threads.Created);
        Assert.Null(wo.PrimaryThread);
    }

    [Fact]
    public async Task TransitionToInvoiced_AuthorizesPayment_WhenPaymentGatewayWired()
    {
        var (svc, _, pay, _, wo) = await NewWorkOrderAsync(WorkOrderStatus.Invoiced);

        Assert.Single(pay.Journal);
        var entry = pay.Journal.Values.Single();
        Assert.Equal(PaymentStatus.Authorized, entry.Status);
        Assert.Equal(Money.Usd(100m), entry.Request.Amount);
        Assert.Equal(wo.Id.Value, entry.Request.CorrelationId);
    }

    [Fact]
    public async Task TransitionToPaid_CapturesAuthorizedPayment()
    {
        var (svc, _, pay, _, wo) = await NewWorkOrderAsync(WorkOrderStatus.Paid);

        var entry = pay.Journal.Values.Single();
        Assert.Equal(PaymentStatus.Captured, entry.Status);
    }

    [Fact]
    public async Task PaymentGateway_NotInvoked_WhenNotWired()
    {
        var trail = new CapturingAuditTrail();
        var svc = new InMemoryMaintenanceService(trail, new StubSigner(), TestTenant, threadStore: null, paymentGateway: null, signatureCapture: null);
        var v = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V", Specialty = VendorSpecialty.Plumbing });
        var r = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        { PropertyId = TestPropertyId, RequestedByDisplayName = "T", Description = "x", Priority = MaintenancePriority.Normal, RequestedDate = new DateOnly(2026, 5, 1) });
        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        { Tenant = TestTenant, RequestId = r.Id, AssignedVendorId = v.Id, ScheduledDate = new DateOnly(2026, 5, 10), EstimatedCost = Money.Usd(100m), CreateThread = false });

        // Transition through to Invoiced.
        foreach (var step in new[] { WorkOrderStatus.Sent, WorkOrderStatus.Accepted, WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress, WorkOrderStatus.Completed, WorkOrderStatus.Invoiced })
        {
            await svc.TransitionWorkOrderAsync(wo.Id, step);
        }
        // Without a wired gateway, no authorization handle was stored — Paid transition is still allowed.
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Paid);
        // Test passes if no exception thrown.
    }

    [Fact]
    public async Task SignatureCapture_AvailableViaCtorInjection()
    {
        var (_, _, _, sigs, _) = await NewWorkOrderAsync();

        // The capture path is consumer-facing for future hand-offs; this test
        // verifies the service accepts the dependency + the in-memory stub
        // round-trips a capture.
        var captured = await sigs.CaptureAsync(
            new SignatureCaptureRequest(TestTenant, ActorId.System, "WorkOrderCompletionAttestation", "abc123"),
            CancellationToken.None);
        Assert.NotEqual(Guid.Empty, captured.SignatureEventId);
        Assert.Single(sigs.Journal);
    }
}
