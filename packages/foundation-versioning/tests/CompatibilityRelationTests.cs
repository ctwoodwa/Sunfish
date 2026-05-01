using System.Collections.Generic;
using Xunit;

namespace Sunfish.Foundation.Versioning.Tests;

public sealed class CompatibilityRelationTests
{
    private static VersionVector Make(
        string kernel = "1.3.0",
        uint schemaEpoch = 7u,
        ChannelKind channel = ChannelKind.Stable,
        InstanceClassKind instanceClass = InstanceClassKind.SelfHost,
        IReadOnlyDictionary<PluginId, PluginVersionVectorEntry>? plugins = null,
        IReadOnlyDictionary<AdapterId, string>? adapters = null) => new(
            Kernel: kernel,
            Plugins: plugins ?? new Dictionary<PluginId, PluginVersionVectorEntry>(),
            Adapters: adapters ?? new Dictionary<AdapterId, string> { [new AdapterId("blazor")] = "0.9.0" },
            SchemaEpoch: schemaEpoch,
            Channel: channel,
            InstanceClass: instanceClass);

    private static readonly DefaultCompatibilityRelation Relation = new();

    // ===== Rule 1 — schema epoch =====

    [Fact]
    public void Rule1_SchemaEpochMatch_Passes()
    {
        var v = Make(schemaEpoch: 7);
        var result = Relation.Evaluate(v, v with { });
        Assert.Equal(VerdictKind.Compatible, result.Verdict);
    }

