using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Capabilities;

/// <summary>
/// Exercises the as-of time-travel semantics of <see cref="CapabilityClosure"/>: Delegate
/// Expires honored, and Revoke only applied at queries whose asOf is at or after the
/// revocation's IssuedAt.
/// </summary>
public class CapabilityGraphExpirationTests
{
    private sealed record Harness(Ed25519Verifier Verifier, InMemoryCapabilityGraph Graph);

    private static Harness BuildGraph()
    {
        var verifier = new Ed25519Verifier();
        return new Harness(verifier, new InMemoryCapabilityGraph(verifier));
    }

    [Fact]
    public async Task Delegate_WithFutureExpires_IsActive()
    {
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var subject = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var now = DateTimeOffset.UtcNow;

        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(subject.PrincipalId, PrincipalKind.Individual), now, Guid.NewGuid()));

        var resource = new Resource("r:future");
        var expires = now.AddHours(1);
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(subject.PrincipalId, resource, CapabilityAction.Read, Expires: expires),
            now, Guid.NewGuid()));

        Assert.True(await h.Graph.QueryAsync(subject.PrincipalId, resource, CapabilityAction.Read, now));
    }

    [Fact]
    public async Task Delegate_WithPastExpires_IsNotActive()
    {
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var subject = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var now = DateTimeOffset.UtcNow;
        var issuedAt = now.AddHours(-2);
        var expiredAt = now.AddHours(-1);

        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(subject.PrincipalId, PrincipalKind.Individual), issuedAt, Guid.NewGuid()));

        var resource = new Resource("r:past");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(subject.PrincipalId, resource, CapabilityAction.Read, Expires: expiredAt),
            issuedAt, Guid.NewGuid()));

        Assert.False(await h.Graph.QueryAsync(subject.PrincipalId, resource, CapabilityAction.Read, now));
    }

    [Fact]
    public async Task Delegate_WithExpires_TogglesAtBoundary()
    {
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var subject = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var issuedAt = DateTimeOffset.UtcNow;
        var expires = issuedAt.AddHours(1);

        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(subject.PrincipalId, PrincipalKind.Individual), issuedAt, Guid.NewGuid()));

        var resource = new Resource("r:boundary");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(subject.PrincipalId, resource, CapabilityAction.Read, Expires: expires),
            issuedAt, Guid.NewGuid()));

        // Just before expiry: active (d.Expires > asOf is true).
        Assert.True(await h.Graph.QueryAsync(
            subject.PrincipalId, resource, CapabilityAction.Read, expires.AddTicks(-1)));

        // At expiry: not active (d.Expires > asOf is false; the plan's condition is strict).
        Assert.False(await h.Graph.QueryAsync(
            subject.PrincipalId, resource, CapabilityAction.Read, expires));

        // After expiry: not active.
        Assert.False(await h.Graph.QueryAsync(
            subject.PrincipalId, resource, CapabilityAction.Read, expires.AddTicks(1)));
    }

    [Fact]
    public async Task Query_AsOf_PastTime_ReturnsHistoricalDecision()
    {
        // The delegate is granted at t0; a revoke is applied at t2. A query at t1 (before the
        // revoke) must still see the capability — this is the time-travel guarantee.
        var h = BuildGraph();
        using var root = KeyPair.Generate();
        using var subject = KeyPair.Generate();
        var rootSigner = new Ed25519Signer(root);
        var t0 = DateTimeOffset.UtcNow;
        var t1 = t0.AddMinutes(5);
        var t2 = t0.AddMinutes(10);

        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(subject.PrincipalId, PrincipalKind.Individual), t0, Guid.NewGuid()));

        var resource = new Resource("r:history");
        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(subject.PrincipalId, resource, CapabilityAction.Read),
            t0, Guid.NewGuid()));

        await h.Graph.MutateAsync(await rootSigner.SignAsync<CapabilityOp>(
            new Revoke(subject.PrincipalId, resource, CapabilityAction.Read), t2, Guid.NewGuid()));

        // Live query after revocation: capability is gone.
        Assert.False(await h.Graph.QueryAsync(subject.PrincipalId, resource, CapabilityAction.Read, t2));

        // Time-travel to before the revoke: capability was still held.
        Assert.True(await h.Graph.QueryAsync(subject.PrincipalId, resource, CapabilityAction.Read, t1));
    }
}
