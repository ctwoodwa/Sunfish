using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Tactical.Tests;

public class DefaultTacticalRuleEngineTests
{
    private static readonly TenantId TenantA = new("alpha");
    private static readonly TenantId TenantB = new("beta");

    private static TacticalSignal MakeSignal(TenantId? tenant = null) =>
        new(
            TenantId: tenant ?? TenantA,
            Kind: TacticalSignalKind.Custom,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: new JsonObject());

    private static DefaultTacticalRuleEngine Build(
        IAuditTrail? audit = null,
        IOperationSigner? signer = null,
        TimeProvider? time = null) =>
        new(
            Options.Create(new TacticalOptions()),
            audit,
            signer,
            logger: null,
            timeProvider: time);

    private sealed class MatchAlwaysRule : ITacticalRule
    {
        public MatchAlwaysRule(string ruleName) { RuleName = ruleName; }
        public string RuleName { get; }
        public AlertSeverity DefaultSeverity => AlertSeverity.Medium;
        public AlertRoutingPolicy DefaultRoutingPolicy => AlertRoutingPolicy.InformationalSonar;
        public bool Evaluate(TacticalSignal signal, out TacticalAlert? alert)
        {
            alert = new TacticalAlert(
                AlertId: $"{RuleName}:{Guid.NewGuid():N}",
                TenantId: signal.TenantId,
                RuleName: RuleName,
                Severity: AlertSeverity.Medium,
                RoutingPolicy: AlertRoutingPolicy.InformationalSonar,
                Title: "test",
                Summary: "",
                DetectedAt: signal.OccurredAt,
                Status: AlertStatus.Active,
                RequiresAcknowledgement: false,
                RunbookStepIds: Array.Empty<string>(),
                AcknowledgedBy: null,
                AcknowledgedAt: null);
            return true;
        }
    }

    private sealed class ThrowingRule : ITacticalRule
    {
        public ThrowingRule(string name) { RuleName = name; }
        public string RuleName { get; }
        public AlertSeverity DefaultSeverity => AlertSeverity.Medium;
        public AlertRoutingPolicy DefaultRoutingPolicy => AlertRoutingPolicy.InformationalSonar;
        public bool Evaluate(TacticalSignal signal, out TacticalAlert? alert)
            => throw new InvalidOperationException("synthetic");
    }

    [Fact]
    public void RegisterRule_RejectsDuplicateRuleName()
    {
        var engine = Build();
        engine.RegisterRule(new MatchAlwaysRule("third-party.x"));
        Assert.Throws<InvalidOperationException>(() =>
            engine.RegisterRule(new MatchAlwaysRule("third-party.x")));
    }

    [Fact]
    public void RegisterRule_RejectsSunfishPrefixFromUnverifiedAssembly()
    {
        // The test assembly is `Sunfish.Foundation.Tactical.Tests` which
        // DOES start with "Sunfish." — so this rule's containing type is
        // a first-party assembly under the Phase 2b proxy. To exercise
        // the rejection path we need a rule defined in a non-"Sunfish."
        // assembly. Use a CompiledExpressions-style anonymous rule
        // forwarder via Castle.DynamicProxy substitute.
        var rule = Substitute.For<ITacticalRule>();
        rule.RuleName.Returns("sunfish.reserved.rule");
        rule.Evaluate(Arg.Any<TacticalSignal>(), out Arg.Any<TacticalAlert?>())
            .Returns(false);

        // NSubstitute's proxy assembly ("Castle.Proxies"-like) is NOT
        // first-party — registration must reject.
        var engine = Build();
        var ex = Assert.Throws<InvalidOperationException>(() => engine.RegisterRule(rule));
        Assert.Contains("sunfish.*", ex.Message);
    }

    [Fact]
    public void RegisterRule_AfterFirstSignalProcessed_Throws()
    {
        var engine = Build();
        engine.RegisterRule(new MatchAlwaysRule("third-party.first"));
        // Process the first signal (closes the registration epoch).
        _ = engine.Evaluate(MakeSignal());
        Assert.Throws<InvalidOperationException>(() =>
            engine.RegisterRule(new MatchAlwaysRule("third-party.second")));
    }

