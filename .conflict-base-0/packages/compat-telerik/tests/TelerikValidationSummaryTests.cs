using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class TelerikValidationSummaryTests
{
    [Fact]
    public void TelerikValidationSummary_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.TelerikValidationSummary);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }
}
