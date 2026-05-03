using Sunfish.UIAdapters.Blazor.Components.Forms.Containers;
using Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

public class FormsTests
{
    [Fact]
    public void SunfishForm_TypeIsPublicAndInContainersNamespace()
    {
        var type = typeof(SunfishForm);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Forms.Containers", type.Namespace);
    }

    [Fact]
    public void SunfishCheckbox_TypeIsInInputsNamespace()
    {
        var type = typeof(SunfishCheckbox);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Forms.Inputs", type.Namespace);
    }
}
