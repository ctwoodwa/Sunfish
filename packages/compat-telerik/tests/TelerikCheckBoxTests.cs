using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikCheckBoxTests
{
    [Fact]
    public void TelerikCheckBox_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikCheckBox);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
