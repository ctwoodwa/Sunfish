using System.Reflection;
using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbTooltipTests
{
    [Fact]
    public void IgbTooltip_ExposesOpenAnchorPlacementContent()
    {
        var props = typeof(Sunfish.Compat.Infragistics.IgbTooltip).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(props, p => p.Name == "Open");
        Assert.Contains(props, p => p.Name == "Anchor");
        Assert.Contains(props, p => p.Name == "Placement");
        Assert.Contains(props, p => p.Name == "Content");
    }

    [Fact]
    public void IgbPlacement_EnumExposesTopBottomLeftRight()
    {
        var members = System.Enum.GetNames(typeof(Sunfish.Compat.Infragistics.Enums.IgbPlacement));
        Assert.Contains("Top", members);
        Assert.Contains("Bottom", members);
        Assert.Contains("Left", members);
        Assert.Contains("Right", members);
    }
}
