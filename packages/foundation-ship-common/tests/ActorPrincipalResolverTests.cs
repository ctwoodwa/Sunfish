using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Ship.Common;
using Xunit;

namespace Sunfish.Foundation.Ship.Common.Tests;

/// <summary>
/// Coverage for <see cref="InMemoryActorPrincipalResolver"/> per
/// <c>actor-principal-resolver-stage06-handoff.md</c> §Tests.
/// Pins the canonical invariant
/// (<c>ActorId.Value = PrincipalId.ToBase64Url()</c>) + the
/// fail-closed null-on-invalid-base64url contract.
/// </summary>
public class ActorPrincipalResolverTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");

    private static PrincipalId NewPrincipalId() => KeyPair.Generate().PrincipalId;

    [Fact]
    public async Task Resolve_CanonicalActorId_ReturnsDerivedIndividual()
    {
        var pid = NewPrincipalId();
        var canonical = new ActorId(pid.ToBase64Url());
        var resolver = new InMemoryActorPrincipalResolver();

        var principal = await resolver.ResolveAsync(TenantA, canonical);

        Assert.NotNull(principal);
        var individual = Assert.IsType<Individual>(principal);
        Assert.Equal(pid, individual.Id);
    }

    [Fact]
    public async Task Resolve_RegisteredOverride_ReturnsOverridePrincipal()
    {
        var pid = NewPrincipalId();
        var override_ = new Individual(pid);
        var actor = new ActorId("alice");

        var resolver = new InMemoryActorPrincipalResolver();
        resolver.Register(actor, override_);

        var resolved = await resolver.ResolveAsync(TenantA, actor);
        Assert.Same(override_, resolved);
    }

    [Fact]
    public async Task Resolve_InvalidBase64UrlActorId_ReturnsNull()
    {
        var resolver = new InMemoryActorPrincipalResolver();
        // "not-a-key" is not a valid 32-byte base64url-encoded key.
        var resolved = await resolver.ResolveAsync(TenantA, new ActorId("not-a-key"));
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_OverrideByActorId_IsNotAffectedByTenantId()
    {
        var pid = NewPrincipalId();
        var override_ = new Individual(pid);
        var actor = new ActorId("alice");

        var resolver = new InMemoryActorPrincipalResolver();
        resolver.Register(actor, override_);

        var inA = await resolver.ResolveAsync(TenantA, actor);
        var inB = await resolver.ResolveAsync(TenantB, actor);
        Assert.Same(override_, inA);
        Assert.Same(override_, inB);
    }

    [Fact]
    public async Task Resolve_CanonicalRoundTrip_PrincipalIdMatchesOriginal()
    {
        // §A0 round-trip pin: PrincipalId → ActorId → resolve →
        // Individual.Id MUST byte-equal the original PrincipalId.
        var pid = NewPrincipalId();
        var actor = new ActorId(pid.ToBase64Url());

        var resolver = new InMemoryActorPrincipalResolver();
        var principal = await resolver.ResolveAsync(TenantA, actor);

        var individual = Assert.IsType<Individual>(principal);
        Assert.Equal(pid, individual.Id);
        Assert.True(pid.AsSpan().SequenceEqual(individual.Id.AsSpan()));
    }

    [Fact]
    public async Task Resolve_EmptyActorId_ReturnsNull()
    {
        var resolver = new InMemoryActorPrincipalResolver();
        var resolved = await resolver.ResolveAsync(TenantA, new ActorId(string.Empty));
        Assert.Null(resolved);
    }

    [Fact]
    public void Register_NullPrincipal_Throws()
    {
        var resolver = new InMemoryActorPrincipalResolver();
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Register(new ActorId("alice"), null!));
    }

    [Fact]
    public async Task Register_OverwritesPriorMapping()
    {
        var actor = new ActorId("alice");
        var first = new Individual(NewPrincipalId());
        var second = new Individual(NewPrincipalId());

        var resolver = new InMemoryActorPrincipalResolver();
        resolver.Register(actor, first);
        resolver.Register(actor, second);

        var resolved = await resolver.ResolveAsync(TenantA, actor);
        Assert.Same(second, resolved);
    }
}
