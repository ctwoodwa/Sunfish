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
/// Phase 3a — Atlas projector tests: projection correctness + search.
/// </summary>
public sealed class DefaultAtlasProjectorTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly ActorId ActorA = new("u1");
    private static readonly ActorId ActorB = new("u2");

    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static StandingOrder NewOrder(
        StandingOrderId id, ActorId issuedBy, DateTimeOffset issuedAt,
        StandingOrderScope scope, StandingOrderState state,
        params (string path, JsonNode? value)[] triples)
    {
        var triplePairs = triples.Select(t => new StandingOrderTriple(t.path, null, t.value)).ToArray();
        return new StandingOrder(
            id, TenantA, issuedBy, issuedAt, scope, triplePairs,
            "test rationale", null,
            new AuditRecordId(Guid.NewGuid()),
            state);
    }

    // ===== Projection-correctness tests =====

    [Fact]
    public async Task ProjectAsync_SingleSetting_ReturnsSnapshot()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var projector = new DefaultAtlasProjector(repo, new TestTimeProvider());

        var order = NewOrder(
            new StandingOrderId(Guid.NewGuid()), ActorA,
            new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
            StandingOrderScope.Tenant, StandingOrderState.Validated,
            ("anchor.maui.theme", JsonNode.Parse("\"dark\"")));
        await repo.AppendAsync(order, CancellationToken.None);

        var view = await projector.ProjectAsync(TenantA, scopeFilter: null, CancellationToken.None);

        Assert.Single(view.SettingsByPath);
        var snapshot = view.SettingsByPath["anchor.maui.theme"];
        Assert.Equal("anchor.maui.theme", snapshot.Path);
        Assert.Equal("dark", (string?)snapshot.CurrentValue);
        Assert.Equal(order.Id, snapshot.LastIssuedBy);
        Assert.Equal(order.IssuedAt, snapshot.LastIssuedAt);
    }

    [Fact]
    public async Task ProjectAsync_LwwResolution_NewerOrderWins()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var projector = new DefaultAtlasProjector(repo, new TestTimeProvider());

        var earlier = NewOrder(
            new StandingOrderId(Guid.NewGuid()), ActorA,
            new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
            StandingOrderScope.Tenant, StandingOrderState.Validated,
            ("anchor.maui.theme", JsonNode.Parse("\"light\"")));
        var later = NewOrder(
            new StandingOrderId(Guid.NewGuid()), ActorA,
            new DateTimeOffset(2026, 5, 4, 11, 0, 0, TimeSpan.Zero),
            StandingOrderScope.Tenant, StandingOrderState.Validated,
            ("anchor.maui.theme", JsonNode.Parse("\"dark\"")));
        await repo.AppendAsync(earlier, CancellationToken.None);
        await repo.AppendAsync(later, CancellationToken.None);

        var view = await projector.ProjectAsync(TenantA, scopeFilter: null, CancellationToken.None);

        Assert.Equal("dark", (string?)view.SettingsByPath["anchor.maui.theme"].CurrentValue);
        Assert.Equal(later.Id, view.SettingsByPath["anchor.maui.theme"].LastIssuedBy);
    }

    [Fact]
    public async Task ProjectAsync_SkipsRescindedAndRejectedOrders()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var projector = new DefaultAtlasProjector(repo, new TestTimeProvider());

        var rescinded = NewOrder(
            new StandingOrderId(Guid.NewGuid()), ActorA,
            new DateTimeOffset(2026, 5, 4, 11, 0, 0, TimeSpan.Zero),
            StandingOrderScope.Tenant, StandingOrderState.Rescinded,
            ("anchor.maui.theme", JsonNode.Parse("\"experimental\"")));
        var rejected = NewOrder(
            new StandingOrderId(Guid.NewGuid()), ActorA,
            new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero),
            StandingOrderScope.Tenant, StandingOrderState.Rejected,
            ("anchor.maui.policy", JsonNode.Parse("\"loose\"")));
        var accepted = NewOrder(
            new StandingOrderId(Guid.NewGuid()), ActorA,
            new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
            StandingOrderScope.Tenant, StandingOrderState.Validated,
            ("anchor.maui.theme", JsonNode.Parse("\"light\"")));
        await repo.AppendAsync(rescinded, CancellationToken.None);
        await repo.AppendAsync(rejected, CancellationToken.None);
        await repo.AppendAsync(accepted, CancellationToken.None);

        var view = await projector.ProjectAsync(TenantA, scopeFilter: null, CancellationToken.None);

        Assert.Single(view.SettingsByPath);
        Assert.Equal("light", (string?)view.SettingsByPath["anchor.maui.theme"].CurrentValue);
    }

    [Fact]
    public async Task ProjectAsync_ScopeFilter_RestrictsToMatchingOrders()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var projector = new DefaultAtlasProjector(repo, new TestTimeProvider());

        var tenantOrder = NewOrder(
            new StandingOrderId(Guid.NewGuid()), ActorA,
            new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
            StandingOrderScope.Tenant, StandingOrderState.Validated,
            ("a", JsonNode.Parse("\"x\"")));
        var userOrder = NewOrder(
            new StandingOrderId(Guid.NewGuid()), ActorA,
            new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
            StandingOrderScope.User, StandingOrderState.Validated,
            ("b", JsonNode.Parse("\"y\"")));
        await repo.AppendAsync(tenantOrder, CancellationToken.None);
        await repo.AppendAsync(userOrder, CancellationToken.None);

        var tenantView = await projector.ProjectAsync(TenantA, StandingOrderScope.Tenant, CancellationToken.None);
        var userView = await projector.ProjectAsync(TenantA, StandingOrderScope.User, CancellationToken.None);

        Assert.True(tenantView.SettingsByPath.ContainsKey("a"));
        Assert.False(tenantView.SettingsByPath.ContainsKey("b"));
        Assert.True(userView.SettingsByPath.ContainsKey("b"));
        Assert.False(userView.SettingsByPath.ContainsKey("a"));
    }

    // ===== Search tests =====

    [Fact]
    public async Task SearchAsync_ExactPathMatch_ScoresHighest()
    {
        var (projector, _) = await BuildSearchableProjectorAsync();

        var hits = new List<AtlasSearchHit>();
        await foreach (var h in projector.SearchAsync(TenantA, "anchor.maui.theme", limit: 5, CancellationToken.None))
        {
            hits.Add(h);
        }

        Assert.NotEmpty(hits);
        Assert.Equal("anchor.maui.theme", hits[0].Path);
        Assert.Equal(1.0, hits[0].Score);
    }

    [Fact]
    public async Task SearchAsync_PrefixMatch_ScoresAboveSubstring()
    {
        var (projector, _) = await BuildSearchableProjectorAsync();

        var hits = new List<AtlasSearchHit>();
        await foreach (var h in projector.SearchAsync(TenantA, "anchor", limit: 10, CancellationToken.None))
        {
            hits.Add(h);
        }

        Assert.NotEmpty(hits);
        // Every "anchor.*" path should match as a prefix (score 0.85).
        Assert.All(hits, h => Assert.True(h.Score >= 0.55, $"{h.Path} scored {h.Score} (expected >= 0.55)"));
        // Hits are emitted in descending score order.
        Assert.Equal(hits.OrderByDescending(h => h.Score).Select(h => h.Path), hits.Select(h => h.Path));
    }

    [Fact]
    public async Task SearchAsync_LimitTruncatesResults()
    {
        var (projector, _) = await BuildSearchableProjectorAsync();

        var hits = new List<AtlasSearchHit>();
        await foreach (var h in projector.SearchAsync(TenantA, "anchor", limit: 1, CancellationToken.None))
        {
            hits.Add(h);
        }

        Assert.Single(hits);
    }

    [Fact]
    public async Task SearchAsync_NoMatch_ReturnsEmpty()
    {
        var (projector, _) = await BuildSearchableProjectorAsync();

        var hits = new List<AtlasSearchHit>();
        await foreach (var h in projector.SearchAsync(TenantA, "nonexistent-substring-xyz", limit: 10, CancellationToken.None))
        {
            hits.Add(h);
        }

        Assert.Empty(hits);
    }

    // ===== Helpers =====

    private async Task<(DefaultAtlasProjector projector, CrdtStandingOrderRepository repo)> BuildSearchableProjectorAsync()
    {
        var repo = new CrdtStandingOrderRepository(new StubCrdtEngine());
        var projector = new DefaultAtlasProjector(repo, new TestTimeProvider());

        var paths = new[] { "anchor.maui.theme", "anchor.maui.scaling", "bridge.host.port", "bridge.host.cors" };
        foreach (var path in paths)
        {
            var order = NewOrder(
                new StandingOrderId(Guid.NewGuid()), ActorA,
                new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
                StandingOrderScope.Tenant, StandingOrderState.Validated,
                (path, JsonNode.Parse("\"x\"")));
            await repo.AppendAsync(order, CancellationToken.None);
        }
        return (projector, repo);
    }
}
