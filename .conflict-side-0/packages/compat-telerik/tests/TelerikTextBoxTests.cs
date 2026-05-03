using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikTextBoxTests
{
    [Fact]
    public void TelerikTextBox_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikTextBox);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
