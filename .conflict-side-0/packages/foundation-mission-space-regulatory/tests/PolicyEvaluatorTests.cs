using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Tests;

public sealed class PolicyEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static JurisdictionProbe DummyProbe(string code = "US-UT") => new()
    {
        JurisdictionCode = code,
        Confidence = Confidence.High,
        ProbedAt = Now,
    };

    [Fact]
    public async Task EmptyRuleSource_ReturnsSilentPass_PerA1_8()
    {
        var evaluator = new DefaultPolicyEvaluator(new EmptyPolicyRuleSource(), new FakeTime(Now));
        var verdict = await evaluator.EvaluateAsync("feature.x", DummyProbe());
        Assert.Equal(PolicyVerdictState.Pass, verdict.State);
        Assert.Empty(verdict.Evaluations);
        Assert.Equal(Now, verdict.EvaluatedAt);
    }

    [Theory]
    [InlineData(RegulatoryRegime.HIPAA, PolicyEvaluationKind.UserConsentRequirement, PolicyEnforcementAction.PromptUserConsent)]
    [InlineData(RegulatoryRegime.GDPR, PolicyEvaluationKind.DataExportConstraint, PolicyEnforcementAction.Block)]
    [InlineData(RegulatoryRegime.FHA, PolicyEvaluationKind.DataResidencyConstraint, PolicyEnforcementAction.Block)]
    [InlineData(RegulatoryRegime.SOC2, PolicyEvaluationKind.AutomatedDecisionGate, PolicyEnforcementAction.AuditOnly)]
    [InlineData(RegulatoryRegime.CCPA, PolicyEvaluationKind.NotificationRequirement, PolicyEnforcementAction.PromptUserConsent)]
    public async Task SyntheticRule_AppliesToAllFeatures_ProducesEvaluationEntry(
        RegulatoryRegime regime,
        PolicyEvaluationKind kind,
        PolicyEnforcementAction action)
    {
        var rule = new JurisdictionalPolicyRule
        {
            RuleId = $"synthetic-{regime}",
            Regime = regime,
            EvaluationKind = kind,
            EnforcementAction = action,
            RelevantFeatures = null, // applies to all
            RuleVersion = "1.0.0",
        };
        var evaluator = new DefaultPolicyEvaluator(new ListRuleSource(new[] { rule }), new FakeTime(Now));

        var verdict = await evaluator.EvaluateAsync("feature.x", DummyProbe());

        Assert.Equal(PolicyVerdictState.Pass, verdict.State);
        Assert.Single(verdict.Evaluations);
        Assert.Equal($"synthetic-{regime}", verdict.Evaluations[0].RuleId);
        Assert.Equal(PolicyVerdictState.Pass, verdict.Evaluations[0].State);
    }

    [Fact]
    public async Task RelevantFeatures_NotMatchingFeature_RuleSkipped()
    {
        var rule = new JurisdictionalPolicyRule
        {
            RuleId = "scoped-rule",
            Regime = RegulatoryRegime.GDPR,
            EvaluationKind = PolicyEvaluationKind.DataExportConstraint,
            EnforcementAction = PolicyEnforcementAction.Block,
            RelevantFeatures = new HashSet<string> { "feature.other" },
            RuleVersion = "1.0.0",
        };
        var evaluator = new DefaultPolicyEvaluator(new ListRuleSource(new[] { rule }), new FakeTime(Now));

        var verdict = await evaluator.EvaluateAsync("feature.x", DummyProbe());

        Assert.Empty(verdict.Evaluations);
    }

    [Fact]
    public async Task RelevantFeatures_MatchingFeature_RuleEvaluated()
    {
        var rule = new JurisdictionalPolicyRule
        {
            RuleId = "scoped-rule",
            Regime = RegulatoryRegime.GDPR,
            EvaluationKind = PolicyEvaluationKind.DataExportConstraint,
            EnforcementAction = PolicyEnforcementAction.Block,
            RelevantFeatures = new HashSet<string> { "feature.x" },
            RuleVersion = "1.0.0",
        };
        var evaluator = new DefaultPolicyEvaluator(new ListRuleSource(new[] { rule }), new FakeTime(Now));

        var verdict = await evaluator.EvaluateAsync("feature.x", DummyProbe());

        Assert.Single(verdict.Evaluations);
        Assert.Equal("scoped-rule", verdict.Evaluations[0].RuleId);
    }

    [Fact]
    public async Task InvalidateCache_ClearsPerFeatureFilterCache()
    {
        var source = new MutableRuleSource();
        var evaluator = new DefaultPolicyEvaluator(source, new FakeTime(Now));

        // First evaluation against empty rule set.
        var v1 = await evaluator.EvaluateAsync("feature.x", DummyProbe());
        Assert.Empty(v1.Evaluations);

        // Mutate the source — without InvalidateCache the per-feature cache returns the cached empty list.
        source.SetRules(new[]
        {
            new JurisdictionalPolicyRule
            {
                RuleId = "added-after-cache",
                Regime = RegulatoryRegime.GDPR,
                EvaluationKind = PolicyEvaluationKind.DataExportConstraint,
                EnforcementAction = PolicyEnforcementAction.Block,
                RuleVersion = "1.0.0",
            },
        });
        var v2 = await evaluator.EvaluateAsync("feature.x", DummyProbe());
        Assert.Empty(v2.Evaluations); // still cached

        evaluator.InvalidateCache();
        var v3 = await evaluator.EvaluateAsync("feature.x", DummyProbe());
        Assert.Single(v3.Evaluations);
    }

    [Fact]
    public async Task EvaluateAsync_NullArgs_Throws()
    {
        var evaluator = new DefaultPolicyEvaluator(new EmptyPolicyRuleSource(), new FakeTime(Now));
        await Assert.ThrowsAsync<ArgumentException>(() => evaluator.EvaluateAsync("", DummyProbe()).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => evaluator.EvaluateAsync("feature.x", null!).AsTask());
    }

    [Fact]
    public void Constructor_NullRuleSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultPolicyEvaluator(null!));
    }

    private sealed class ListRuleSource : IPolicyRuleSource
    {
        private readonly IReadOnlyList<JurisdictionalPolicyRule> _rules;
        public ListRuleSource(IReadOnlyList<JurisdictionalPolicyRule> rules) => _rules = rules;
        public IReadOnlyList<JurisdictionalPolicyRule> GetRules() => _rules;
    }

    private sealed class MutableRuleSource : IPolicyRuleSource
    {
        private IReadOnlyList<JurisdictionalPolicyRule> _rules = Array.Empty<JurisdictionalPolicyRule>();
        public void SetRules(IReadOnlyList<JurisdictionalPolicyRule> rules) => _rules = rules;
        public IReadOnlyList<JurisdictionalPolicyRule> GetRules() => _rules;
    }

    private sealed class FakeTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
