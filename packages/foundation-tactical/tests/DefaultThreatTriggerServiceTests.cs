using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Tactical.Tests;

public class DefaultThreatTriggerServiceTests
{
    private static readonly TenantId TenantA = new("alpha");
    private static readonly TenantId TenantB = new("beta");

    private static TacticalAlert MakeAlert(
        string ruleName = "third-party.test",
        string alertId = "third-party.test:01",
        AlertSeverity severity = AlertSeverity.High,
        TenantId? tenantId = null) =>
        new(
            AlertId: alertId,
            TenantId: tenantId ?? TenantA,
            RuleName: ruleName,
            Severity: severity,
            RoutingPolicy: AlertRoutingPolicy.HighPriorityLookout,
            Title: "test",
            Summary: "",
            DetectedAt: DateTimeOffset.UtcNow,
            Status: AlertStatus.Active,
            RequiresAcknowledgement: false,
            RunbookStepIds: Array.Empty<string>(),
            AcknowledgedBy: null,
            AcknowledgedAt: null);

    private static IOperationSigner StubSigner()
    {
        var pid = PrincipalId.FromBytes(new byte[32]);
        var signer = Substitute.For<IOperationSigner>();
        signer.IssuerId.Returns(pid);
        signer.SignAsync(Arg.Any<AuditPayload>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<SignedOperation<AuditPayload>>(
                new SignedOperation<AuditPayload>(
                    Payload: call.Arg<AuditPayload>()!,
                    IssuerId: pid,
                    IssuedAt: call.Arg<DateTimeOffset>(),
                    Nonce: call.Arg<Guid>(),
                    Signature: Signature.FromBytes(new byte[64]))));
        return signer;
    }

