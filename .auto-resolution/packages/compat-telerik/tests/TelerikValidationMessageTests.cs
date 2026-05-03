using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikValidationMessageTests
{
    [Fact]
    public void TelerikValidationMessage_TypeIsPublicGenericAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikValidationMessage<>);
        Assert.True(type.IsPublic);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Single(type.GetGenericArguments());
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
