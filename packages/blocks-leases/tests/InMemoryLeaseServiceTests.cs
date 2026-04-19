using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Leases.Tests;

public class InMemoryLeaseServiceTests
{
    private static CreateLeaseRequest MakeRequest(string unitLocalPart = "unit-1") =>
        new()
        {
            UnitId = new EntityId("unit", "test", unitLocalPart),
            Tenants = [new PartyId("tenant-a")],
            Landlord = new PartyId("landlord-x"),
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            MonthlyRent = 1500m
        };

    [Fact]
    public async Task CreateAsync_AssignsNonEmptyId()
    {
        var svc = new InMemoryLeaseService();
        var lease = await svc.CreateAsync(MakeRequest());

        Assert.False(string.IsNullOrWhiteSpace(lease.Id.Value));
    }

    [Fact]
    public async Task CreateAsync_SetsPhaseToDraft()
    {
        var svc = new InMemoryLeaseService();
        var lease = await svc.CreateAsync(MakeRequest());

        Assert.Equal(LeasePhase.Draft, lease.Phase);
    }

    [Fact]
    public async Task GetAsync_RoundTrips_CreatedLease()
    {
        var svc = new InMemoryLeaseService();
        var created = await svc.CreateAsync(MakeRequest());

        var retrieved = await svc.GetAsync(created.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(created.MonthlyRent, retrieved.MonthlyRent);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenIdUnknown()
    {
        var svc = new InMemoryLeaseService();
        var result = await svc.GetAsync(new LeaseId("no-such-id"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ListAsync_ReturnsAll_WhenQueryIsEmpty()
    {
        var svc = new InMemoryLeaseService();
        await svc.CreateAsync(MakeRequest("unit-1"));
        await svc.CreateAsync(MakeRequest("unit-2"));
        await svc.CreateAsync(MakeRequest("unit-3"));

        var leases = await CollectAsync(svc.ListAsync(ListLeasesQuery.Empty));

        Assert.Equal(3, leases.Count);
    }

    [Fact]
    public async Task ListAsync_FiltersBy_Phase()
    {
        var svc = new InMemoryLeaseService();
        await svc.CreateAsync(MakeRequest("unit-1")); // Draft
        await svc.CreateAsync(MakeRequest("unit-2")); // Draft

        var drafts = await CollectAsync(svc.ListAsync(new ListLeasesQuery { Phase = LeasePhase.Draft }));
        var active = await CollectAsync(svc.ListAsync(new ListLeasesQuery { Phase = LeasePhase.Active }));

        Assert.Equal(2, drafts.Count);
        Assert.Empty(active);
    }

    [Fact]
    public async Task CreateAsync_ThrowsOnNull_Request()
    {
        var svc = new InMemoryLeaseService();
        await Assert.ThrowsAsync<ArgumentNullException>(() => svc.CreateAsync(null!).AsTask());
    }

    [Fact]
    public async Task ListAsync_ThrowsOnNull_Query()
    {
        var svc = new InMemoryLeaseService();
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in svc.ListAsync(null!)) { }
        });
    }

    [Fact]
    public async Task ConcurrentCreates_AreAllPersisted()
    {
        var svc = new InMemoryLeaseService();
        var tasks = Enumerable.Range(0, 20)
            .Select(i => svc.CreateAsync(MakeRequest($"unit-{i}")).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        var all = await CollectAsync(svc.ListAsync(ListLeasesQuery.Empty));
        Assert.Equal(20, all.Count);
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }
}
