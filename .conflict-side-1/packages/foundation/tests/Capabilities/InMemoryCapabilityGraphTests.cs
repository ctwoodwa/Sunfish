using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Capabilities;

public class InMemoryCapabilityGraphTests
{
    private sealed record TestHarness(
        KeyPair Keys,
        Ed25519Signer Signer,
        Ed25519Verifier Verifier,
        InMemoryCapabilityGraph Graph) : IDisposable
    {
        public void Dispose() => Keys.Dispose();
    }

    private static TestHarness BuildHarness()
    {
        var keys = KeyPair.Generate();
        var signer = new Ed25519Signer(keys);
        var verifier = new Ed25519Verifier();
        var graph = new InMemoryCapabilityGraph(verifier);
        return new TestHarness(keys, signer, verifier, graph);
    }

    private static async Task<List<SignedOperation<CapabilityOp>>> MaterializeAsync(
        IAsyncEnumerable<SignedOperation<CapabilityOp>> source)
    {
        var list = new List<SignedOperation<CapabilityOp>>();
        await foreach (var op in source)
            list.Add(op);
        return list;
    }

    [Fact]
    public async Task Mutate_RejectsOpWithInvalidSignature()
    {
        using var h = BuildHarness();
        using var subjectA = KeyPair.Generate();
        using var subjectB = KeyPair.Generate();

        CapabilityOp originalPayload = new Sunfish.Foundation.Capabilities.Delegate(subjectA.PrincipalId, new Resource("r1"), CapabilityAction.Read);
        var validOp = await h.Signer.SignAsync(originalPayload, DateTimeOffset.UtcNow, Guid.NewGuid());

        // Tamper: swap the payload. The original signature no longer covers the new payload.
        CapabilityOp otherPayload = new Sunfish.Foundation.Capabilities.Delegate(subjectB.PrincipalId, new Resource("r2"), CapabilityAction.Write);
        var tampered = validOp with { Payload = otherPayload };

        var result = await h.Graph.MutateAsync(tampered);

        Assert.Equal(MutationKind.Rejected, result.Kind);
        Assert.Equal("Invalid signature", result.Reason);
    }

    [Fact]
    public async Task Mutate_RejectsDuplicateNonce()
    {
        using var h = BuildHarness();
        using var newKp = KeyPair.Generate();

        CapabilityOp payload = new MintPrincipal(newKp.PrincipalId, PrincipalKind.Individual);
        var op = await h.Signer.SignAsync(payload, DateTimeOffset.UtcNow, Guid.NewGuid());

        var first = await h.Graph.MutateAsync(op);
        var second = await h.Graph.MutateAsync(op);

        Assert.Equal(MutationKind.Accepted, first.Kind);
        Assert.Equal(MutationKind.Rejected, second.Kind);
        Assert.Equal("Duplicate nonce", second.Reason);
    }

    [Fact]
    public async Task Mutate_MintPrincipalIndividual_AddsToPrincipalSet()
    {
        using var h = BuildHarness();
        using var newKp = KeyPair.Generate();

        CapabilityOp payload = new MintPrincipal(newKp.PrincipalId, PrincipalKind.Individual);
        var op = await h.Signer.SignAsync(payload, DateTimeOffset.UtcNow, Guid.NewGuid());

        var result = await h.Graph.MutateAsync(op);

        Assert.Equal(MutationKind.Accepted, result.Kind);
        var ops = await MaterializeAsync(h.Graph.ListOpsAsync());
        Assert.Single(ops);
        Assert.Same(op, ops[0]);
    }

    [Fact]
    public async Task Mutate_MintPrincipalGroup_AddsToPrincipalSet()
    {
        using var h = BuildHarness();
        using var groupKp = KeyPair.Generate();
        using var memberKp = KeyPair.Generate();

        CapabilityOp payload = new MintPrincipal(
            groupKp.PrincipalId,
            PrincipalKind.Group,
            InitialMembers: new[] { memberKp.PrincipalId });
        var op = await h.Signer.SignAsync(payload, DateTimeOffset.UtcNow, Guid.NewGuid());

        var result = await h.Graph.MutateAsync(op);

        Assert.Equal(MutationKind.Accepted, result.Kind);
        var ops = await MaterializeAsync(h.Graph.ListOpsAsync());
        Assert.Single(ops);
    }

    [Fact]
    public async Task ListOpsAsync_ReturnsAppliedOpsInOrder()
    {
        using var h = BuildHarness();
        using var mintedKp = KeyPair.Generate();
        using var subjectKp = KeyPair.Generate();

        CapabilityOp mintPayload = new MintPrincipal(mintedKp.PrincipalId, PrincipalKind.Individual);
        var mintOp = await h.Signer.SignAsync(mintPayload, DateTimeOffset.UtcNow, Guid.NewGuid());

        CapabilityOp delegatePayload = new Sunfish.Foundation.Capabilities.Delegate(
            subjectKp.PrincipalId,
            new Resource("urn:doc:1"),
            CapabilityAction.Read);
        var delegateOp = await h.Signer.SignAsync(delegatePayload, DateTimeOffset.UtcNow, Guid.NewGuid());

        var mintResult = await h.Graph.MutateAsync(mintOp);
        var delegateResult = await h.Graph.MutateAsync(delegateOp);

        Assert.Equal(MutationKind.Accepted, mintResult.Kind);
        Assert.Equal(MutationKind.Accepted, delegateResult.Kind);

        var ops = await MaterializeAsync(h.Graph.ListOpsAsync());
        Assert.Equal(2, ops.Count);
        Assert.Same(mintOp, ops[0]);
        Assert.Same(delegateOp, ops[1]);
    }
}
