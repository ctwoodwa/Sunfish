using System.Reflection;

using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Versions;
using Sunfish.Foundation.Blobs;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.PolicyEvaluator;

namespace Sunfish.Kernel.Tests;

/// <summary>
/// Smoke tests for the Sunfish.Kernel facade (gap G1).
/// </summary>
/// <remarks>
/// <para>
/// These tests prove the shipped (forwarded) kernel primitives resolve from the
/// Foundation assembly via <c>[assembly: TypeForwardedTo]</c>. If someone renames
/// a type in Foundation without updating <c>TypeForwards.cs</c>, the Kernel
/// assembly won't compile in the first place — but these tests guard the
/// additional invariant that the CLR resolves the type to the SAME Foundation
/// assembly at runtime (not an accidental duplicate).
/// </para>
/// <para>
/// The §3.4 Schema Registry (gap G2) and §3.6 Event Bus (gap G3) primitives are
/// no longer covered here: each has been promoted out of this façade into its
/// own sibling package (<c>Sunfish.Kernel.SchemaRegistry</c>,
/// <c>Sunfish.Kernel.EventBus</c>) and ships its own smoke tests. See
/// <c>icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md</c> (G2, G3).
/// </para>
/// </remarks>
public class TypeForwardingSmokeTests
{
    // ----- Forwarded primitives (spec §3.1, §3.2, §3.3, §3.5, §3.7) -----

    [Fact]
    public void EntityStore_TypeForwarding_ResolvesToFoundation()
    {
        AssertLivesInFoundation(typeof(IEntityStore));
        AssertLivesInFoundation(typeof(InMemoryEntityStore));
    }

    [Fact]
    public void VersionStore_TypeForwarding_ResolvesToFoundation()
    {
        AssertLivesInFoundation(typeof(IVersionStore));
        AssertLivesInFoundation(typeof(InMemoryVersionStore));
    }

    [Fact]
    public void AuditLog_TypeForwarding_ResolvesToFoundation()
    {
        AssertLivesInFoundation(typeof(IAuditLog));
        AssertLivesInFoundation(typeof(AuditRecord));
        AssertLivesInFoundation(typeof(HashChain));
    }

    [Fact]
    public void PermissionEvaluator_TypeForwarding_ResolvesToFoundation()
    {
        AssertLivesInFoundation(typeof(IPermissionEvaluator));
        AssertLivesInFoundation(typeof(Decision));
        AssertLivesInFoundation(typeof(Subject));
        AssertLivesInFoundation(typeof(PolicyResource));
        AssertLivesInFoundation(typeof(ContextEnvelope));
    }

    [Fact]
    public void BlobStore_TypeForwarding_ResolvesToFoundation()
    {
        AssertLivesInFoundation(typeof(IBlobStore));
        AssertLivesInFoundation(typeof(Cid));
    }

    [Fact]
    public void IdentityTypes_TypeForwarding_ResolveToFoundation()
    {
        AssertLivesInFoundation(typeof(EntityId));
        AssertLivesInFoundation(typeof(VersionId));
        AssertLivesInFoundation(typeof(Instant));
        AssertLivesInFoundation(typeof(PrincipalId));
        AssertLivesInFoundation(typeof(Signature));
        AssertLivesInFoundation(typeof(SignedOperation<>));
    }

    // ----- Helper -----

    private static void AssertLivesInFoundation(Type type)
    {
        Assert.NotNull(type);
        var asmName = type.Assembly.GetName().Name;
        Assert.Equal("Sunfish.Foundation", asmName);
    }
}
