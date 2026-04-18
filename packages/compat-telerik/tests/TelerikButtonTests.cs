using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikButtonTests
{
    [Fact]
    public void TelerikButton_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikButton);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }

    [Fact]
    public void ButtonType_EnumExposesButtonSubmitReset()
    {
        // Reset is publicly exposed in the enum but throws at runtime — this test
        // locks the compat enum surface.
        var members = System.Enum.GetNames(typeof(Sunfish.Compat.Telerik.Enums.ButtonType));
        Assert.Contains("Button", members);
        Assert.Contains("Submit", members);
        Assert.Contains("Reset", members);
    }
}
