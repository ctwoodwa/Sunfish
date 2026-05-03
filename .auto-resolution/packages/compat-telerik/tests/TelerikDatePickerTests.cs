using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikDatePickerTests
{
    [Fact]
    public void TelerikDatePicker_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikDatePicker);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
