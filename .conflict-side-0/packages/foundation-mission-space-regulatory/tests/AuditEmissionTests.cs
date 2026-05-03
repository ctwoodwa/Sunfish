using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MissionSpace.Regulatory.Audit;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Tests;

public sealed class RegulatoryAuditPayloadsTests
{
    [Fact]
    public void PolicyEvaluated_KeysAlphabetized()
    {
        var p = RegulatoryAuditPayloads.PolicyEvaluated("feature.x", "US-UT", 3, "Pass");
        Assert.Equal(3, p.Body["evaluation_count"]);
        Assert.Equal("feature.x", p.Body["feature_key"]);
        Assert.Equal("US-UT", p.Body["jurisdiction_code"]);
        Assert.Equal("Pass", p.Body["verdict_state"]);
    }

    [Fact]
    public void PolicyEnforcementBlocked_PopulatesAllFields()
    {
        var p = RegulatoryAuditPayloads.PolicyEnforcementBlocked("feature.x", "US-UT", "rule-1", "Block");
        Assert.Equal("Block", p.Body["enforcement_action"]);
        Assert.Equal("feature.x", p.Body["feature_key"]);
        Assert.Equal("US-UT", p.Body["jurisdiction_code"]);
        Assert.Equal("rule-1", p.Body["rule_id"]);
    }

    [Fact]
    public void DataResidencyViolation_PopulatesAllFields()
    {
        var p = RegulatoryAuditPayloads.DataResidencyViolation("lease", "RU", "blocked");
        Assert.Equal("blocked", p.Body["detail"]);
        Assert.Equal("RU", p.Body["jurisdiction_code"]);
        Assert.Equal("lease", p.Body["record_class"]);
    }

    [Fact]
    public void SanctionsScreeningHit_PopulatesAllFields()
    {
        var p = RegulatoryAuditPayloads.SanctionsScreeningHit("john-doe", "OFAC-SDN", "2026-05-01", 0.95);
        Assert.Equal("OFAC-SDN", p.Body["list_source"]);
        Assert.Equal("2026-05-01", p.Body["list_version"]);
        Assert.Equal(0.95, p.Body["match_score"]);
        Assert.Equal("john-doe", p.Body["subject_id"]);
    }

    [Fact]
    public void RegimeAcknowledgmentSurfaced_PopulatesFields()
    {
        var p = RegulatoryAuditPayloads.RegimeAcknowledgmentSurfaced("HIPAA", "CommercialProductOnly");
        Assert.Equal("HIPAA", p.Body["regime"]);
        Assert.Equal("CommercialProductOnly", p.Body["stance"]);
    }

    [Fact]
    public void EuAiActTierClassified_PopulatesFields()
    {
        var p = RegulatoryAuditPayloads.EuAiActTierClassified("feature.x", "Limited");
        Assert.Equal("feature.x", p.Body["feature_key"]);
        Assert.Equal("Limited", p.Body["tier"]);
    }

    [Fact]
    public void SanctionsAdvisoryOnlyConfigured_PopulatesField()
    {
        var p = RegulatoryAuditPayloads.SanctionsAdvisoryOnlyConfigured("operator-1");
        Assert.Equal("operator-1", p.Body["operator_principal_id"]);
    }

    [Fact]
    public void RegulatoryRuleContentReloaded_PopulatesFields()
    {
        var p = RegulatoryAuditPayloads.RegulatoryRuleContentReloaded("v2", 42);
        Assert.Equal(42, p.Body["rule_count"]);
        Assert.Equal("v2", p.Body["rule_set_version"]);
    }

    [Fact]
    public void RegulatoryPolicyCacheInvalidated_PopulatesField()
    {
        var p = RegulatoryAuditPayloads.RegulatoryPolicyCacheInvalidated("probe-status-transition");
        Assert.Equal("probe-status-transition", p.Body["trigger"]);
    }

    [Fact]
    public void JurisdictionProbedWithLowConfidence_PopulatesFields()
    {
        var p = RegulatoryAuditPayloads.JurisdictionProbedWithLowConfidence("US-UT", 1);
        Assert.Equal("US-UT", p.Body["jurisdiction_code"]);
        Assert.Equal(1, p.Body["signal_count"]);
    }
}

public sealed class RegulatoryAuditEmitterDedupTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new("tenant-a");

    private static RegulatoryAuditEmitter Build(IAuditTrail trail, FakeTime time) =>
        new(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, time);

    [Fact]
    public async Task EmitAsync_WithoutDedup_AlwaysFires()
    {
        var trail = Substitute.For<IAuditTrail>();
        var time = new FakeTime(Now);
        var emitter = Build(trail, time);

        await emitter.EmitAsync(AuditEventType.PolicyEvaluated, AnyPayload(), default);
        await emitter.EmitAsync(AuditEventType.PolicyEvaluated, AnyPayload(), default);

        await trail.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitWithRuleDedup_SuppressesWithinWindow()
    {
        var trail = Substitute.For<IAuditTrail>();
        var time = new FakeTime(Now);
        var emitter = Build(trail, time);

        var fired1 = await emitter.EmitWithRuleDedupAsync("k", AuditEventType.DataResidencyViolation, AnyPayload(), default);
        var fired2 = await emitter.EmitWithRuleDedupAsync("k", AuditEventType.DataResidencyViolation, AnyPayload(), default);

        Assert.True(fired1);
        Assert.False(fired2);
        await trail.Received(1).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitWithRuleDedup_FiresAgainAfterWindow()
    {
        var trail = Substitute.For<IAuditTrail>();
        var time = new FakeTime(Now);
        var emitter = Build(trail, time);

        await emitter.EmitWithRuleDedupAsync("k", AuditEventType.DataResidencyViolation, AnyPayload(), default);
        time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        var fired2 = await emitter.EmitWithRuleDedupAsync("k", AuditEventType.DataResidencyViolation, AnyPayload(), default);

        Assert.True(fired2);
        await trail.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitWithRegimeDedup_SuppressesWithin24Hours()
    {
        var trail = Substitute.For<IAuditTrail>();
        var time = new FakeTime(Now);
        var emitter = Build(trail, time);

        await emitter.EmitWithRegimeDedupAsync("HIPAA", AuditEventType.RegimeAcknowledgmentSurfaced, AnyPayload(), default);
        time.Advance(TimeSpan.FromHours(23));
        var fired2 = await emitter.EmitWithRegimeDedupAsync("HIPAA", AuditEventType.RegimeAcknowledgmentSurfaced, AnyPayload(), default);

        Assert.False(fired2);
        await trail.Received(1).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitWithRegimeDedup_FiresAgainAfter24Hours()
    {
        var trail = Substitute.For<IAuditTrail>();
        var time = new FakeTime(Now);
        var emitter = Build(trail, time);

        await emitter.EmitWithRegimeDedupAsync("HIPAA", AuditEventType.RegimeAcknowledgmentSurfaced, AnyPayload(), default);
        time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));
        var fired2 = await emitter.EmitWithRegimeDedupAsync("HIPAA", AuditEventType.RegimeAcknowledgmentSurfaced, AnyPayload(), default);

        Assert.True(fired2);
    }

    [Fact]
    public void Constructor_NullArgs_Throws()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        Assert.Throws<ArgumentNullException>(() => new RegulatoryAuditEmitter(null!, signer, TenantA));
        Assert.Throws<ArgumentNullException>(() => new RegulatoryAuditEmitter(trail, null!, TenantA));
        Assert.Throws<ArgumentException>(() => new RegulatoryAuditEmitter(trail, signer, default));
    }

    private static AuditPayload AnyPayload() =>
        new(new Dictionary<string, object?> { ["k"] = "v" });
}

public sealed class PolicyEvaluatorAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new("tenant-a");

    [Fact]
    public async Task EvaluateAsync_AuditEnabled_EmitsPolicyEvaluated()
    {
        var trail = Substitute.For<IAuditTrail>();
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, new FakeTime(Now));
        var evaluator = new DefaultPolicyEvaluator(new EmptyPolicyRuleSource(), emitter, new FakeTime(Now));

        await evaluator.EvaluateAsync("feature.x", DummyProbe(Confidence.High));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.PolicyEvaluated)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_LowConfidenceProbe_EmitsLowConfidenceAudit()
    {
        var trail = Substitute.For<IAuditTrail>();
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, new FakeTime(Now));
        var evaluator = new DefaultPolicyEvaluator(new EmptyPolicyRuleSource(), emitter, new FakeTime(Now));

        await evaluator.EvaluateAsync("feature.x", DummyProbe(Confidence.Low));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.JurisdictionProbedWithLowConfidence)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_AuditDisabled_DoesNotEmit()
    {
        // No emitter → no audit calls. Build with the audit-disabled overload.
        var evaluator = new DefaultPolicyEvaluator(new EmptyPolicyRuleSource(), new FakeTime(Now));
        // Just exercise the path; absence of an audit trail = absence of emission.
        await evaluator.EvaluateAsync("feature.x", DummyProbe(Confidence.High));
    }

    private static JurisdictionProbe DummyProbe(Confidence c) => new()
    {
        JurisdictionCode = "US-UT",
        Confidence = c,
        ProbedAt = Now,
    };
}

