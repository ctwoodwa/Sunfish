using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.SickBay;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.SickBay.Tests;

// ── Shared test helpers ───────────────────────────────────────────────────

file static class TestHelpers
{
    public static readonly TenantId Tenant = new("test-tenant");
    public static readonly PrincipalId PrincipalA = PrincipalId.FromBytes(new byte[32]);
    public static readonly PrincipalId PrincipalB = PrincipalId.FromBytes(Enumerable.Repeat((byte)1, 32).ToArray());

    public static IOperationSigner StubSigner()
    {
        var signer = Substitute.For<IOperationSigner>();
        signer.IssuerId.Returns(PrincipalA);
        signer.SignAsync(
                Arg.Any<AuditPayload>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var payload = call.Arg<AuditPayload>();
                var occurredAt = call.Arg<DateTimeOffset>();
                var nonce = call.Arg<Guid>();
                return new ValueTask<SignedOperation<AuditPayload>>(
                    new SignedOperation<AuditPayload>(
                        Payload: payload!,
                        IssuerId: PrincipalA,
                        IssuedAt: occurredAt,
                        Nonce: nonce,
                        Signature: Signature.FromBytes(new byte[64])));
            });
        return signer;
    }

    public static (InMemoryAuditTrail trail, IOperationSigner signer) NewAudit() =>
        (new InMemoryAuditTrail(), StubSigner());

    public static async Task<List<AuditRecord>> QueryAllAsync(
        InMemoryAuditTrail trail, TenantId tenant)
    {
        var results = new List<AuditRecord>();
        await foreach (var r in trail.QueryAsync(new AuditQuery(TenantId: tenant)))
            results.Add(r);
        return results;
    }
}

// ── SickBayCommandServiceTests ────────────────────────────────────────────

public sealed class SickBayCommandServiceTests
{
    [Fact]
    public async Task TriggerKeyRotationAsync_emits_audit_pre_op()
    {
        var (trail, signer) = TestHelpers.NewAudit();
        var scheduler = Substitute.For<IKeyRotationScheduler>();
        bool auditEmittedBeforeSchedule = false;

        scheduler.ScheduleAsync(
                Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var records = await TestHelpers.QueryAllAsync(trail, TestHelpers.Tenant);
                auditEmittedBeforeSchedule = records.Any(r =>
                    r.EventType == AuditEventType.SickBayKeyRotationTriggered);
            });

        var svc = new SickBayCommandService(trail, signer, scheduler);
        await svc.TriggerKeyRotationAsync(TestHelpers.Tenant, "recovery-key", "quarterly-rotation");

        Assert.True(auditEmittedBeforeSchedule,
            "Audit must be emitted BEFORE ScheduleAsync is called (ADR 0082 §6).");
    }

    [Fact]
    public async Task TriggerKeyRotationAsync_calls_scheduler_after_audit()
    {
        var (trail, signer) = TestHelpers.NewAudit();
        var scheduler = Substitute.For<IKeyRotationScheduler>();
        var svc = new SickBayCommandService(trail, signer, scheduler);

        await svc.TriggerKeyRotationAsync(TestHelpers.Tenant, "recovery-key", "manual");

        await scheduler.Received(1).ScheduleAsync(
            TestHelpers.Tenant, "recovery-key", "manual",
            Arg.Any<CancellationToken>());

        var records = await TestHelpers.QueryAllAsync(trail, TestHelpers.Tenant);
        Assert.Single(records, r => r.EventType == AuditEventType.SickBayKeyRotationTriggered);
    }

    [Fact]
    public async Task TriggerKeyRotationAsync_throws_when_fieldPurpose_null()
    {
        var (trail, signer) = TestHelpers.NewAudit();
        var scheduler = Substitute.For<IKeyRotationScheduler>();
        var svc = new SickBayCommandService(trail, signer, scheduler);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.TriggerKeyRotationAsync(TestHelpers.Tenant, null!, "reason"));
    }
}

// ── MedevacServiceTests ───────────────────────────────────────────────────

public sealed class MedevacServiceTests
{
    private static MedevacServiceImpl NewSvc(InMemoryAuditTrail? trail = null, IOperationSigner? signer = null)
    {
        trail ??= new InMemoryAuditTrail();
        signer ??= TestHelpers.StubSigner();
        return new MedevacServiceImpl(trail, signer);
    }

