using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Foundation.Versioning.Tests;

public sealed class HandshakeProtocolTests
{
    private static readonly DefaultCompatibilityRelation Relation = new();

    private static VersionVector Make(
        string kernel = "1.3.0",
        uint schemaEpoch = 7u,
        ChannelKind channel = ChannelKind.Stable) => new(
            Kernel: kernel,
            Plugins: new Dictionary<PluginId, PluginVersionVectorEntry>(),
            Adapters: new Dictionary<AdapterId, string> { [new AdapterId("blazor")] = "0.9.0" },
            SchemaEpoch: schemaEpoch,
            Channel: channel,
            InstanceClass: InstanceClassKind.SelfHost);

    [Fact]
    public async Task EvaluateAsync_DelegatesToRelation()
    {
        var exchange = new InMemoryVersionVectorExchange(Relation);
        var verdict = await exchange.EvaluateAsync(Make(), Make(), CancellationToken.None);

        Assert.Equal(VerdictKind.Compatible, verdict.Verdict);
        Assert.Null(verdict.FailedRule);
    }

    [Fact]
    public async Task EvaluateAsync_TwoPhaseCommit_BothPeersAgreeCompatible_Proceeds()
    {
        // A7.1.3c: federation proceeds iff BOTH verdicts are Compatible.
        var exchange = new InMemoryVersionVectorExchange(Relation);
        var localView = Make(kernel: "1.3.0");
        var peerView = Make(kernel: "1.4.0");

        var localVerdict = await exchange.EvaluateAsync(localView, peerView);
        var peerVerdict = await exchange.EvaluateAsync(peerView, localView);

        Assert.Equal(VerdictKind.Compatible, localVerdict.Verdict);
        Assert.Equal(VerdictKind.Compatible, peerVerdict.Verdict);
    }

    [Fact]
    public async Task EvaluateAsync_TwoPhaseCommit_OneSideIncompatible_BothTearDown()
    {
        // A7.1.3c: if EITHER side is Incompatible, both must tear down.
        // The exchange is symmetric (per Phase 2's symmetry pin), so a
        // disagreement-style pathology converges to Incompatible from
        // both sides.
        var exchange = new InMemoryVersionVectorExchange(Relation);
        var stableNode = Make(channel: ChannelKind.Stable);
        var nightlyNode = Make(channel: ChannelKind.Nightly);

        var fromStableSide = await exchange.EvaluateAsync(stableNode, nightlyNode);
        var fromNightlySide = await exchange.EvaluateAsync(nightlyNode, stableNode);

        Assert.Equal(VerdictKind.Incompatible, fromStableSide.Verdict);
        Assert.Equal(VerdictKind.Incompatible, fromNightlySide.Verdict);
        Assert.Equal(fromStableSide.FailedRule, fromNightlySide.FailedRule);
    }

    [Fact]
    public async Task EvaluateAsync_LegacyReconnectPattern_SignalsKernelSemverWindow()
    {
        // A6.5 receive-only mode: when a peer's kernel lags by > MaxKernelMinorLag,
        // the verdict is Incompatible(KernelSemverWindow). Up-to-date peers
        // inspect FailedRule + FailedRuleDetail and may opt into one-sided
        // receive-only per their policy. The exchange surface itself stays
        // simple — receive-only is a caller-side decision against this verdict
        // shape.
        var exchange = new InMemoryVersionVectorExchange(Relation);
        var current = Make(kernel: "1.7.0");
        var legacy = Make(kernel: "1.3.0");

        var verdict = await exchange.EvaluateAsync(current, legacy);

        Assert.Equal(VerdictKind.Incompatible, verdict.Verdict);
        Assert.Equal(FailedRule.KernelSemverWindow, verdict.FailedRule);
        Assert.NotNull(verdict.FailedRuleDetail);
        Assert.Contains("kernel minor lag", verdict.FailedRuleDetail);
    }

    [Fact]
    public async Task EvaluateAsync_NullArgs_Throws()
    {
        var exchange = new InMemoryVersionVectorExchange(Relation);

        await Assert.ThrowsAsync<System.ArgumentNullException>(
            () => exchange.EvaluateAsync(null!, Make()).AsTask());
        await Assert.ThrowsAsync<System.ArgumentNullException>(
            () => exchange.EvaluateAsync(Make(), null!).AsTask());
    }

    [Fact]
    public async Task EvaluateAsync_HonorsCancellation()
    {
        var exchange = new InMemoryVersionVectorExchange(Relation);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<System.OperationCanceledException>(
            () => exchange.EvaluateAsync(Make(), Make(), cts.Token).AsTask());
    }

    [Fact]
    public void Constructor_NullRelation_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new InMemoryVersionVectorExchange(null!));
    }

    [Fact]
    public async Task EvaluateAsync_PreservesFailedRuleDetail()
    {
        var exchange = new InMemoryVersionVectorExchange(Relation);
        var v1 = Make(schemaEpoch: 7);
        var v2 = Make(schemaEpoch: 9);

        var verdict = await exchange.EvaluateAsync(v1, v2);

        Assert.Equal(FailedRule.SchemaEpochMismatch, verdict.FailedRule);
        Assert.NotNull(verdict.FailedRuleDetail);
        Assert.Contains("7", verdict.FailedRuleDetail);
        Assert.Contains("9", verdict.FailedRuleDetail);
    }
}
