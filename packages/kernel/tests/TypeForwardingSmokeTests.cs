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
/// These tests do two jobs:
/// </para>
/// <list type="number">
///   <item><description>
///     Prove the shipped (forwarded) kernel primitives resolve from the
///     Foundation assembly via <c>[assembly: TypeForwardedTo]</c>. If someone
///     renames a type in Foundation without updating <c>TypeForwards.cs</c>,
///     the Kernel assembly won't compile in the first place — but these tests
///     guard the additional invariant that the CLR resolves the type to the
///     SAME Foundation assembly at runtime (not an accidental duplicate).
///   </description></item>
///   <item><description>
///     Confirm the stub interfaces for the not-yet-shipped primitives
///     (<c>ISchemaRegistry</c>, <c>IEventBus</c>) ship from the Kernel
///     assembly directly — i.e. the Kernel physically owns the stub types
///     rather than forwarding them anywhere.
///   </description></item>
/// </list>
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

    // ----- Stub primitives (spec §3.4 / G2, §3.6 / G3) -----

    [Fact]
    public void SchemaRegistryStub_LivesInKernelAssembly()
    {
        var type = typeof(Schema.ISchemaRegistry);
        Assert.NotNull(type);
        Assert.True(type.IsInterface);
        Assert.Equal("Sunfish.Kernel.Schema", type.Namespace);
        var asmName = type.Assembly.GetName().Name;
        Assert.Equal("Sunfish.Kernel", asmName);
    }

    [Fact]
    public void EventBusStub_LivesInKernelAssembly()
    {
        var type = typeof(Events.IEventBus);
        Assert.NotNull(type);
        Assert.True(type.IsInterface);
        Assert.Equal("Sunfish.Kernel", type.Assembly.GetName().Name);
        Assert.Equal("Sunfish.Kernel.Events", type.Namespace);
    }

    // ----- Helper -----

    private static void AssertLivesInFoundation(Type type)
    {
        Assert.NotNull(type);
        var asmName = type.Assembly.GetName().Name;
        Assert.Equal("Sunfish.Foundation", asmName);
    }
}
