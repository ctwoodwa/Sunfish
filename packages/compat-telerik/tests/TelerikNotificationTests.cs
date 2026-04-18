using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikNotificationTests
{
    [Fact]
    public void TelerikNotification_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikNotification);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
