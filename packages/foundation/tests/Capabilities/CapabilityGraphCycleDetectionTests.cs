using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Capabilities;

/// <summary>
/// Verifies that pathological group structures (self-membership, mutually-recursive groups)
/// do not cause the closure traversal to stack-overflow or otherwise loop. Per the Phase B
/// plan's lazy-closure philosophy we do NOT reject cycles at mutate time — we only require
/// queries to terminate safely.
/// </summary>
public class CapabilityGraphCycleDetectionTests
{
    private sealed record Harness(Ed25519Verifier Verifier, InMemoryCapabilityGraph Graph);

    private static Harness BuildGraph()
    {
        var verifier = new Ed25519Verifier();
        return new Harness(verifier, new InMemoryCapabilityGraph(verifier));
    }

    [Fact]
    public async Task Group_MemberContainsSelf_QueryDoesNotStackOverflow()
    {
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var group = KeyPair.Generate();
        using var stranger = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var now = DateTimeOffset.UtcNow;

        // Mint the group containing itself as an initial member (self-cycle).
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(group.PrincipalId, PrincipalKind.Group, new[] { group.PrincipalId }),
            now, Guid.NewGuid()));

        var resource = new Resource("r:selfcycle");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(group.PrincipalId, resource, CapabilityAction.Read),
            now, Guid.NewGuid()));

        // Query for the group itself: it terminates and returns true (target == candidate).
        Assert.True(await h.Graph.QueryAsync(group.PrincipalId, resource, CapabilityAction.Read, now));

        // Query for a non-member: terminates (doesn't loop) and returns false.
        Assert.False(await h.Graph.QueryAsync(stranger.PrincipalId, resource, CapabilityAction.Read, now));
    }

    [Fact]
    public async Task TwoGroups_MutuallyRecursive_QueryTerminates()
    {
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var groupA = KeyPair.Generate();
        using var groupB = KeyPair.Generate();
        using var stranger = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var now = DateTimeOffset.UtcNow;

        // A contains B; B contains A — mutual recursion.
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(groupA.PrincipalId, PrincipalKind.Group, new[] { groupB.PrincipalId }),
            now, Guid.NewGuid()));
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(groupB.PrincipalId, PrincipalKind.Group, new[] { groupA.PrincipalId }),
            now, Guid.NewGuid()));

        var resource = new Resource("r:mutual");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(groupA.PrincipalId, resource, CapabilityAction.Read),
            now, Guid.NewGuid()));

        // A member's query terminates: B is in A's closure, so B gets true.
        Assert.True(await h.Graph.QueryAsync(groupB.PrincipalId, resource, CapabilityAction.Read, now));
        // A non-member terminates with false.
        Assert.False(await h.Graph.QueryAsync(stranger.PrincipalId, resource, CapabilityAction.Read, now));
    }

    [Fact]
    public async Task Mutate_AddMember_GroupContainsSelf_Accepted_QuerySafe()
    {
        // Per Phase B plan: we do NOT add explicit cycle detection at mutate time. The
        // mutation is Accepted and subsequent queries still terminate via the visited-set
        // guard in IsTransitiveMember. This documents the chosen lazy-closure behavior.
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var group = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var t0 = DateTimeOffset.UtcNow;

        // Mint the group empty; then add the group to itself in a follow-up AddMember.
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(group.PrincipalId, PrincipalKind.Group), t0, Guid.NewGuid()));

        // Bootstrap rule allows any issuer to issue the first membership op for the group.
        var t1 = t0.AddSeconds(1);
        var addResult = await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new AddMember(group.PrincipalId, group.PrincipalId), t1, Guid.NewGuid()));
        Assert.Equal(MutationKind.Accepted, addResult.Kind);

        var resource = new Resource("r:accepted-cycle");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(group.PrincipalId, resource, CapabilityAction.Read),
            t1, Guid.NewGuid()));

        // Query for the group terminates without error.
        Assert.True(await h.Graph.QueryAsync(group.PrincipalId, resource, CapabilityAction.Read, t1));
    }
}