    [Fact]
    public async Task RequestAsync_transitions_Idle_to_PendingAuthorization()
    {
        var svc = NewSvc();
        await svc.RequestAsync(TestHelpers.Tenant, TestHelpers.PrincipalA, "crew member injured");

        var state = await svc.GetStateAsync(TestHelpers.Tenant);
        Assert.Equal(MedevacState.PendingAuthorization, state);
    }

    [Fact]
    public async Task RequestAsync_emits_SickBayMedevacInitiated_pre_op()
    {
        var trail = new InMemoryAuditTrail();
        var svc = NewSvc(trail);

        await svc.RequestAsync(TestHelpers.Tenant, TestHelpers.PrincipalA, "emergency");

        var records = await TestHelpers.QueryAllAsync(trail, TestHelpers.Tenant);
        Assert.Contains(records, r => r.EventType == AuditEventType.SickBayMedevacInitiated);
    }

    [Fact]
    public async Task AuthorizeAsync_rejects_self_approval_and_emits_SickBayMedevacSelfApprovalRejected()
    {
        var trail = new InMemoryAuditTrail();
        var svc = NewSvc(trail);

        await svc.RequestAsync(TestHelpers.Tenant, TestHelpers.PrincipalA, "injury");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AuthorizeAsync(TestHelpers.Tenant, TestHelpers.PrincipalA));

        Assert.Contains("Self-approval rejected", ex.Message);

        var records = await TestHelpers.QueryAllAsync(trail, TestHelpers.Tenant);
        Assert.Contains(records, r => r.EventType == AuditEventType.SickBayMedevacSelfApprovalRejected);
    }

    [Fact]
    public async Task AuthorizeAsync_transitions_PendingAuthorization_to_InProgress()
    {
        var svc = NewSvc();
        await svc.RequestAsync(TestHelpers.Tenant, TestHelpers.PrincipalA, "injury");
        await svc.AuthorizeAsync(TestHelpers.Tenant, TestHelpers.PrincipalB);

        var state = await svc.GetStateAsync(TestHelpers.Tenant);
        Assert.Equal(MedevacState.InProgress, state);
    }

    [Fact]
    public async Task CancelAsync_throws_InvalidOperationException_when_state_is_Complete()
    {
        var svc = NewSvc();
        await svc.RequestAsync(TestHelpers.Tenant, TestHelpers.PrincipalA, "injury");
        await svc.AuthorizeAsync(TestHelpers.Tenant, TestHelpers.PrincipalB);
        await svc.CompleteAsync(TestHelpers.Tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CancelAsync(TestHelpers.Tenant, TestHelpers.PrincipalA));
    }

    [Fact]
    public async Task CompleteAsync_transitions_InProgress_to_Complete()
    {
        var svc = NewSvc();
        await svc.RequestAsync(TestHelpers.Tenant, TestHelpers.PrincipalA, "injury");
        await svc.AuthorizeAsync(TestHelpers.Tenant, TestHelpers.PrincipalB);
        await svc.CompleteAsync(TestHelpers.Tenant);

        var state = await svc.GetStateAsync(TestHelpers.Tenant);
        Assert.Equal(MedevacState.Complete, state);
    }

    [Fact]
    public async Task All_transitions_emit_audit_pre_op()
    {
        var trail = new InMemoryAuditTrail();
        var svc = NewSvc(trail);

        await svc.RequestAsync(TestHelpers.Tenant, TestHelpers.PrincipalA, "injury");
        await svc.AuthorizeAsync(TestHelpers.Tenant, TestHelpers.PrincipalB);
        await svc.CompleteAsync(TestHelpers.Tenant);

        var records = await TestHelpers.QueryAllAsync(trail, TestHelpers.Tenant);
        var eventTypes = records.Select(r => r.EventType).ToList();

        Assert.Contains(AuditEventType.SickBayMedevacInitiated, eventTypes);
        Assert.Contains(AuditEventType.SickBayMedevacAuthorized, eventTypes);
        Assert.Contains(AuditEventType.SickBayMedevacCompleted, eventTypes);
    }
}
