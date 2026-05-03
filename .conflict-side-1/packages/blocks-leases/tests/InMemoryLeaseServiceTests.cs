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

    // ─────────── W#27 Phase 1: state-machine guards ───────────

    private static readonly ActorId Operator = new("operator");

    private async Task<(InMemoryLeaseService svc, Lease lease)> NewDraftAsync()
    {
        var svc = new InMemoryLeaseService();
        var lease = await svc.CreateAsync(MakeRequest());
        return (svc, lease);
    }

    private async Task<(InMemoryLeaseService svc, Lease lease)> AdvanceToAsync(LeasePhase phase)
    {
        var (svc, lease) = await NewDraftAsync();
        var path = phase switch
        {
            LeasePhase.Draft => Array.Empty<LeasePhase>(),
            LeasePhase.AwaitingSignature => new[] { LeasePhase.AwaitingSignature },
            LeasePhase.Executed => new[] { LeasePhase.AwaitingSignature, LeasePhase.Executed },
            LeasePhase.Active => new[] { LeasePhase.AwaitingSignature, LeasePhase.Executed, LeasePhase.Active },
            LeasePhase.Renewed => new[] { LeasePhase.AwaitingSignature, LeasePhase.Executed, LeasePhase.Active, LeasePhase.Renewed },
            _ => throw new ArgumentOutOfRangeException(nameof(phase)),
        };
        foreach (var step in path)
        {
            lease = await svc.TransitionPhaseAsync(lease.Id, step, Operator);
        }
        return (svc, lease);
    }

    [Fact]
    public async Task Transition_Draft_To_AwaitingSignature_OK()
    {
        var (svc, lease) = await NewDraftAsync();
        var updated = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);
        Assert.Equal(LeasePhase.AwaitingSignature, updated.Phase);
    }

    [Fact]
    public async Task Transition_Draft_To_Cancelled_OK()
    {
        var (svc, lease) = await NewDraftAsync();
        var updated = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Cancelled, Operator);
        Assert.Equal(LeasePhase.Cancelled, updated.Phase);
    }

    [Fact]
    public async Task Transition_AwaitingSignature_To_Executed_OK()
    {
        var (svc, lease) = await AdvanceToAsync(LeasePhase.AwaitingSignature);
        var updated = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator);
        Assert.Equal(LeasePhase.Executed, updated.Phase);
    }

    [Fact]
    public async Task Transition_AwaitingSignature_To_Draft_RevisionsAllowed()
    {
        var (svc, lease) = await AdvanceToAsync(LeasePhase.AwaitingSignature);
        var updated = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Draft, Operator);
        Assert.Equal(LeasePhase.Draft, updated.Phase);
    }

    [Fact]
    public async Task Transition_Executed_To_Active_OK()
    {
        var (svc, lease) = await AdvanceToAsync(LeasePhase.Executed);
        var updated = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Active, Operator);
        Assert.Equal(LeasePhase.Active, updated.Phase);
    }

    [Fact]
    public async Task Transition_Active_To_Renewed_OK()
    {
        var (svc, lease) = await AdvanceToAsync(LeasePhase.Active);
        var updated = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Renewed, Operator);
        Assert.Equal(LeasePhase.Renewed, updated.Phase);
    }

    [Fact]
    public async Task Transition_Active_To_Terminated_OK()
    {
        var (svc, lease) = await AdvanceToAsync(LeasePhase.Active);
        var updated = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Terminated, Operator);
        Assert.Equal(LeasePhase.Terminated, updated.Phase);
    }

    [Fact]
    public async Task Transition_Renewed_To_Active_OK()
    {
        var (svc, lease) = await AdvanceToAsync(LeasePhase.Renewed);
        var updated = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Active, Operator);
        Assert.Equal(LeasePhase.Active, updated.Phase);
    }

    [Fact]
    public async Task Transition_Draft_To_Active_Rejected()
    {
        var (svc, lease) = await NewDraftAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionPhaseAsync(lease.Id, LeasePhase.Active, Operator).AsTask());
    }

    [Fact]
    public async Task Transition_FromTerminated_Rejected()
    {
        var (svc, lease) = await AdvanceToAsync(LeasePhase.Active);
        var terminated = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Terminated, Operator);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionPhaseAsync(terminated.Id, LeasePhase.Renewed, Operator).AsTask());
    }

    [Fact]
    public async Task Transition_FromCancelled_Rejected()
    {
        var (svc, lease) = await NewDraftAsync();
        var cancelled = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Cancelled, Operator);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionPhaseAsync(cancelled.Id, LeasePhase.Active, Operator).AsTask());
    }

    [Fact]
    public async Task Transition_UnknownLease_Rejected()
    {
        var svc = new InMemoryLeaseService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionPhaseAsync(LeaseId.NewId(), LeasePhase.AwaitingSignature, Operator).AsTask());
    }
}
