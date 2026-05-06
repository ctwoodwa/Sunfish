using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Crdt.Backends;
using Xunit;

namespace Sunfish.Foundation.Wayfinder.Tests;

/// <summary>
/// Phase 2 — issuer tests: validator chain, audit emission, rescission.
/// </summary>
public sealed class DefaultStandingOrderIssuerTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly ActorId ActorA = new("u1");

    private static StandingOrderDraft NewDraft(string path) => new(
        TenantA,
        StandingOrderScope.Tenant,
        new[] { new StandingOrderTriple(path, null, JsonNode.Parse("\"new-value\"")) },
        "switch the value",
        ApprovalChain: null);

    private static (DefaultStandingOrderIssuer Issuer, CrdtStandingOrderRepository Repo, IAuditTrail Audit, FakeSigner Signer, InMemoryStandingOrderEventStream EventStream)
        Build(params IStandingOrderValidator[] validators)
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var audit = Substitute.For<IAuditTrail>();
        var signer = new FakeSigner();
        var time = new TestTimeProvider();
        var eventStream = new InMemoryStandingOrderEventStream();
        var issuer = new DefaultStandingOrderIssuer(repo, validators, signer, time, eventStream);
        return (issuer, repo, audit, signer, eventStream);
    }

    [Fact]
    public async Task IssueAsync_AcceptedOrder_EmitsStandingOrderIssued()
    {
        var (issuer, _, audit, _, _) = Build();
        var order = await issuer.IssueAsync(NewDraft("anchor.maui.theme"), ActorA, audit, CancellationToken.None);

        Assert.Equal(StandingOrderState.Validated, order.State);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.StandingOrderIssued)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueAsync_BlockSeverity_FlipsStateToRejected_AndEmitsRejectedAudit()
    {
        var blocker = new InlineValidator(StandingOrderValidatorPriority.Policy,
            new StandingOrderValidationIssue(StandingOrderValidationSeverity.Block,
                "anchor.maui.theme", "not allowed in production", null));
        var (issuer, _, audit, _, _) = Build(blocker);

        var order = await issuer.IssueAsync(NewDraft("anchor.maui.theme"), ActorA, audit, CancellationToken.None);

        Assert.Equal(StandingOrderState.Rejected, order.State);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.StandingOrderRejected)),
            Arg.Any<CancellationToken>());
        await audit.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.StandingOrderIssued)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueAsync_RunsValidatorChainInPriorityOrder()
    {
        var calls = new List<StandingOrderValidatorPriority>();
        var schemaV = new RecordingValidator(StandingOrderValidatorPriority.Schema, calls);
        var policyV = new RecordingValidator(StandingOrderValidatorPriority.Policy, calls);
        var authV = new RecordingValidator(StandingOrderValidatorPriority.Authority, calls);
        var conflictV = new RecordingValidator(StandingOrderValidatorPriority.Conflict, calls);

        // Register out of priority order; issuer must sort.
        var (issuer, _, audit, _, _) = Build(conflictV, schemaV, authV, policyV);
        await issuer.IssueAsync(NewDraft("anchor.maui.theme"), ActorA, audit, CancellationToken.None);

        Assert.Equal(
            new[]
            {
                StandingOrderValidatorPriority.Schema,
                StandingOrderValidatorPriority.Policy,
                StandingOrderValidatorPriority.Authority,
                StandingOrderValidatorPriority.Conflict,
            },
            calls);
    }

    [Fact]
    public async Task IssueAsync_NonBlockingIssues_AccumulateButDoNotReject()
    {
        var noisy = new InlineValidator(StandingOrderValidatorPriority.Policy,
            new StandingOrderValidationIssue(StandingOrderValidationSeverity.Warning,
                "anchor.maui.theme", "consider documenting this", null));
        var (issuer, _, audit, _, _) = Build(noisy);
        var order = await issuer.IssueAsync(NewDraft("anchor.maui.theme"), ActorA, audit, CancellationToken.None);

        Assert.Equal(StandingOrderState.Validated, order.State);
    }

    [Fact]
    public async Task RescindAsync_EmitsRescindedRecord_AndOriginalIssuedRecordPreserved()
    {
        var (issuer, _, audit, _, _) = Build();
        var order = await issuer.IssueAsync(NewDraft("anchor.maui.theme"), ActorA, audit, CancellationToken.None);
        var rescinded = await issuer.RescindAsync(order.Id, ActorA, "reverted policy decision", audit, CancellationToken.None);

        Assert.Equal(StandingOrderState.Rescinded, rescinded.State);

        // The repository now holds the rescinded version (state=Rescinded).
        // The audit trail received TWO appends: the original Issued + the
        // new Rescinded record. Audit immutability means the original
        // record was not mutated/redacted.
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.StandingOrderIssued)),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.StandingOrderRescinded)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RescindAsync_UnknownId_Throws()
    {
        var (issuer, _, audit, _, _) = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await issuer.RescindAsync(new StandingOrderId(Guid.NewGuid()), ActorA, "n/a", audit, CancellationToken.None));
    }

    [Fact]
    public async Task IssueAsync_AuditPayload_ContainsExpectedKeys()
    {
        var (issuer, _, audit, _, _) = Build();
        AuditRecord? captured = null;
        await audit.AppendAsync(Arg.Do<AuditRecord>(r => captured ??= r), Arg.Any<CancellationToken>());

        await issuer.IssueAsync(NewDraft("anchor.maui.theme"), ActorA, audit, CancellationToken.None);

        Assert.NotNull(captured);
        var body = captured!.Payload.Payload.Body;
        Assert.True(body.ContainsKey("standing_order_id"));
        Assert.True(body.ContainsKey("tenant_id"));
        Assert.True(body.ContainsKey("issued_by"));
        Assert.True(body.ContainsKey("scope"));
        Assert.True(body.ContainsKey("rationale"));
        Assert.True(body.ContainsKey("triple_count"));
        Assert.True(body.ContainsKey("state"));
    }

    [Fact]
    public async Task IssueAsync_NullDraft_ThrowsArgumentNullException()
    {
        var (issuer, _, audit, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await issuer.IssueAsync(null!, ActorA, audit, CancellationToken.None));
    }

    // ===== W#57 — Standing-Order applied-event publish =====

    [Fact]
    public async Task IssueAsync_AcceptedOrder_PublishesStandingOrderAppliedEvent()
    {
        var (issuer, _, audit, _, eventStream) = Build();
        var order = await issuer.IssueAsync(NewDraft("anchor.maui.theme"), ActorA, audit, CancellationToken.None);

        var events = eventStream.ReplayAll();
        Assert.Single(events);
        var evt = events[0];
        Assert.Equal(order.Id, evt.StandingOrderId);
        Assert.Equal(TenantA, evt.TenantId);
        Assert.Equal(ActorA, evt.IssuedBy);
        Assert.Equal(order.Scope, evt.Scope);
        Assert.Equal(order.Triples, evt.Triples);
        Assert.Equal(order.Rationale, evt.Rationale);
    }

    [Fact]
    public async Task IssueAsync_RejectedOrder_DoesNotPublishAppliedEvent()
    {
        var blocker = new InlineValidator(StandingOrderValidatorPriority.Policy,
            new StandingOrderValidationIssue(StandingOrderValidationSeverity.Block,
                "anchor.maui.theme", "not allowed", null));
        var (issuer, _, audit, _, eventStream) = Build(blocker);

        await issuer.IssueAsync(NewDraft("anchor.maui.theme"), ActorA, audit, CancellationToken.None);

        Assert.Empty(eventStream.ReplayAll());
    }

    [Fact]
    public async Task RescindAsync_DoesNotPublishAppliedEvent()
    {
        var (issuer, _, audit, _, eventStream) = Build();
        var order = await issuer.IssueAsync(NewDraft("anchor.maui.theme"), ActorA, audit, CancellationToken.None);
        // Issue published 1 event; rescind must NOT publish a second.
        Assert.Single(eventStream.ReplayAll());

        await issuer.RescindAsync(order.Id, ActorA, "reverted", audit, CancellationToken.None);
        Assert.Single(eventStream.ReplayAll());
    }

    // ===== Test helpers =====

    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeSigner : IOperationSigner
    {
        private static readonly byte[] ZeroPubKey = new byte[32];
        private static readonly byte[] ZeroSig = new byte[64];

        public PrincipalId IssuerId { get; } = PrincipalId.FromBytes(ZeroPubKey);

        public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)
        {
            var signed = new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, Signature.FromBytes(ZeroSig));
            return ValueTask.FromResult(signed);
        }
    }

    private sealed class InlineValidator : IStandingOrderValidator
    {
        private readonly StandingOrderValidationIssue[] _issues;

        public InlineValidator(StandingOrderValidatorPriority priority, params StandingOrderValidationIssue[] issues)
        {
            Priority = priority;
            _issues = issues;
        }

        public StandingOrderValidatorPriority Priority { get; }

        public ValueTask<StandingOrderValidationResult> ValidateAsync(
            StandingOrder order, StandingOrderContext context, CancellationToken ct)
        {
            var accepted = !_issues.Any(i => i.Severity == StandingOrderValidationSeverity.Block);
            return ValueTask.FromResult(new StandingOrderValidationResult(accepted, _issues));
        }
    }

    private sealed class RecordingValidator : IStandingOrderValidator
    {
        private readonly List<StandingOrderValidatorPriority> _calls;

        public RecordingValidator(StandingOrderValidatorPriority priority, List<StandingOrderValidatorPriority> calls)
        {
            Priority = priority;
            _calls = calls;
        }

        public StandingOrderValidatorPriority Priority { get; }

        public ValueTask<StandingOrderValidationResult> ValidateAsync(
            StandingOrder order, StandingOrderContext context, CancellationToken ct)
        {
            _calls.Add(Priority);
            return ValueTask.FromResult(new StandingOrderValidationResult(true,
                Array.Empty<StandingOrderValidationIssue>()));
        }
    }
}
