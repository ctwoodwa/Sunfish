using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Kernel.Crdt.Backends;
using Xunit;

namespace Sunfish.Foundation.Wayfinder.Tests;

/// <summary>
/// Phase 2 — CRDT-backed repository tests.
/// </summary>
public sealed class CrdtStandingOrderRepositoryTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly ActorId ActorA = new("u1");
    private static readonly DateTimeOffset Issued = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    private static StandingOrder NewOrder(StandingOrderId id, TenantId tenantId, string path) => new(
        id,
        tenantId,
        ActorA,
        Issued,
        StandingOrderScope.Tenant,
        new[] { new StandingOrderTriple(path, JsonNode.Parse("\"old\""), JsonNode.Parse("\"new\"")) },
        "rationale",
        ApprovalChain: null,
        new AuditRecordId(Guid.NewGuid()),
        StandingOrderState.Issued);

    [Fact]
    public async Task AppendAsync_ThenGet_RoundTripsTheOrder()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var order = NewOrder(new StandingOrderId(Guid.NewGuid()), TenantA, "anchor.maui.theme");

        await repo.AppendAsync(order, CancellationToken.None);
        var fetched = await repo.GetAsync(TenantA, order.Id, CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal(order.Id, fetched!.Id);
        Assert.Equal(order.TenantId, fetched.TenantId);
        Assert.Equal(order.IssuedBy, fetched.IssuedBy);
        Assert.Single(fetched.Triples);
        Assert.Equal("anchor.maui.theme", fetched.Triples[0].Path);
    }

    [Fact]
    public async Task AppendAsync_SameIdTwice_IsIdempotent()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var order = NewOrder(new StandingOrderId(Guid.NewGuid()), TenantA, "anchor.maui.theme");

        await repo.AppendAsync(order, CancellationToken.None);
        // Second append with same Id but different rationale must be a no-op.
        var mutated = order with { Rationale = "different rationale" };
        await repo.AppendAsync(mutated, CancellationToken.None);

        var fetched = await repo.GetAsync(TenantA, order.Id, CancellationToken.None);
        Assert.NotNull(fetched);
        Assert.Equal("rationale", fetched!.Rationale);
    }

    [Fact]
    public async Task EnumerateAsync_ReturnsEveryOrderInTheTenant()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var ids = new List<StandingOrderId>();
        for (int i = 0; i < 5; i++)
        {
            var id = new StandingOrderId(Guid.NewGuid());
            ids.Add(id);
            await repo.AppendAsync(NewOrder(id, TenantA, $"anchor.path.{i}"), CancellationToken.None);
        }

        var collected = new List<StandingOrder>();
        await foreach (var order in repo.EnumerateAsync(TenantA, CancellationToken.None))
        {
            collected.Add(order);
        }

        Assert.Equal(5, collected.Count);
        Assert.True(ids.All(id => collected.Any(o => o.Id == id)));
    }

    [Fact]
    public async Task Repository_PerTenantIsolation_OrdersDoNotLeak()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var a = new StandingOrderId(Guid.NewGuid());
        var b = new StandingOrderId(Guid.NewGuid());

        await repo.AppendAsync(NewOrder(a, TenantA, "anchor.a"), CancellationToken.None);
        await repo.AppendAsync(NewOrder(b, TenantB, "anchor.b"), CancellationToken.None);

        var fromA = await repo.GetAsync(TenantA, b, CancellationToken.None);
        var fromB = await repo.GetAsync(TenantB, a, CancellationToken.None);
        Assert.Null(fromA); // b lives in TenantB, not TenantA
        Assert.Null(fromB); // a lives in TenantA, not TenantB
    }

    [Fact]
    public async Task KnownTenants_ReportsEveryMaterializedTenant()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        await repo.AppendAsync(NewOrder(new StandingOrderId(Guid.NewGuid()), TenantA, "x"), CancellationToken.None);
        await repo.AppendAsync(NewOrder(new StandingOrderId(Guid.NewGuid()), TenantB, "y"), CancellationToken.None);

        Assert.Contains(TenantA, repo.KnownTenants);
        Assert.Contains(TenantB, repo.KnownTenants);
        Assert.Equal(2, repo.KnownTenants.Count);
    }
}
