using Sunfish.Kernel.Buckets;
using Sunfish.Kernel.Buckets.Storage;

namespace Sunfish.Kernel.Buckets.Tests;

public sealed class StorageBudgetTests
{
    private static BucketDefinition Def(string name, ReplicationMode mode) =>
        new(
            Name: name,
            RecordTypes: new[] { "projects" },
            Filter: null,
            Replication: mode,
            RequiredAttestation: "team_member",
            MaxLocalAgeDays: null);

    private static (IBucketRegistry registry, IStorageBudgetManager mgr, StorageBudget budget) BuildWithBudget(long maxBytes)
    {
        var registry = new BucketRegistry();
        registry.Register(Def("archived_projects", ReplicationMode.Lazy));
        registry.Register(Def("team_core", ReplicationMode.Eager));
        var budget = new StorageBudget { MaxBytes = maxBytes };
        var mgr = new InMemoryStorageBudgetManager(registry, budget);
        return (registry, mgr, budget);
    }

    [Fact]
    public void NearLimit_fires_above_ninety_percent()
    {
        var (_, mgr, budget) = BuildWithBudget(1_000);

        mgr.Track(new TrackedRecord("archived_projects", "r1", 901, DateTimeOffset.UtcNow));

        Assert.True(budget.NearLimit);
        Assert.False(budget.OverLimit);
    }

    [Fact]
    public void OverLimit_fires_when_current_reaches_max()
    {
        var (_, mgr, budget) = BuildWithBudget(1_000);

        mgr.Track(new TrackedRecord("archived_projects", "r1", 1_000, DateTimeOffset.UtcNow));

        Assert.True(budget.OverLimit);
        Assert.True(budget.NearLimit);
    }

    [Fact]
    public async Task EvictLru_reclaims_at_least_the_requested_bytes_when_available()
    {
        var (_, mgr, budget) = BuildWithBudget(10_000);
        var now = DateTimeOffset.UtcNow;

        mgr.Track(new TrackedRecord("archived_projects", "oldest", 500, now.AddMinutes(-30)));
        mgr.Track(new TrackedRecord("archived_projects", "middle", 500, now.AddMinutes(-10)));
        mgr.Track(new TrackedRecord("archived_projects", "newest", 500, now));
        Assert.Equal(1_500, budget.CurrentBytes);

        var reclaimed = await mgr.EvictLruAsync(700, CancellationToken.None);

        Assert.True(reclaimed >= 700, $"expected >=700, got {reclaimed}");
        Assert.Equal(1_500 - reclaimed, budget.CurrentBytes);
    }

    [Fact]
    public async Task EvictLru_returns_zero_when_nothing_lazy_to_evict()
    {
        var (_, mgr, budget) = BuildWithBudget(10_000);
        var now = DateTimeOffset.UtcNow;

        // Track only eager records — eviction must not touch them.
        mgr.Track(new TrackedRecord("team_core", "eager-1", 1_000, now));
        mgr.Track(new TrackedRecord("team_core", "eager-2", 1_000, now));

        var reclaimed = await mgr.EvictLruAsync(500, CancellationToken.None);

        Assert.Equal(0, reclaimed);
        Assert.Equal(2_000, budget.CurrentBytes);
    }
}
