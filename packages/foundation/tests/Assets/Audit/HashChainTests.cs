using System.Text.Json;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tests.Assets.Audit;

public sealed class HashChainTests
{
    private static readonly EntityId Entity = new("property", "acme", "42");
    private static readonly ActorId Actor = new("alice");
    private static readonly TenantId Tenant = new("t1");
    private static readonly DateTimeOffset At = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ComputeHash_IsDeterministic_ForSameInputs()
    {
        using var payload = JsonDocument.Parse("""{"x":1}""");
        var h1 = HashChain.ComputeHash(null, Entity, Op.Mint, Actor, Tenant, At, payload);
        var h2 = HashChain.ComputeHash(null, Entity, Op.Mint, Actor, Tenant, At, payload);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ComputeHash_ChangesWhenPrevHashChanges()
    {
        using var payload = JsonDocument.Parse("""{"x":1}""");
        var h1 = HashChain.ComputeHash(null, Entity, Op.Write, Actor, Tenant, At, payload);
        var h2 = HashChain.ComputeHash("abc", Entity, Op.Write, Actor, Tenant, At, payload);
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void ComputeHash_ChangesWhenPayloadChanges()
    {
        using var p1 = JsonDocument.Parse("""{"x":1}""");
        using var p2 = JsonDocument.Parse("""{"x":2}""");
        var h1 = HashChain.ComputeHash(null, Entity, Op.Write, Actor, Tenant, At, p1);
        var h2 = HashChain.ComputeHash(null, Entity, Op.Write, Actor, Tenant, At, p2);
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForValidChain()
    {
        using var p1 = JsonDocument.Parse("""{"v":1}""");
        using var p2 = JsonDocument.Parse("""{"v":2}""");

        var h1 = HashChain.ComputeHash(null, Entity, Op.Mint, Actor, Tenant, At, p1);
        var r1 = new AuditRecord(new AuditId(1), Entity, null, Op.Mint, Actor, Tenant, At, null, p1, null, null, h1);

        var at2 = At.AddDays(1);
        var h2 = HashChain.ComputeHash(h1, Entity, Op.Write, Actor, Tenant, at2, p2);
        var r2 = new AuditRecord(new AuditId(2), Entity, null, Op.Write, Actor, Tenant, at2, null, p2, null, r1.Id, h2);

        Assert.True(HashChain.Verify(new[] { r1, r2 }));
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenPrevLinkIsBroken()
    {
        using var p1 = JsonDocument.Parse("""{"v":1}""");
        using var p2 = JsonDocument.Parse("""{"v":2}""");
        var h1 = HashChain.ComputeHash(null, Entity, Op.Mint, Actor, Tenant, At, p1);
        var r1 = new AuditRecord(new AuditId(1), Entity, null, Op.Mint, Actor, Tenant, At, null, p1, null, null, h1);
        var at2 = At.AddDays(1);
        var h2 = HashChain.ComputeHash(h1, Entity, Op.Write, Actor, Tenant, at2, p2);
        // prev-link set to a DIFFERENT id — broken chain.
        var r2 = new AuditRecord(new AuditId(2), Entity, null, Op.Write, Actor, Tenant, at2, null, p2, null, new AuditId(99), h2);
        Assert.False(HashChain.Verify(new[] { r1, r2 }));
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenRecordHashIsTampered()
    {
        using var p1 = JsonDocument.Parse("""{"v":1}""");
        var h1 = HashChain.ComputeHash(null, Entity, Op.Mint, Actor, Tenant, At, p1);
        var r1 = new AuditRecord(new AuditId(1), Entity, null, Op.Mint, Actor, Tenant, At, null, p1, null, null, h1+ "tampered");
        Assert.False(HashChain.Verify(new[] { r1 }));
    }
}