    [Fact]
    public void Rule1_SchemaEpochMismatch_Rejects()
    {
        var result = Relation.Evaluate(Make(schemaEpoch: 7), Make(schemaEpoch: 8));
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.SchemaEpochMismatch, result.FailedRule);
    }

    // ===== Rule 2 — kernel SemVer window =====

    [Fact]
    public void Rule2_KernelMinorWithinWindow_Passes()
    {
        var result = Relation.Evaluate(Make(kernel: "1.3.0"), Make(kernel: "1.5.0"));
        Assert.Equal(VerdictKind.Compatible, result.Verdict);
    }

    [Fact]
    public void Rule2_KernelMinorOutsideWindow_Rejects()
    {
        // 1.3 vs 1.6 = 3-minor lag > default 2.
        var result = Relation.Evaluate(Make(kernel: "1.3.0"), Make(kernel: "1.6.0"));
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.KernelSemverWindow, result.FailedRule);
    }

    [Fact]
    public void Rule2_KernelMajorMismatch_Rejects()
    {
        var result = Relation.Evaluate(Make(kernel: "1.3.0"), Make(kernel: "2.3.0"));
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.KernelSemverWindow, result.FailedRule);
    }

    [Fact]
    public void Rule2_KernelMalformed_Rejects()
    {
        var result = Relation.Evaluate(Make(kernel: "not-semver"), Make(kernel: "1.3.0"));
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.KernelSemverWindow, result.FailedRule);
    }

    [Fact]
    public void Rule2_TunableMaxKernelMinorLag_Honored()
    {
        var strict = new DefaultCompatibilityRelation(maxKernelMinorLag: 0);
        var result = strict.Evaluate(Make(kernel: "1.3.0"), Make(kernel: "1.4.0"));
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.KernelSemverWindow, result.FailedRule);
    }

    // ===== Rule 3 — required-plugin intersection (A7.3 augmentation) =====

    [Fact]
    public void Rule3_BothSidesCarryRequiredPlugin_Passes()
    {
        var plugins = new Dictionary<PluginId, PluginVersionVectorEntry>
        {
            [new PluginId("p1")] = new("1.0.0", Required: true),
        };
        var result = Relation.Evaluate(Make(plugins: plugins), Make(plugins: plugins));
        Assert.Equal(VerdictKind.Compatible, result.Verdict);
    }

    [Fact]
    public void Rule3_RequiredOnLocalAbsentOnPeer_Rejects()
    {
        var withRequired = new Dictionary<PluginId, PluginVersionVectorEntry>
        {
            [new PluginId("p1")] = new("1.0.0", Required: true),
        };
        var result = Relation.Evaluate(Make(plugins: withRequired), Make());
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.RequiredPluginIntersection, result.FailedRule);
    }

    [Fact]
    public void Rule3_RequiredOnPeerAbsentOnLocal_RejectsSymmetrically()
    {
        var withRequired = new Dictionary<PluginId, PluginVersionVectorEntry>
        {
            [new PluginId("p1")] = new("1.0.0", Required: true),
        };
        var result = Relation.Evaluate(Make(), Make(plugins: withRequired));
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.RequiredPluginIntersection, result.FailedRule);
    }

    [Fact]
    public void Rule3_OptionalPluginAbsentOnPeer_Passes()
    {
        var withOptional = new Dictionary<PluginId, PluginVersionVectorEntry>
        {
            [new PluginId("p1")] = new("1.0.0", Required: false),
        };
        var result = Relation.Evaluate(Make(plugins: withOptional), Make());
        Assert.Equal(VerdictKind.Compatible, result.Verdict);
    }

    // ===== Rule 4 — adapter-set intersection =====

    [Fact]
    public void Rule4_AdapterSetIntersectionNonEmpty_Passes()
    {
        var v1Adapters = new Dictionary<AdapterId, string>
        {
            [new AdapterId("blazor")] = "0.9.0",
            [new AdapterId("react")] = "1.0.0",
        };
        var v2Adapters = new Dictionary<AdapterId, string>
        {
            [new AdapterId("blazor")] = "0.9.0",
            [new AdapterId("maui-blazor")] = "0.5.0",
        };
        var result = Relation.Evaluate(Make(adapters: v1Adapters), Make(adapters: v2Adapters));
        Assert.Equal(VerdictKind.Compatible, result.Verdict);
    }

    [Fact]
    public void Rule4_AdapterSetDisjoint_Rejects()
    {
        var v1Adapters = new Dictionary<AdapterId, string> { [new AdapterId("blazor")] = "0.9.0" };
        var v2Adapters = new Dictionary<AdapterId, string> { [new AdapterId("react")] = "1.0.0" };
        var result = Relation.Evaluate(Make(adapters: v1Adapters), Make(adapters: v2Adapters));
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.AdapterSetIncompatible, result.FailedRule);
    }

    // ===== Rule 5 — channel ordering (A7.9 reword) =====

    [Fact]
    public void Rule5_SameChannelPairing_Passes()
    {
        var result = Relation.Evaluate(
            Make(channel: ChannelKind.Beta),
            Make(channel: ChannelKind.Beta));
        Assert.Equal(VerdictKind.Compatible, result.Verdict);
    }

    [Fact]
    public void Rule5_StableNightlyCrossPairing_Rejects()
    {
        var result = Relation.Evaluate(
            Make(channel: ChannelKind.Stable),
            Make(channel: ChannelKind.Nightly));
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.ChannelOrdering, result.FailedRule);
    }

    [Fact]
    public void Rule5_StableBetaCrossPairing_Rejects()
    {
        var result = Relation.Evaluate(
            Make(channel: ChannelKind.Stable),
            Make(channel: ChannelKind.Beta));
        Assert.Equal(VerdictKind.Incompatible, result.Verdict);
        Assert.Equal(FailedRule.ChannelOrdering, result.FailedRule);
    }

    // ===== Rule 6 — instance-class (always passes in v0) =====

    [Fact]
    public void Rule6_SelfHostManagedBridgeCrossPairing_PassesInV0()
    {
        // Per A6.2 rule 6 / A7.6: cross-instance pairing OK by default;
        // v0 names no specific blocking pairs.
        var result = Relation.Evaluate(
            Make(instanceClass: InstanceClassKind.SelfHost),
            Make(instanceClass: InstanceClassKind.ManagedBridge));
        Assert.Equal(VerdictKind.Compatible, result.Verdict);
    }

    // ===== Symmetry pin (A7.1 two-phase commit convergence) =====

    [Fact]
    public void Symmetry_AsymmetricEvaluation_ConvergesToSameVerdict()
    {
        // A7.1 pathology resolution: when peers disagree on channel rule
        // (Stable peer sees Nightly peer; Nightly peer sees Stable peer),
        // BOTH peers must arrive at Incompatible from BOTH sides.
        var stableNode = Make(channel: ChannelKind.Stable);
        var nightlyNode = Make(channel: ChannelKind.Nightly);

        var fromStableSide = Relation.Evaluate(stableNode, nightlyNode);
        var fromNightlySide = Relation.Evaluate(nightlyNode, stableNode);

        Assert.Equal(fromStableSide.Verdict, fromNightlySide.Verdict);
        Assert.Equal(fromStableSide.FailedRule, fromNightlySide.FailedRule);
        Assert.Equal(VerdictKind.Incompatible, fromStableSide.Verdict);
    }

    [Fact]
    public void Symmetry_RequiredPluginAsymmetry_ConvergesToSameVerdict()
    {
        var withRequired = new Dictionary<PluginId, PluginVersionVectorEntry>
        {
            [new PluginId("p1")] = new("1.0.0", Required: true),
        };
        var v1 = Make(plugins: withRequired);
        var v2 = Make();

        var fromV1Side = Relation.Evaluate(v1, v2);
        var fromV2Side = Relation.Evaluate(v2, v1);

        Assert.Equal(fromV1Side.Verdict, fromV2Side.Verdict);
        Assert.Equal(fromV1Side.FailedRule, fromV2Side.FailedRule);
    }

    [Fact]
    public void EvaluationOrder_SchemaEpochCheckedBeforeKernel()
    {
        // Both schema-epoch + kernel are bad; we want schema-epoch to win
        // because it's a hard rejection per A6.2 rule 1.
        var result = Relation.Evaluate(
            Make(kernel: "1.3.0", schemaEpoch: 7),
            Make(kernel: "9.9.9", schemaEpoch: 8));
        Assert.Equal(FailedRule.SchemaEpochMismatch, result.FailedRule);
    }

    [Fact]
    public void NullArgs_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => Relation.Evaluate(null!, Make()));
        Assert.Throws<System.ArgumentNullException>(() => Relation.Evaluate(Make(), null!));
    }
}
