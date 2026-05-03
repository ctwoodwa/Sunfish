using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikComboBoxTests
{
    [Fact]
    public void TelerikComboBox_TypeIsPublicGenericAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikComboBox<,>);
        Assert.True(type.IsPublic);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Equal(2, type.GetGenericArguments().Length);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