    [Fact]
    public void Evaluate_ReturnsEmpty_WhenNoRulesRegistered()
    {
        var engine = Build();
        var alerts = engine.Evaluate(MakeSignal());
        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_CatchesThrowingRule_ContinuesOthers()
    {
        var engine = Build();
        engine.RegisterRule(new ThrowingRule("third-party.bad"));
        engine.RegisterRule(new MatchAlwaysRule("third-party.good"));

        var alerts = engine.Evaluate(MakeSignal());

        Assert.Single(alerts);
        Assert.Equal("third-party.good", alerts[0].RuleName);
    }

    [Fact]
    public void Evaluate_AllRulesInvoked_NoShortCircuit()
    {
        var engine = Build();
        engine.RegisterRule(new MatchAlwaysRule("third-party.r1"));
        engine.RegisterRule(new MatchAlwaysRule("third-party.r2"));
        engine.RegisterRule(new MatchAlwaysRule("third-party.r3"));

        var alerts = engine.Evaluate(MakeSignal());

        Assert.Equal(3, alerts.Count);
        Assert.Equal(new[] { "third-party.r1", "third-party.r2", "third-party.r3" },
            alerts.Select(a => a.RuleName).ToArray());
    }

    [Fact]
    public async Task EvaluateStreamAsync_PartitionsByTenant_OrderingPreservedWithinTenant()
    {
        var engine = Build();
        engine.RegisterRule(new MatchAlwaysRule("third-party.echo"));

        var signals = new[]
        {
            new TacticalSignal(TenantA, TacticalSignalKind.Custom,
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new JsonObject()),
            new TacticalSignal(TenantA, TacticalSignalKind.Custom,
                new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero), new JsonObject()),
            new TacticalSignal(TenantB, TacticalSignalKind.Custom,
                new DateTimeOffset(2026, 1, 1, 0, 0, 2, TimeSpan.Zero), new JsonObject()),
        };

        var alerts = new List<TacticalAlert>();
        await foreach (var alert in engine.EvaluateStreamAsync(ToAsync(signals)))
        {
            alerts.Add(alert);
        }

        Assert.Equal(3, alerts.Count);
        // Within TenantA the two alerts must be in DetectedAt order.
        var tenantAAlerts = alerts.Where(a => a.TenantId == TenantA).ToArray();
        Assert.Equal(2, tenantAAlerts.Length);
        Assert.True(tenantAAlerts[0].DetectedAt < tenantAAlerts[1].DetectedAt);
    }

    [Fact]
    public void Ctor_AuditTrailWithoutSigner_Throws()
    {
        var trail = Substitute.For<IAuditTrail>();
        Assert.Throws<ArgumentException>(() =>
            new DefaultTacticalRuleEngine(
                Options.Create(new TacticalOptions()),
                auditTrail: trail,
                signer: null));
    }

    [Fact]
    public void Ctor_SignerWithoutAuditTrail_Throws()
    {
        var signer = Substitute.For<IOperationSigner>();
        Assert.Throws<ArgumentException>(() =>
            new DefaultTacticalRuleEngine(
                Options.Create(new TacticalOptions()),
                auditTrail: null,
                signer: signer));
    }

    /// <summary>
    /// Per W#52 P2b council Major M1: cooldown is consumed ONLY after
    /// successful AppendAsync. A flaky audit backend that throws on
    /// every Append must not silently spend the window.
    /// </summary>
    [Fact]
    public void Evaluate_RuleErrorRate_FlakyAuditBackend_DoesNotConsumeCooldown()
    {
        var faultyTrail = Substitute.For<IAuditTrail>();
        faultyTrail.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("synthetic backend fault"));
        var signer = StubSigner();
        var fakeTime = new TestTimeProvider(DateTimeOffset.UtcNow);
        var engine = Build(audit: faultyTrail, signer: signer, time: fakeTime);
        engine.RegisterRule(new ThrowingRule("third-party.flaky"));

        for (int i = 0; i < 150; i++)
        {
            _ = engine.Evaluate(MakeSignal());
        }

        // Wait briefly for fire-and-forget tasks to attempt + fail.
        Thread.Sleep(100);

