using Sunfish.Foundation.Notifications;
using Xunit;

namespace Sunfish.Foundation.Tests;

public class NotificationContractsTests
{
    [Fact]
    public void UserNotification_CanBeConstructed_WithDefaults()
    {
        var n = new UserNotification
        {
            Title = "Task assigned",
            Message = "A new task was assigned to you.",
            Source = NotificationSource.Tasks,
            Category = NotificationCategory.Assignment,
            Importance = NotificationImportance.Normal,
            CorrelationKey = "task:123",
        };

        Assert.False(n.IsRead);
        Assert.Equal("Task assigned", n.Title);
        Assert.Equal(NotificationSource.Tasks, n.Source);
        Assert.Equal(NotificationDelivery.FeedOnly, n.Delivery);
        Assert.False(string.IsNullOrEmpty(n.Id));
    }

    [Fact]
    public void NotificationDelivery_FeedAndToast_ContainsBothFlags()
    {
        var both = NotificationDelivery.FeedAndToast;
        Assert.True((both & NotificationDelivery.FeedOnly) == NotificationDelivery.FeedOnly);
        Assert.True((both & NotificationDelivery.ToastOnly) == NotificationDelivery.ToastOnly);
    }

    [Fact]
    public void UserNotification_WithCopy_PreservesUnsetFieldsAndUpdatesRead()
    {
        var original = new UserNotification { Message = "hi" };
        var read = original with { IsRead = true };

        Assert.False(original.IsRead);
        Assert.True(read.IsRead);
        Assert.Equal(original.Id, read.Id);
        Assert.Equal("hi", read.Message);
    }
}

public class TenantContextContractTests
{
    private sealed class StubTenant : Sunfish.Foundation.Authorization.ITenantContext
    {
        public string TenantId => "t1";
        public string UserId => "u1";
        public IReadOnlyList<string> Roles { get; } = ["Admin"];
        public bool HasPermission(string permission) => permission == "view";
    }

    [Fact]
    public void ITenantContext_ImplementationExposesExpectedShape()
    {
        Sunfish.Foundation.Authorization.ITenantContext tc = new StubTenant();
        Assert.Equal("t1", tc.TenantId);
        Assert.Equal("u1", tc.UserId);
        Assert.Single(tc.Roles);
        Assert.True(tc.HasPermission("view"));
        Assert.False(tc.HasPermission("delete"));
    }
}
