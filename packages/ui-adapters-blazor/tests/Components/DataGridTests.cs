using Sunfish.Components.Blazor.Components.DataDisplay;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

public class DataGridTests
{
    [Fact]
    public void SunfishDataGrid_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishDataGrid<>);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Components.Blazor.Components.DataDisplay", type.Namespace);
    }

    [Fact]
    public void SunfishDataSheet_TypeExists()
    {
        var type = typeof(SunfishDataSheet<>);
        Assert.Equal("Sunfish.Components.Blazor.Components.DataDisplay", type.Namespace);
    }

    [Fact]
    public void SunfishColumnBase_TypeExists()
    {
        var type = typeof(SunfishColumnBase);
        Assert.Equal("Sunfish.Components.Blazor.Components.DataDisplay", type.Namespace);
    }
}