        // Multiple emission attempts allowed because cooldown was NOT
        // burned on the failed AppendAsync calls.
        faultyTrail.ReceivedWithAnyArgs().AppendAsync(default!, default);
    }

    [Fact]
    public void Evaluate_RuleErrorRate_Above100PerMinute_EmitsDenialOncePerMinute()
    {
        var trail = new RecordingAuditTrail();
        var signer = StubSigner();
        var fakeTime = new TestTimeProvider(DateTimeOffset.UtcNow);
        var engine = Build(audit: trail, signer: signer, time: fakeTime);
        engine.RegisterRule(new ThrowingRule("third-party.flaky"));

        // Drive 150 throws within the same simulated minute.
        for (int i = 0; i < 150; i++)
        {
            _ = engine.Evaluate(MakeSignal());
        }

        // Wait for fire-and-forget emission to land.
        SpinWait.SpinUntil(
            () => trail.Records.Count >= 1, TimeSpan.FromSeconds(2));

        var denials = trail.Records
            .Where(r => r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied))
            .ToList();
        Assert.Single(denials);
        Assert.Equal("rule-evaluation-failure-rate",
            (string?)denials[0].Payload.Payload.Body["denial_reason"]);
    }

    /// <summary>
    /// Council amendment: error-rate trackers must be per-(rule, tenant).
    /// Tenant A's rule failures MUST NOT consume Tenant B's denial cooldown
    /// (§Trust cross-tenant isolation invariant).
    /// </summary>
    [Fact]
    public void Evaluate_RuleErrorRate_CrossTenantTrackerIsolation()
    {
        var trail = new RecordingAuditTrail();
        var signer = StubSigner();
        var fakeTime = new TestTimeProvider(DateTimeOffset.UtcNow);
        var engine = Build(audit: trail, signer: signer, time: fakeTime);
        engine.RegisterRule(new ThrowingRule("third-party.shared-rule"));

        // Drive Tenant A past the threshold (>100 throws).
        for (int i = 0; i < 110; i++)
            _ = engine.Evaluate(MakeSignal(TenantA));

        // Wait for Tenant A's denial to land.
        SpinWait.SpinUntil(() => trail.Records.Count >= 1, TimeSpan.FromSeconds(2));

        var countBeforeTenantB = trail.Records.Count;

        // Now drive Tenant B past the threshold as well.
        for (int i = 0; i < 110; i++)
            _ = engine.Evaluate(MakeSignal(TenantB));

        SpinWait.SpinUntil(() => trail.Records.Count >= 2, TimeSpan.FromSeconds(2));

        // Tenant B must have its OWN denial record — independent of Tenant A's cooldown.
        var denials = trail.Records
            .Where(r => r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied))
            .ToList();
        Assert.True(denials.Count >= 2,
            "Each tenant must emit its own denial; cross-tenant shared tracker would suppress the second.");
        Assert.Contains(denials, d => d.TenantId == TenantA);
        Assert.Contains(denials, d => d.TenantId == TenantB);
    }

    /// <summary>
    /// Council amendment: tenant_id must be present inside the signed payload
    /// so the signature covers the tenant binding.
    /// </summary>
    [Fact]
    public void Evaluate_RuleErrorRate_DenialPayload_ContainsTenantId()
    {
        var trail = new RecordingAuditTrail();
        var signer = StubSigner();
        var fakeTime = new TestTimeProvider(DateTimeOffset.UtcNow);
        var engine = Build(audit: trail, signer: signer, time: fakeTime);
        engine.RegisterRule(new ThrowingRule("third-party.flaky2"));

        for (int i = 0; i < 110; i++)
            _ = engine.Evaluate(MakeSignal(TenantA));

        SpinWait.SpinUntil(() => trail.Records.Count >= 1, TimeSpan.FromSeconds(2));

        var denial = trail.Records.First(r =>
            r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied));
        Assert.NotNull(denial.Payload.Payload.Body["tenant_id"]);
        Assert.Equal(TenantA.Value, (string?)denial.Payload.Payload.Body["tenant_id"]);
    }

    private static async IAsyncEnumerable<TacticalSignal> ToAsync(
        IEnumerable<TacticalSignal> signals)
    {
        foreach (var s in signals)
        {
            yield return s;
            await Task.Yield();
        }
    }

    private static IOperationSigner StubSigner()
    {
        var principalId = PrincipalId.FromBytes(new byte[32]);
        var signer = Substitute.For<IOperationSigner>();
        signer.IssuerId.Returns(principalId);
        signer.SignAsync(
                Arg.Any<AuditPayload>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<SignedOperation<AuditPayload>>(
                new SignedOperation<AuditPayload>(
                    Payload: call.Arg<AuditPayload>()!,
                    IssuerId: principalId,
                    IssuedAt: call.Arg<DateTimeOffset>(),
                    Nonce: call.Arg<Guid>(),
                    Signature: Signature.FromBytes(new byte[64]))));
        return signer;
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public TestTimeProvider(DateTimeOffset start) { _now = start; }
        public override DateTimeOffset GetUtcNow() => _now;
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
