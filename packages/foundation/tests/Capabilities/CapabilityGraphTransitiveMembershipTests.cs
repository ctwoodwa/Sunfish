using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Capabilities;

/// <summary>
/// Exercises <see cref="CapabilityClosure.HasCapability"/> through the graph's public
/// <see cref="ICapabilityGraph.QueryAsync"/>. Covers direct delegates, delegation to groups,
/// nested groups, and RemoveMember revocation of transitive access.
/// </summary>
public class CapabilityGraphTransitiveMembershipTests
{
    private sealed record Harness(Ed25519Verifier Verifier, InMemoryCapabilityGraph Graph);

    private static Harness BuildGraph()
    {
        var verifier = new Ed25519Verifier();
        return new Harness(verifier, new InMemoryCapabilityGraph(verifier));
    }

    [Fact]
    public async Task Individual_HasCapability_OnDirectDelegate()
    {
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var subjectB = KeyPair.Generate();
        using var other = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var now = DateTimeOffset.UtcNow;

        // Root mints itself + subject as individuals so the graph knows them.
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(subjectB.PrincipalId, PrincipalKind.Individual), now, Guid.NewGuid()));

        // Root delegates Read on R to subjectB (bootstraps root as the resource's root owner).
        var resource = new Resource("r1");
        CapabilityOp grantPayload = new Sunfish.Foundation.Capabilities.Delegate(
            subjectB.PrincipalId, resource, CapabilityAction.Read);
        var grant = await rootSigner.SignAsync(grantPayload, now, Guid.NewGuid());
        var grantResult = await h.Graph.MutateAsync(grant);
        Assert.Equal(MutationKind.Accepted, grantResult.Kind);

        Assert.True(await h.Graph.QueryAsync(subjectB.PrincipalId, resource, CapabilityAction.Read, now));
        Assert.False(await h.Graph.QueryAsync(other.PrincipalId, resource, CapabilityAction.Read, now));
    }

    [Fact]
    public async Task Individual_HasCapability_ViaGroupMembership()
    {
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var group = KeyPair.Generate();
        using var member = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var now = DateTimeOffset.UtcNow;

        // Root mints the group principal with an initial member.
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(group.PrincipalId, PrincipalKind.Group, new[] { member.PrincipalId }),
            now, Guid.NewGuid()));

        // Root delegates Read on R to the group.
        var resource = new Resource("r:doc");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(group.PrincipalId, resource, CapabilityAction.Read),
            now, Guid.NewGuid()));

        Assert.True(await h.Graph.QueryAsync(member.PrincipalId, resource, CapabilityAction.Read, now));
    }

    [Fact]
    public async Task Individual_HasCapability_ViaNestedGroupMembership()
    {
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var firm = KeyPair.Generate();
        using var team = KeyPair.Generate();
        using var user = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var now = DateTimeOffset.UtcNow;

        // firm { team }, team { user }
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(firm.PrincipalId, PrincipalKind.Group, new[] { team.PrincipalId }),
            now, Guid.NewGuid()));
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(team.PrincipalId, PrincipalKind.Group, new[] { user.PrincipalId }),
            now, Guid.NewGuid()));

        // Root delegates Read on R to firm; user inherits via team.
        var resource = new Resource("r:nested");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(firm.PrincipalId, resource, CapabilityAction.Read),
            now, Guid.NewGuid()));

        Assert.True(await h.Graph.QueryAsync(user.PrincipalId, resource, CapabilityAction.Read, now));
    }

    [Fact]
    public async Task Individual_LosesCapability_AfterRemoveMember()
    {
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var team = KeyPair.Generate();
        using var user = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var t0 = DateTimeOffset.UtcNow;

        // Root mints team with user as member, then delegates Read on R to team.
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(team.PrincipalId, PrincipalKind.Group, new[] { user.PrincipalId }),
            t0, Guid.NewGuid()));

        var resource = new Resource("r:toggle");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(team.PrincipalId, resource, CapabilityAction.Read),
            t0, Guid.NewGuid()));

        Assert.True(await h.Graph.QueryAsync(user.PrincipalId, resource, CapabilityAction.Read, t0));

        // Bootstrap rule: root is the first principal to issue a membership op for this group
        // (the MintPrincipal InitialMembers shortcut did not produce an AddMember op), so root
        // can still issue RemoveMember directly.
        var t1 = t0.AddMinutes(1);
        var remove = await rootSigner.SignAsync<CapabilityOp>(
            new RemoveMember(team.PrincipalId, user.PrincipalId), t1, Guid.NewGuid());
        var removeResult = await h.Graph.MutateAsync(remove);
        Assert.Equal(MutationKind.Accepted, removeResult.Kind);

        Assert.False(await h.Graph.QueryAsync(user.PrincipalId, resource, CapabilityAction.Read, t1));
    }

    [Fact]
    public async Task ResourceRootOwner_CanIssueMultipleDelegates()
    {
        // The resource root owner (first Delegate issuer) must retain delegation authority
        // across subsequent Delegate ops on the same resource.
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var alice = KeyPair.Generate();
        using var bob = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var now = DateTimeOffset.UtcNow;

        var resource = new Resource("r:multi");

        // First delegate (bootstrap) establishes root as the owner.
        var first = await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(alice.PrincipalId, resource, CapabilityAction.Read),
            now, Guid.NewGuid()));
        Assert.Equal(MutationKind.Accepted, first.Kind);

        // Second delegate by the same root must be accepted via root-owner continuation.
        var second = await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(bob.PrincipalId, resource, CapabilityAction.Write),
            now, Guid.NewGuid()));
        Assert.Equal(MutationKind.Accepted, second.Kind);
    }

    [Fact]
    public async Task NonOwner_CannotDelegateAfterBootstrap()
    {
        // After the bootstrap delegate, a stranger (non-owner, non-delegate-holder) must be
        // rejected when attempting to delegate the same resource.
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var stranger = KeyPair.Generate();
        using var victim = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var strangerSigner = new Ed25519Signer(stranger);
        var now = DateTimeOffset.UtcNow;

        var resource = new Resource("r:auth");

        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(victim.PrincipalId, resource, CapabilityAction.Read),
            now, Guid.NewGuid()));

        var attempt = await h.Graph.MutateAsync(await strangerSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(stranger.PrincipalId, resource, CapabilityAction.Write),
            now, Guid.NewGuid()));
        Assert.Equal(MutationKind.Rejected, attempt.Kind);
    }

    [Fact]
    public async Task Individual_HasCapability_OnDelegateToIndividualDirectly()
    {
        // When the delegate target is an Individual (not a group) and the subject equals the
        // target, HasCapability must return true without needing group expansion.
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var alice = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var now = DateTimeOffset.UtcNow;

        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(alice.PrincipalId, PrincipalKind.Individual), now, Guid.NewGuid()));

        var resource = new Resource("r:direct");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(alice.PrincipalId, resource, CapabilityAction.Write),
            now, Guid.NewGuid()));

        Assert.True(await h.Graph.QueryAsync(alice.PrincipalId, resource, CapabilityAction.Write, now));
    }
}