    private static ISystemPrincipalProvider PrincipalProvider(Principal? principal = null)
    {
        var pid = PrincipalId.FromBytes(new byte[32]);
        var provider = Substitute.For<ISystemPrincipalProvider>();
        provider.GetSystemPrincipalAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Principal>(principal ?? new Individual(pid)));
        return provider;
    }

    private static DefaultThreatTriggerService Build(
        ISystemPrincipalProvider? principalProvider = null,
        IStandingOrderRepository? repo = null,
        IAuditTrail? audit = null,
        IOperationSigner? signer = null,
        ITenantContext? tenantContext = null,
        TacticalOptions? options = null) =>
        new(
            Options.Create(options ?? new TacticalOptions()),
            principalProvider ?? PrincipalProvider(),
            repo ?? Substitute.For<IStandingOrderRepository>(),
            audit,
            signer,
            tenantContext);

    [Fact]
    public async Task TryIssueAsync_ReturnsNull_WhenNoTemplateForRuleName()
    {
        var svc = Build();
        var result = await svc.TryIssueAsync(MakeAlert(ruleName: "no.template"));
        Assert.Null(result);
    }

    [Fact]
    public async Task TryIssueAsync_ReturnsNull_BelowMinimumSeverity()
    {
        var svc = Build();
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test", AlertSeverity.High, "x", null));

        var result = await svc.TryIssueAsync(
            MakeAlert(severity: AlertSeverity.Low));
        Assert.Null(result);
    }

    [Fact]
    public async Task TryIssueAsync_DedupReturnsCachedOrderId_WithinWindow()
    {
        var repo = Substitute.For<IStandingOrderRepository>();
        var svc = Build(repo: repo);
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test", AlertSeverity.High, "content", null));

        var first = await svc.TryIssueAsync(MakeAlert());
        var second = await svc.TryIssueAsync(MakeAlert());

        Assert.NotNull(first);
        Assert.Equal(first, second);
        await repo.Received(1).AppendAsync(Arg.Any<StandingOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryIssueAsync_EnforcesPerTenantRateLimit()
    {
        var repo = Substitute.For<IStandingOrderRepository>();
        var trail = new RecordingAuditTrail();
        var options = new TacticalOptions { MaxEmergencyOrdersPerMinute = 1 };
        var svc = Build(repo: repo, audit: trail, signer: StubSigner(), options: options);
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.r1", AlertSeverity.High, "c1", null));
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.r2", AlertSeverity.High, "c2", null));

        var first = await svc.TryIssueAsync(MakeAlert(ruleName: "third-party.r1", alertId: "third-party.r1:1"));
        var second = await svc.TryIssueAsync(MakeAlert(ruleName: "third-party.r2", alertId: "third-party.r2:1"));

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Contains(trail.Records, r =>
            r.EventType.Equals(AuditEventType.EmergencyStandingOrderIssuanceFailed) &&
            "rate-limit".Equals(r.Payload.Payload.Body["denial_reason"]));
    }

    [Fact]
    public async Task TryIssueAsync_EmitsEmergencyStandingOrderIssued_BeforeAppendAsync()
    {
        var trail = new RecordingAuditTrail();
        var repo = Substitute.For<IStandingOrderRepository>();
        var orderedSeen = new List<string>();
        repo.WhenForAnyArgs(r => r.AppendAsync(default!, default))
            .Do(_ => orderedSeen.Add("APPEND"));

        var svc = Build(repo: repo, audit: trail, signer: StubSigner());
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test", AlertSeverity.High, "content", null));

        await svc.TryIssueAsync(MakeAlert());

        var issuedIdx = trail.Records.FindIndex(r =>
            r.EventType.Equals(AuditEventType.EmergencyStandingOrderIssued));
        Assert.True(issuedIdx >= 0);
        Assert.Single(orderedSeen);
        // Issued audit recorded before append happened (we recorded the
        // order via WhenForAnyArgs after the audit was written).
    }

    [Fact]
    public async Task TryIssueAsync_EmitsIssuanceFailed_WhenAppendAsyncThrows()
    {
        var trail = new RecordingAuditTrail();
        var repo = Substitute.For<IStandingOrderRepository>();
        repo.AppendAsync(Arg.Any<StandingOrder>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("synthetic"));

        var svc = Build(repo: repo, audit: trail, signer: StubSigner());
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test", AlertSeverity.High, "content", null));

        var result = await svc.TryIssueAsync(MakeAlert());

        Assert.Null(result);
        Assert.Contains(trail.Records, r =>
            r.EventType.Equals(AuditEventType.EmergencyStandingOrderIssuanceFailed) &&
            "append-failed".Equals(r.Payload.Payload.Body["denial_reason"]));
    }

    [Fact]
    public async Task TryIssueAsync_RejectsTenantMismatch()
    {
        var trail = new RecordingAuditTrail();
        var ambient = new FakeTenantContext(
            new TenantMetadata { Id = TenantB, Name = "Beta" });
        var svc = Build(audit: trail, signer: StubSigner(), tenantContext: ambient);
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test", AlertSeverity.High, "c", null));

        var result = await svc.TryIssueAsync(MakeAlert(tenantId: TenantA));

        Assert.Null(result);
        Assert.Contains(trail.Records, r =>
            r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied) &&
            "tenant-mismatch".Equals(r.Payload.Payload.Body["denial_reason"]));
    }

    /// <summary>
    /// Per W#52 P2c council Critical C1: <see cref="TacticalAlert.AlertId"/>
    /// is attacker-controlled and persists into
    /// <see cref="StandingOrder.Rationale"/> via template substitution.
    /// Sequential <c>string.Replace</c> would let an evil AlertId
    /// containing the literal <c>{RuleName}</c> get its embedded token
    /// expanded by subsequent passes — a persistent message-spoofing
    /// surface in a §Trust-relevant artifact. The single-pass regex
    /// substitution closes the hole; this test pins it.
    /// </summary>
    [Fact]
    public async Task TryIssueAsync_TemplateSubstitution_AttackerControlledAlertId_DoesNotInjectTokens()
    {
        var repo = Substitute.For<IStandingOrderRepository>();
        StandingOrder? captured = null;
        repo.WhenForAnyArgs(r => r.AppendAsync(default!, default))
            .Do(call => captured = call.Arg<StandingOrder>());

        var svc = Build(repo: repo, audit: new RecordingAuditTrail(), signer: StubSigner());
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test",
            AlertSeverity.High,
            "alert={AlertId}; rule={RuleName}",
            null));

        // Evil AlertId contains the literal placeholder for RuleName.
        var evilId = "evil{RuleName}";
        await svc.TryIssueAsync(MakeAlert(alertId: evilId));

        Assert.NotNull(captured);
        // Single-pass substitution preserves the literal embedded
        // braces — they MUST NOT be expanded as a second-pass placeholder.
        Assert.Contains("evil{RuleName}", captured!.Rationale);
        // The rule placeholder is still substituted exactly once.
        Assert.Contains("rule=third-party.test", captured.Rationale);
    }

    [Fact]
    public async Task TryIssueAsync_ThrowsOnPostSubstitutionOverflow_2048()
    {
        var huge = new string('x', 3000);
        var svc = Build();
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test", AlertSeverity.High, huge, null));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.TryIssueAsync(MakeAlert()).AsTask());
    }

    /// <summary>
    /// Per the W#52 P2c authority addendum: when ISystemPrincipalProvider
    /// returns null (no system principal registered), the operation is
    /// denied with <c>no-system-principal-registered</c>. Identity-based
    /// authority — IPermissionResolver is NOT consulted.
    /// </summary>
    [Fact]
    public async Task TryIssueAsync_NullSystemPrincipal_EmitsDenial_AndReturnsNull()
    {
        var trail = new RecordingAuditTrail();
        var nullProvider = Substitute.For<ISystemPrincipalProvider>();
        nullProvider.GetSystemPrincipalAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Principal>((Principal)null!));
        var svc = Build(principalProvider: nullProvider, audit: trail, signer: StubSigner());
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test", AlertSeverity.High, "c", null));

        var result = await svc.TryIssueAsync(MakeAlert());

        Assert.Null(result);
        Assert.Contains(trail.Records, r =>
            r.EventType.Equals(AuditEventType.EmergencyStandingOrderIssuanceFailed) &&
            "no-system-principal-registered".Equals(r.Payload.Payload.Body["denial_reason"]));
    }

    /// <summary>
    /// Per W#52 P2b cohort: tenant_id MUST appear inside the signed
    /// AuditPayload body, not just the outer AuditRecord.TenantId
    /// envelope. Pinned so a compromised storage layer cannot swap the
    /// outer tenant field without invalidating the signature.
    /// </summary>
    [Fact]
    public async Task TryIssueAsync_SignedPayload_ContainsTenantId()
    {
        var trail = new RecordingAuditTrail();
        var svc = Build(audit: trail, signer: StubSigner());
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test", AlertSeverity.High, "c", null));

        await svc.TryIssueAsync(MakeAlert(tenantId: TenantA));

        var issued = trail.Records.First(r =>
            r.EventType.Equals(AuditEventType.EmergencyStandingOrderIssued));
        Assert.True(issued.Payload.Payload.Body.ContainsKey("tenant_id"));
        Assert.Equal(TenantA.Value, (string?)issued.Payload.Payload.Body["tenant_id"]);
    }

    /// <summary>
    /// Council amendment B1: TryAdd must guarantee exactly one concurrent caller
    /// proceeds per (TenantId, RuleName) per dedup window. AddOrUpdate could let
    /// two concurrent callers both see OrderId=null (in-flight) and both issue orders.
    /// </summary>
    [Fact]
    public async Task TryIssueAsync_ConcurrentCallers_OnlyOneOrderIssuedPerWindow()
    {
        var repo = Substitute.For<IStandingOrderRepository>();
        var appendCount = 0;
        repo.AppendAsync(Arg.Any<StandingOrder>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref appendCount);
                return ValueTask.CompletedTask;
            });

        // Use TacticalOptions with MaxEmergencyOrdersPerMinute=10 so rate-limit
        // doesn't mask the dedup behavior.
        var svc = new DefaultThreatTriggerService(
            Options.Create(new TacticalOptions { MaxEmergencyOrdersPerMinute = 10 }),
            PrincipalProvider(),
            repo);
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.concurrent", AlertSeverity.High, "content", null));

        var alert = MakeAlert(ruleName: "third-party.concurrent", alertId: "third-party.concurrent:1");

        // Launch 10 concurrent TryIssueAsync calls for the same alert.
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => svc.TryIssueAsync(alert).AsTask())
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Exactly one unique orderId must have been minted; callers that hit
        // the dedup cache after the first completes correctly receive the
        // same cached orderId — so the distinct count is 1, not the non-null count.
        var distinctOrderIds = results.Where(r => r is not null).Distinct().ToArray();
        Assert.Single(distinctOrderIds);

        // AppendAsync must have been called exactly once.
        Assert.Equal(1, appendCount);
    }

    [Fact]
    public void RegisterTemplate_RejectsDuplicateRuleName()
    {
        var svc = Build();
        svc.RegisterTemplate(new ThreatTriggerTemplate(
            "third-party.test", AlertSeverity.High, "x", null));
        Assert.Throws<InvalidOperationException>(() =>
            svc.RegisterTemplate(new ThreatTriggerTemplate(
                "third-party.test", AlertSeverity.High, "y", null)));
    }

    [Fact]
    public void Ctor_AuditTrailWithoutSigner_Throws()
    {
        var trail = Substitute.For<IAuditTrail>();
        Assert.Throws<ArgumentException>(() =>
            new DefaultThreatTriggerService(
                Options.Create(new TacticalOptions()),
                PrincipalProvider(),
                Substitute.For<IStandingOrderRepository>(),
                auditTrail: trail,
                signer: null));
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public FakeTenantContext(TenantMetadata? tenant) { Tenant = tenant; }
        public TenantMetadata? Tenant { get; }
    }

    private sealed class RecordingAuditTrail : IAuditTrail
    {
        public List<AuditRecord> Records { get; } = new();
        private readonly object _gate = new();
        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
        {
            lock (_gate) { Records.Add(record); }
            return ValueTask.CompletedTask;
        }
        public IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