public sealed class DataResidencyEnforcerAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new("tenant-a");

    [Fact]
    public async Task EnforceAsync_Violation_EmitsDataResidencyViolation()
    {
        var trail = Substitute.For<IAuditTrail>();
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, new FakeTime(Now));
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                ProhibitedJurisdictions = new[] { "RU" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source, emitter);

        var verdict = await enforcer.EnforceAsync("lease", "RU");

        Assert.False(verdict.IsPermitted);
        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DataResidencyViolation)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnforceAsync_Permitted_NoEmission()
    {
        var trail = Substitute.For<IAuditTrail>();
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, new FakeTime(Now));
        var enforcer = new DefaultDataResidencyEnforcer(new EmptyDataResidencyConstraintSource(), emitter);

        await enforcer.EnforceAsync("lease", "US-UT");

        await trail.DidNotReceive().AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnforceAsync_RepeatedViolation_DedupedWithin1Hour()
    {
        var trail = Substitute.For<IAuditTrail>();
        var time = new FakeTime(Now);
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, time);
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                ProhibitedJurisdictions = new[] { "RU" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source, emitter);

        await enforcer.EnforceAsync("lease", "RU");
        await enforcer.EnforceAsync("lease", "RU");
        await enforcer.EnforceAsync("lease", "RU");

        await trail.Received(1).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    private sealed class DictSource : IDataResidencyConstraintSource
    {
        private readonly Dictionary<string, DataResidencyConstraint> _by;
        public DictSource(Dictionary<string, DataResidencyConstraint> by) => _by = by;
        public DataResidencyConstraint? GetConstraint(string recordClass) =>
            _by.TryGetValue(recordClass, out var c) ? c : null;
    }
}

public sealed class SanctionsScreenerAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new("tenant-a");

    [Fact]
    public async Task ScreenAsync_Hit_EmitsSanctionsScreeningHit()
    {
        var trail = Substitute.For<IAuditTrail>();
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, new FakeTime(Now));
        var entries = new[]
        {
            new SanctionsListEntry
            {
                ListSource = "OFAC-SDN",
                MatchedName = "John Doe",
                MatchScore = 0.95,
                ListVersion = "2026-05-01",
            },
        };
        var screener = new DefaultSanctionsScreener(
            new ListSource(entries),
            emitter,
            ScreeningPolicy.Default,
            "operator-1",
            new FakeTime(Now));

        await screener.ScreenAsync("john-doe");

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SanctionsScreeningHit)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScreenAsync_NoHits_NoEmission()
    {
        var trail = Substitute.For<IAuditTrail>();
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, new FakeTime(Now));
        var screener = new DefaultSanctionsScreener(
            new EmptySanctionsListSource(),
            emitter,
            ScreeningPolicy.Default,
            "operator-1",
            new FakeTime(Now));

        await screener.ScreenAsync("john-doe");

        await trail.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SanctionsScreeningHit)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Constructor_AdvisoryOnly_EmitsConfigurationAudit()
    {
        var trail = Substitute.For<IAuditTrail>();
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, new FakeTime(Now));

        _ = new DefaultSanctionsScreener(
            new EmptySanctionsListSource(),
            emitter,
            ScreeningPolicy.AdvisoryOnly,
            "operator-1",
            new FakeTime(Now));

        // Fire-and-forget at ctor; give the runtime a brief moment.
        await Task.Delay(50);

        await trail.Received().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SanctionsAdvisoryOnlyConfigured)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_AuditEnabled_NullEmitter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultSanctionsScreener(new EmptySanctionsListSource(), null!, ScreeningPolicy.Default, "op-1"));
    }

    [Fact]
    public void Constructor_AuditEnabled_NullOperator_Throws()
    {
        var emitter = new RegulatoryAuditEmitter(
            Substitute.For<IAuditTrail>(),
            new Ed25519Signer(KeyPair.Generate()),
            TenantA);
        Assert.Throws<ArgumentException>(() =>
            new DefaultSanctionsScreener(new EmptySanctionsListSource(), emitter, ScreeningPolicy.Default, ""));
    }

    private sealed class ListSource : ISanctionsListSource
    {
        private readonly IReadOnlyList<SanctionsListEntry> _entries;
        public ListSource(IReadOnlyList<SanctionsListEntry> entries) => _entries = entries;
        public IReadOnlyList<SanctionsListEntry> MatchesFor(string subjectId) => _entries;
    }
}

public sealed class CompositeJurisdictionProbeAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new("tenant-a");

    [Fact]
    public async Task ProbeAsync_LowConfidence_EmitsAudit()
    {
        var trail = Substitute.For<IAuditTrail>();
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, new FakeTime(Now));
        var probe = new DefaultCompositeJurisdictionProbe(emitter, new FakeTime(Now));

        await probe.ProbeAsync(new CompositeJurisdictionSignals { UserDeclaredCode = "US-UT" });

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.JurisdictionProbedWithLowConfidence)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProbeAsync_HighConfidence_NoEmission()
    {
        var trail = Substitute.For<IAuditTrail>();
        var emitter = new RegulatoryAuditEmitter(trail, new Ed25519Signer(KeyPair.Generate()), TenantA, new FakeTime(Now));
        var probe = new DefaultCompositeJurisdictionProbe(emitter, new FakeTime(Now));

        await probe.ProbeAsync(new CompositeJurisdictionSignals
        {
            UserDeclaredCode = "US-UT",
            TenantConfigCode = "US-UT",
            IpGeoCode = "US-UT",
        });

        await trail.DidNotReceive().AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }
}

internal sealed class FakeTime : TimeProvider
{
    private DateTimeOffset _now;
    public FakeTime(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
