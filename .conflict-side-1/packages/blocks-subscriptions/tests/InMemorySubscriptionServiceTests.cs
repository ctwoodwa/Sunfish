using Sunfish.Blocks.Subscriptions.Models;
using Sunfish.Blocks.Subscriptions.Services;
using Xunit;

namespace Sunfish.Blocks.Subscriptions.Tests;

public class InMemorySubscriptionServiceTests
{
    private static CreateSubscriptionRequest MakeRequest(
        string planLocalPart = "plan-standard",
        Edition edition = Edition.Standard) =>
        new()
        {
            PlanId = new PlanId(planLocalPart),
            Edition = edition,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = null
        };

    [Fact]
    public async Task ListPlansAsync_ReturnsThreeSeededPlans()
    {
        var svc = new InMemorySubscriptionService();

        var plans = await CollectAsync(svc.ListPlansAsync());

        Assert.Equal(3, plans.Count);
        Assert.Contains(plans, p => p.Edition == Edition.Lite);
        Assert.Contains(plans, p => p.Edition == Edition.Standard);
        Assert.Contains(plans, p => p.Edition == Edition.Enterprise);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_AssignsIdAndPersists()
    {
        var svc = new InMemorySubscriptionService();

        var created = await svc.CreateSubscriptionAsync(MakeRequest());
        var retrieved = await svc.GetSubscriptionAsync(created.Id);

        Assert.False(string.IsNullOrWhiteSpace(created.Id.Value));
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(Edition.Standard, retrieved.Edition);
    }

    [Fact]
    public async Task ListSubscriptionsAsync_ReturnsAll_WhenQueryIsEmpty()
    {
        var svc = new InMemorySubscriptionService();
        await svc.CreateSubscriptionAsync(MakeRequest("plan-lite", Edition.Lite));
        await svc.CreateSubscriptionAsync(MakeRequest("plan-standard", Edition.Standard));
        await svc.CreateSubscriptionAsync(MakeRequest("plan-enterprise", Edition.Enterprise));

        var subs = await CollectAsync(svc.ListSubscriptionsAsync(ListSubscriptionsQuery.Empty));

        Assert.Equal(3, subs.Count);
    }

    [Fact]
    public async Task ListSubscriptionsAsync_FiltersBy_Edition()
    {
        var svc = new InMemorySubscriptionService();
        await svc.CreateSubscriptionAsync(MakeRequest("plan-lite", Edition.Lite));
        await svc.CreateSubscriptionAsync(MakeRequest("plan-standard", Edition.Standard));

        var lite = await CollectAsync(
            svc.ListSubscriptionsAsync(new ListSubscriptionsQuery { Edition = Edition.Lite }));
        var enterprise = await CollectAsync(
            svc.ListSubscriptionsAsync(new ListSubscriptionsQuery { Edition = Edition.Enterprise }));

        Assert.Single(lite);
        Assert.Empty(enterprise);
    }

    [Fact]
    public async Task AddAddOnAsync_AttachesAddOn_Idempotently()
    {
        var svc = new InMemorySubscriptionService();
        var sub = await svc.CreateSubscriptionAsync(MakeRequest());
        var addOnId = new AddOnId("addon-priority-support");

        var afterFirstAdd = await svc.AddAddOnAsync(sub.Id, addOnId);
        var afterSecondAdd = await svc.AddAddOnAsync(sub.Id, addOnId);

        Assert.Contains(addOnId, afterFirstAdd.AddOns);
        Assert.Single(afterSecondAdd.AddOns);
    }

    [Fact]
    public async Task RecordUsageAsync_PersistsUsage_WithPositiveQuantity()
    {
        var svc = new InMemorySubscriptionService();
        var meterId = new UsageMeterId("meter-api-calls");

        var usage = await svc.RecordUsageAsync(meterId, 42m);

        Assert.NotEqual(Guid.Empty, usage.Id);
        Assert.Equal(meterId, usage.MeterId);
        Assert.Equal(42m, usage.Quantity);
    }

    [Fact]
    public async Task RecordUsageAsync_RejectsNegativeQuantity()
    {
        var svc = new InMemorySubscriptionService();
        var meterId = new UsageMeterId("meter-api-calls");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.RecordUsageAsync(meterId, -1m).AsTask());
    }

    [Fact]
    public async Task CreateSubscriptionAsync_ThrowsOnNull_Request()
    {
        var svc = new InMemorySubscriptionService();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.CreateSubscriptionAsync(null!).AsTask());
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }
}
